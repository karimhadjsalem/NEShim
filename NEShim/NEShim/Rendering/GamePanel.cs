using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NEShim.Input;
using NEShim.UI;


namespace NEShim.Rendering;

/// <summary>
/// Renders NES frames to the screen using GDI+.
/// Scales the 256×240 (effective) NES buffer to fill the panel while maintaining
/// the NES's 8:7 pixel aspect ratio with letterboxing.
/// </summary>
internal sealed class GamePanel : Panel
{
    private readonly FrameBuffer _frameBuffer;
    private readonly Bitmap _bitmap;
    private InGameMenu?      _menu;
    private MainMenuScreen?  _mainMenu;
    private LogoScreen?      _logoScreen;
    private IGraphicsScaler  _scaler = new NearestNeighborScaler();
    private Bitmap?          _sidebarLeft;
    private Bitmap?          _sidebarRight;

    // Cursor visibility — tracked so Hide/Show calls stay balanced (they're reference-counted).
    private bool _cursorHidden;

    // When true, D3D11Renderer owns NES frame display; skip g.DrawImage(_bitmap) in OnPaint.
    // Menus and overlay UI still paint via GDI+. Set by MainForm after D3D11Renderer is created.
    private bool _d3dRendererActive;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool D3DRendererActive
    {
        get => _d3dRendererActive;
        set => _d3dRendererActive = value;
    }

    // Toast notification
    private string? _toastText;
    private DateTime _toastExpiry;

    // Achievement notification
    private string?  _achievementText;
    private DateTime _achievementExpiry;
    private const int AchievementDurationSeconds = 5;

    // FPS overlay — volatile fields shared between the emulation thread (writer)
    // and the UI paint thread (reader) to prevent stale reads without a lock.
    private volatile float _currentFps;
    private volatile bool  _showFps;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float CurrentFps { get => _currentFps; set => _currentFps = value; }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowFps { get => _showFps; set => _showFps = value; }

    // NES pixels are not square — display width = bufferWidth * (8/7)
    private const float NesPixelAspect = 8f / 7f;

    // ---- Overlay draw constants ----
    private const string OverlayFontFamily    = "Segoe UI";
    private const double ToastDurationSeconds = 1.5;
    private const float  ToastFontSize        = 14f;
    private const float  ToastBottomPad       = 30f;
    private const int    ToastBgPadX          = 8;
    private const int    ToastBgPadY          = 4;
    private const int    ToastBgAlpha         = 160;

    private const float AchievementMargin     = 15f;
    private const float AchievementPadding    = 10f;
    private const float AchievementInnerGap   = 4f;
    private const float AchievementHeaderSize = 10f;
    private const float AchievementNameSize   = 13f;
    private static readonly Color AchievementBgColor     = Color.FromArgb(210, 20, 20, 20);
    private static readonly Color AchievementBorderColor = Color.FromArgb(200, 200, 160, 40);
    private static readonly Color AchievementHeaderColor = Color.FromArgb(255, 220, 180, 50);

    private const float FpsFontSize  = 11f;
    private const float FpsRightPad  = 10f;
    private const float FpsTopPad    = 8f;
    private const int   FpsBgAlpha   = 140;
    private static readonly Color FpsTextColor = Color.FromArgb(255, 180, 255, 120);

    public GamePanel(FrameBuffer frameBuffer)
    {
        _frameBuffer = frameBuffer;
        _bitmap = new Bitmap(256, 240, PixelFormat.Format32bppArgb);

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.Opaque,
            true);
        DoubleBuffered = true;
        BackColor = Color.Black;
        TabStop = true; // Allow receiving keyboard focus
    }

    // Allow all keys (including arrows, escape, enter) to reach KeyDown
    protected override bool IsInputKey(Keys keyData) => true;

    public void SetMenu(InGameMenu menu)
    {
        _menu = menu;
        // Events fire on the emulation thread — marshal to UI thread.
        menu.Opened += () => BeginInvoke(() => SetCursorVisible(true));
        menu.Closed += () => BeginInvoke(() => SetCursorVisible(false));
    }

    public void SetMainMenu(MainMenuScreen mainMenu)
    {
        _mainMenu = mainMenu;
        SetCursorVisible(true); // main menu is visible on startup
    }

    public void SetLogoScreen(LogoScreen? logo) => _logoScreen = logo;

    private void SetCursorVisible(bool visible)
    {
        if (visible && _cursorHidden)  { Cursor.Show(); _cursorHidden = false; }
        if (!visible && !_cursorHidden) { Cursor.Hide(); _cursorHidden = true; }
    }
    public void SetScaler(IGraphicsScaler scaler)    => _scaler   = scaler;

    /// <summary>
    /// Sets the images drawn in the left and right letterbox bars during gameplay.
    /// Disposes any previously set bitmaps. Pass null to revert to plain black bars.
    /// </summary>
    public void SetSidebars(Bitmap? left, Bitmap? right)
    {
        _sidebarLeft?.Dispose();
        _sidebarRight?.Dispose();
        _sidebarLeft  = left;
        _sidebarRight = right;
    }

    private bool IsMenuActive => _mainMenu?.IsVisible == true || _menu?.IsOpen == true;

    /// <summary>
    /// True when the active menu is waiting for a gamepad button press (rebind mode).
    /// Checked by the emulation thread each pause-loop tick.
    /// </summary>
    public bool IsWaitingForGamepadButton
        => _mainMenu?.IsGamepadRebinding == true || _menu?.IsGamepadRebinding == true;

    /// <summary>Queues a toast message (shown for 1.5 seconds).</summary>
    public void ShowToast(string text)
    {
        _toastText  = text;
        _toastExpiry = DateTime.UtcNow.AddSeconds(ToastDurationSeconds);
    }

    /// <summary>
    /// Shows an achievement-unlocked notification in the bottom-right corner for
    /// <see cref="AchievementDurationSeconds"/> seconds.
    /// </summary>
    public void ShowAchievementNotification(string displayName)
    {
        _achievementText   = displayName;
        _achievementExpiry = DateTime.UtcNow.AddSeconds(AchievementDurationSeconds);
    }

    /// <summary>
    /// Copies the current front buffer into the GDI+ bitmap without invalidating.
    /// Called by MainForm before making GamePanel visible for a menu (D3D11 mode) so the
    /// frozen-frame background under the menu shows the most recent NES frame.
    /// </summary>
    internal void SyncBitmap()
    {
        var front = _frameBuffer.FrontBuffer;
        var data = _bitmap.LockBits(
            new Rectangle(0, 0, _bitmap.Width, _bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(front, 0, data.Scan0, Math.Min(front.Length, _bitmap.Width * _bitmap.Height));
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }
    }

    /// <summary>Called from EmulationThread (via BeginInvoke) when a new frame is ready.</summary>
    public void UpdateFrame()
    {
        SetCursorVisible(false); // game is running — no menu is open
        // Copy front buffer into the GDI+ bitmap
        var front = _frameBuffer.FrontBuffer;
        var data = _bitmap.LockBits(
            new Rectangle(0, 0, _bitmap.Width, _bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(front, 0, data.Scan0, Math.Min(front.Length, _bitmap.Width * _bitmap.Height));
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }

        Invalidate();
    }

    /// <summary>
    /// Dispatches a detected gamepad button press to the active menu's rebind handler.
    /// Called on the UI thread via BeginInvoke from the emulation thread.
    /// </summary>
    public void HandleGamepadButtonPress(string buttonName)
    {
        string? toast = null;
        if (_mainMenu?.IsVisible == true)
            toast = _mainMenu.HandleGamepadButtonPress(buttonName);
        else if (_menu?.IsOpen == true)
            toast = _menu.HandleGamepadButtonPress(buttonName);
        if (toast != null) ShowToast(toast);
    }

    /// <summary>
    /// Dispatches gamepad menu navigation to whichever menu is currently active.
    /// Called on the UI thread via BeginInvoke from the emulation thread.
    /// Returns true if a repaint is needed.
    /// </summary>
    public bool HandleGamepadNav(MenuNavInput nav)
    {
        if (_mainMenu?.IsVisible == true)
        {
            _mainMenu.HandleGamepadNav(nav);
            return true;
        }
        if (_menu?.IsOpen == true)
        {
            _menu.HandleGamepadNav(nav);
            return true;
        }
        return false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetCursorVisible(IsMenuActive);

        if (!IsMenuActive) return;

        bool repaint = false;
        if (_mainMenu?.IsVisible == true)
            repaint = _mainMenu.HandleMouseMove(e.Location, ClientRectangle);
        else if (_menu?.IsOpen == true)
            repaint = _menu.HandleMouseMove(e.Location, ClientRectangle);

        if (repaint) Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;

        bool repaint = false;
        if (_mainMenu?.IsVisible == true)
            repaint = _mainMenu.HandleMouseClick(e.Location, ClientRectangle);
        else if (_menu?.IsOpen == true)
            repaint = _menu.HandleMouseClick(e.Location, ClientRectangle);

        if (repaint) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        if (_logoScreen is not null)
        {
            LogoRenderer.Draw(g, ClientRectangle, _logoScreen.Image, _logoScreen.CurrentAlpha);
            return;
        }

        // Pre-game main menu takes over the whole panel
        if (_mainMenu?.IsVisible == true)
        {
            MainMenuRenderer.Draw(g, ClientRectangle, _mainMenu);
            return;
        }

        g.CompositingMode = CompositingMode.SourceCopy;
        _scaler.Configure(g);

        int srcW = _frameBuffer.Width;
        int srcH = _frameBuffer.Height;

        float displayAspect = srcW * NesPixelAspect / srcH;
        float panelAspect   = (float)Width / Height;

        int destW, destH;
        if (panelAspect > displayAspect)
        {
            destH = Height;
            destW = (int)(Height * displayAspect);
        }
        else
        {
            destW = Width;
            destH = (int)(Width / displayAspect);
        }

        int destX = (Width  - destW) / 2;
        int destY = (Height - destH) / 2;

        // Fill letterbox bars with black
        g.CompositingMode = CompositingMode.SourceCopy;
        using var black = new SolidBrush(Color.Black);
        if (destX > 0) g.FillRectangle(black, 0, 0, destX, Height);
        if (destY > 0) g.FillRectangle(black, 0, 0, Width, destY);
        if (destX > 0) g.FillRectangle(black, Width - destX, 0, destX, Height);
        if (destY > 0) g.FillRectangle(black, 0, Height - destY, Width, destY);

        // Draw sidebar images over the left and right bars if configured
        if (destX > 0)
        {
            if (_sidebarLeft  != null) DrawSidebar(g, _sidebarLeft,  new Rectangle(0,             0, destX, Height));
            if (_sidebarRight != null) DrawSidebar(g, _sidebarRight, new Rectangle(Width - destX, 0, destX, Height));
            if (_sidebarLeft != null || _sidebarRight != null)
                _scaler.Configure(g); // restore scaler settings changed by DrawSidebar
        }

        var srcRect  = new Rectangle(0, 0, srcW, srcH);
        var destRect = new Rectangle(destX, destY, destW, destH);

        if (_menu?.IsOpen == true)
        {
            // Menu is open: draw frozen frame + overlay.
            // In D3D11 mode, _bitmap is synced by MainForm before GamePanel is made visible.
            g.CompositingMode   = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            if (!_d3dRendererActive)
                g.DrawImage(_bitmap, destRect, srcRect, GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
            _menu.Render(g, ClientRectangle);
        }
        else if (!_d3dRendererActive)
        {
            // GDI+ path: blit the NES frame. In D3D11 mode this branch is unreachable
            // because GamePanel is hidden during gameplay (no menu, no overlay).
            g.DrawImage(_bitmap, destRect, srcRect, GraphicsUnit.Pixel);
        }

        // Draw toast if active
        if (_toastText is not null && DateTime.UtcNow < _toastExpiry)
        {
            DrawToast(g, _toastText);
        }
        else
        {
            _toastText = null;
        }

        // Achievement notification
        if (_achievementText is not null)
        {
            if (DateTime.UtcNow < _achievementExpiry)
                DrawAchievementNotification(g, _achievementText);
            else
                _achievementText = null;
        }

        // FPS overlay
        if (ShowFps)
            DrawFps(g, CurrentFps);
    }

    /// <summary>
    /// Computes the source and destination rectangles for cover-scale sidebar rendering.
    /// The image is scaled uniformly so it fills the entire dest area, then center-cropped.
    /// </summary>
    internal static (RectangleF src, Rectangle dst) ComputeSidebarCover(Size imageSize, Rectangle dest)
    {
        float scale = Math.Max((float)dest.Width / imageSize.Width, (float)dest.Height / imageSize.Height);
        float srcW  = dest.Width  / scale;
        float srcH  = dest.Height / scale;
        float srcX  = (imageSize.Width  - srcW) / 2f;
        float srcY  = (imageSize.Height - srcH) / 2f;
        return (new RectangleF(srcX, srcY, srcW, srcH), dest);
    }

    internal static void DrawSidebar(Graphics g, Bitmap bmp, Rectangle dest)
    {
        var (src, dst) = ComputeSidebarCover(bmp.Size, dest);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
    }

    private void DrawToast(Graphics g, string text)
    {
        using var font = new Font(OverlayFontFamily, ToastFontSize, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        float x = (Width  - size.Width)  / 2f;
        float y = Height  - size.Height  - ToastBottomPad;

        using var bg = new SolidBrush(Color.FromArgb(ToastBgAlpha, 0, 0, 0));
        g.CompositingMode = CompositingMode.SourceOver;
        g.FillRectangle(bg, x - ToastBgPadX, y - ToastBgPadY, size.Width + ToastBgPadX * 2, size.Height + ToastBgPadY * 2);

        using var fg = new SolidBrush(Color.White);
        g.DrawString(text, font, fg, x, y);
    }

    private void DrawAchievementNotification(Graphics g, string displayName)
    {
        const string header = "Achievement Unlocked!";

        using var headerFont = new Font(OverlayFontFamily, AchievementHeaderSize, FontStyle.Bold, GraphicsUnit.Point);
        using var nameFont   = new Font(OverlayFontFamily, AchievementNameSize,   FontStyle.Bold, GraphicsUnit.Point);

        var headerSize = g.MeasureString(header,      headerFont);
        var nameSize   = g.MeasureString(displayName, nameFont);

        float boxW = Math.Max(headerSize.Width, nameSize.Width) + AchievementPadding * 2;
        float boxH = headerSize.Height + nameSize.Height + AchievementPadding * 2 + AchievementInnerGap;
        float boxX = Width  - boxW - AchievementMargin;
        float boxY = Height - boxH - AchievementMargin;

        g.CompositingMode = CompositingMode.SourceOver;

        using var bg     = new SolidBrush(AchievementBgColor);
        using var border = new Pen(AchievementBorderColor, 1.5f);
        g.FillRectangle(bg, boxX, boxY, boxW, boxH);
        g.DrawRectangle(border, boxX, boxY, boxW, boxH);

        using var headerBrush = new SolidBrush(AchievementHeaderColor);
        using var nameBrush   = new SolidBrush(Color.White);
        g.DrawString(header,      headerFont, headerBrush, boxX + AchievementPadding, boxY + AchievementPadding);
        g.DrawString(displayName, nameFont,   nameBrush,   boxX + AchievementPadding, boxY + AchievementPadding + headerSize.Height + AchievementInnerGap);
    }

    private void DrawFps(Graphics g, float fps)
    {
        string text = $"{fps:F1} fps";
        using var font = new Font(OverlayFontFamily, FpsFontSize, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        float x = Width  - size.Width  - FpsRightPad;
        float y = FpsTopPad;

        using var bg = new SolidBrush(Color.FromArgb(FpsBgAlpha, 0, 0, 0));
        g.CompositingMode = CompositingMode.SourceOver;
        g.FillRectangle(bg, x - 4, y - 2, size.Width + 8, size.Height + 4);

        using var fg = new SolidBrush(FpsTextColor);
        g.DrawString(text, font, fg, x, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap.Dispose();
            _sidebarLeft?.Dispose();
            _sidebarRight?.Dispose();
        }
        base.Dispose(disposing);
    }
}
