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
    public void Tick(bool vsync)                          => _hook.Present();
    public void Resize(int width, int height)             => _hook.Resize(width, height);

    public void UpdateFpsOverlay(bool show, float fps)
    {
        _gamePanel.ShowFps    = show;
        _gamePanel.CurrentFps = fps;
    }

    public void SetSidebars(Bitmap? left, Bitmap? right) => _gamePanel.SetSidebars(left, right);
    public void ShowToast(string text)                   => _gamePanel.ShowToast(text);
    public void ShowAchievementNotification(string name) => _gamePanel.ShowAchievementNotification(name);

    public void Dispose() { } // does not own gamePanel or hook
}
