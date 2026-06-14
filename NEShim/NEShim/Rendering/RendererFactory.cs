namespace NEShim.Rendering;

/// <summary>
/// Creates the appropriate <see cref="IFrameRenderer"/> for the current hardware.
/// Tries D3D11 first; falls back to GDI+ if D3D11 initialisation fails.
/// Also sets <see cref="Platform.PlatformDetector.IsD3D11Active"/>.
/// </summary>
internal static class RendererFactory
{
    internal static IFrameRenderer Create(
        D3DOverlayHook hook,
        GamePanel      gamePanel,
        int            nesWidth,
        int            nesHeight)
    {
        if (hook.Device is not null && hook.SwapChain is not null)
        {
            try
            {
                var renderer = new D3D11Renderer(hook.Device, hook.SwapChain, nesWidth, nesHeight);
                Platform.PlatformDetector.SetD3D11Active(true);
                Logger.Log("[Renderer] D3D11 active — video filters supported.");
                return renderer;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Renderer] D3D11 init failed: {ex.Message} — falling back to GDI+.");
            }
        }

        Platform.PlatformDetector.SetD3D11Active(false);
        Logger.Log("[Renderer] GDI+ active. Video filters unavailable.");
        return new GdiRenderer(gamePanel, hook);
    }
}
