using System.Drawing;

namespace NEShim.Rendering;

/// <summary>
/// GDI+ renderer strategy. Delegates frame display and overlays to GamePanel, and
/// swap chain presentation to D3DOverlayHook. Used when D3D11 initialisation fails.
/// Does not own GamePanel or D3DOverlayHook.
/// </summary>
internal sealed class GdiRenderer : IFrameRenderer
{
    private readonly GamePanel      _gamePanel;
    private readonly D3DOverlayHook _hook;

    internal GdiRenderer(GamePanel gamePanel, D3DOverlayHook hook)
    {
        _gamePanel = gamePanel;
        _hook      = hook;
    }

    public bool OwnsFrameSurface => false;

    // GdiRenderer never loses the device — this event is intentionally never fired.
    public event EventHandler? DeviceLost
    {
        add    { }
        remove { }
    }

    public void UploadFrame(ReadOnlySpan<int> pixels, int contentWidth, int contentHeight)
        => _gamePanel.UpdateFrame(pixels);
    public void Tick(bool vsync)          => _hook.Present();
    public void Resize(int width, int height) => _hook.Resize(width, height);

    public void UpdateFpsOverlay(bool show, float fps)
    {
        _gamePanel.ShowFps    = show;
        _gamePanel.CurrentFps = fps;
    }

    public void SetSidebars(Bitmap? left, Bitmap? right) => _gamePanel.SetSidebars(left, right);
    public void ShowToast(string text)                   => _gamePanel.ShowToast(text);
    public void ShowAchievementNotification(string name) => _gamePanel.ShowAchievementNotification(name);

    /// <summary>
    /// Injects a new GDI+ filter. Takes effect on the next paint cycle.
    /// Called by MainForm with a filter created by GdiFilterFactory.
    /// </summary>
    public void SetFilter(Filters.IGdiFilter filter) => _gamePanel.SetFilter(filter);

    /// <inheritdoc/>
    public void SetOverscanMode(OverscanMode overscan) => _gamePanel.SetOverscanMode(overscan);

    /// <summary>
    /// Applies both filter and overscan in one call. Used at startup and after device recovery.
    /// </summary>
    public void InitializeRenderingOptions(Filters.IGdiFilter filter, OverscanMode overscan)
    {
        _gamePanel.SetFilter(filter);
        _gamePanel.SetOverscanMode(overscan);
    }

    // GDI+ path: scene rendering goes through GamePanel.OnPaint; these are no-ops.
    public void SetMenuSceneProvider(IMenuSceneProvider? provider) { }
    public void MarkOverlayDirty() { }

    public void Dispose() { } // does not own gamePanel or hook
}
