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
    private IGraphicsScaler  _scaler = new NearestNeighborScaler();
    private Bitmap?          _sidebarLeft;
    private Bitmap?          _sidebarRight;

    // Toast notification
    private string? _toastText;
    private DateTime _toastExpiry;

    // FPS overlay — set each frame by EmulationThread
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float CurrentFps { get; set; }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowFps { get; set; }

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

    public void SetMenu(InGameMenu menu)             => _menu     = menu;

    public void SetMainMenu(MainMenuScreen mainMenu) => _mainMenu = mainMenu;
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
        _toastExpiry = DateTime.UtcNow.AddSeconds(1.5);
    }

    /// <summary>Called from EmulationThread (via BeginInvoke) when a new frame is ready.</summary>
    public void UpdateFrame()
    {
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

    private const int WM_SETCURSOR = 0x0020;

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    protected override void WndProc(ref Message m)
    {
        // When no menu is active, suppress the default cursor so the pointer is hidden
        if (m.Msg == WM_SETCURSOR && !IsMenuActive)
        {
            SetCursor(IntPtr.Zero);
            m.Result = new IntPtr(1);
            return;
        }
        base.WndProc(ref m);
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

        // Pre-game main menu takes over the whole panel
        if (_mainMenu?.IsVisible == true)
        {
            MainMenuRenderer.Draw(g, ClientRectangle, _mainMenu);
            return;
        }

        g.CompositingMode = CompositingMode.SourceCopy;
        _scaler.Configure(g);

        // Compute letterboxed destination using 8:7 pixel aspect ratio
        // NES pixels are not square — display width = bufferWidth * (8/7)
        int srcW = _frameBuffer.Width;
        int srcH = _frameBuffer.Height;

        float displayAspect = srcW * (8f / 7f) / srcH;
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
            // Menu is open: draw frozen frame + overlay
            g.CompositingMode   = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(_bitmap, destRect, srcRect, GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
            _menu.Render(g, ClientRectangle);
        }
        else
        {
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

        // FPS overlay
        if (ShowFps)
            DrawFps(g, CurrentFps);
    }

    private static void DrawSidebar(Graphics g, Bitmap bmp, Rectangle dest)
    {
        // Draw at 1:1 pixel resolution, centered, cropping any overflow
        int srcW = Math.Min(bmp.Width,  dest.Width);
        int srcH = Math.Min(bmp.Height, dest.Height);
        int srcX = (bmp.Width  - srcW) / 2;
        int srcY = (bmp.Height - srcH) / 2;
        int dstX = dest.X + (dest.Width  - srcW) / 2;
        int dstY = dest.Y + (dest.Height - srcH) / 2;

        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(bmp,
            new Rectangle(dstX, dstY, srcW, srcH),
            new Rectangle(srcX, srcY, srcW, srcH),
            GraphicsUnit.Pixel);
    }

    private void DrawToast(Graphics g, string text)
    {
        using var font = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        float x = (Width  - size.Width)  / 2f;
        float y = Height  - size.Height  - 30f;

        using var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        g.CompositingMode = CompositingMode.SourceOver;
        g.FillRectangle(bg, x - 8, y - 4, size.Width + 16, size.Height + 8);

        using var fg = new SolidBrush(Color.White);
        g.DrawString(text, font, fg, x, y);
    }

    private void DrawFps(Graphics g, float fps)
    {
        string text = $"{fps:F1} fps";
        using var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        float x = Width  - size.Width  - 10f;
        float y = 8f;

        using var bg = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
        g.CompositingMode = CompositingMode.SourceOver;
        g.FillRectangle(bg, x - 4, y - 2, size.Width + 8, size.Height + 4);

        using var fg = new SolidBrush(Color.FromArgb(255, 180, 255, 120));
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
