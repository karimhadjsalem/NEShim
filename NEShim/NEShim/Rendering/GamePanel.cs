using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NEShim.Input;
using NEShim.UI;


namespace NEShim.Rendering;

/// <summary>
/// Renders NES frames and overlays using GDI+.
/// In D3D11 mode this panel is hidden during gameplay; it becomes visible only when
/// a menu opens, showing a frozen NES frame (synced via <see cref="SyncBitmap"/>)
/// as the menu background.
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

    // Toast notification
    private string? _toastText;
    private DateTime _toastExpiry;

    // Achievement notification
    private string?  _achievementText;
    private DateTime _achievementExpiry;

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
    public void SetScaler(IGraphicsScaler scaler) => _scaler = scaler;

    /// <summary>
    /// Sets the images drawn in the left and right letterbox bars during GDI+ gameplay.
    /// Does not take ownership — bitmaps are owned and disposed by MainForm.
    /// Pass null to revert to plain black bars.
    /// </summary>
    public void SetSidebars(Bitmap? left, Bitmap? right)
    {
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
        _toastText   = text;
        _toastExpiry = DateTime.UtcNow.AddSeconds(OverlayRenderer.ToastDurationSeconds);
    }

    /// <summary>
    /// Shows an achievement-unlocked notification in the bottom-right corner for
    /// <see cref="OverlayRenderer.AchievementDurationSeconds"/> seconds.
    /// </summary>
    public void ShowAchievementNotification(string displayName)
    {
        _achievementText   = displayName;
        _achievementExpiry = DateTime.UtcNow.AddSeconds(OverlayRenderer.AchievementDurationSeconds);
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

    /// <summary>
    /// Called from GdiRenderer (via BeginInvoke) when a new frame is ready.
    /// Copies the emulation pixel data into the GDI+ bitmap and triggers a repaint.
    /// </summary>
    internal unsafe void UpdateFrame(ReadOnlySpan<int> pixels)
    {
        SetCursorVisible(false); // game is running — no menu is open
        var data = _bitmap.LockBits(
            new Rectangle(0, 0, _bitmap.Width, _bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int byteCount = Math.Min(pixels.Length, _bitmap.Width * _bitmap.Height) * sizeof(int);
            fixed (int* src = pixels)
                Buffer.MemoryCopy(src, (void*)data.Scan0, byteCount, byteCount);
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
            if (_sidebarLeft  != null) OverlayRenderer.DrawSidebar(g, _sidebarLeft,  new Rectangle(0,             0, destX, Height));
            if (_sidebarRight != null) OverlayRenderer.DrawSidebar(g, _sidebarRight, new Rectangle(Width - destX, 0, destX, Height));
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
            g.DrawImage(_bitmap, destRect, srcRect, GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
            _menu.Render(g, ClientRectangle);
        }
        else
        {
            // GDI+ gameplay: blit the NES frame.
            // In D3D11 mode GamePanel is hidden during gameplay, so this branch is unreachable.
            g.DrawImage(_bitmap, destRect, srcRect, GraphicsUnit.Pixel);
        }

        // Draw toast if active
        if (_toastText is not null && DateTime.UtcNow < _toastExpiry)
        {
            OverlayRenderer.DrawToast(g, ClientRectangle, _toastText);
        }
        else
        {
            _toastText = null;
        }

        // Achievement notification
        if (_achievementText is not null)
        {
            if (DateTime.UtcNow < _achievementExpiry)
                OverlayRenderer.DrawAchievementNotification(g, ClientRectangle, _achievementText);
            else
                _achievementText = null;
        }

        // FPS overlay
        if (ShowFps)
            OverlayRenderer.DrawFps(g, ClientRectangle, CurrentFps);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap.Dispose();
            // _sidebarLeft and _sidebarRight are owned by MainForm — do not dispose here.
        }
        base.Dispose(disposing);
    }
}
