using System.Diagnostics.CodeAnalysis;

namespace NEShim.Rendering;

/// <summary>
/// Creates the appropriate <see cref="IFrameRenderer"/> for the current hardware.
/// Tries D3D11 first; falls back to GDI+ if D3D11 initialisation fails.
/// Also sets <see cref="Platform.PlatformDetector.IsD3D11Active"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class RendererFactory
{
    internal static IFrameRenderer Create(
        IOverlayRenderer overlayRenderer,
        GamePanel?       gamePanel,
        int              nesWidth,
        int              nesHeight,
        string           forceRenderer = "auto")
    {
        bool skipD3D11 = forceRenderer.Equals("gdi", StringComparison.OrdinalIgnoreCase);

        if (skipD3D11)
        {
            Logger.Log("[Renderer] GDI+ forced via forceRenderer config.");
        }
        else if (overlayRenderer.Device is not null && overlayRenderer.SwapChain is not null)
        {
            try
            {
                var renderer = new D3D11Renderer(overlayRenderer.Device, overlayRenderer.SwapChain, nesWidth, nesHeight);
                Platform.PlatformDetector.SetD3D11Active(true);
                Logger.Log("[Renderer] D3D11 active — video filters supported.");
                return renderer;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Renderer] D3D11 init failed: {ex.Message} — falling back to GDI+.");
            }
        }

        if (gamePanel is null)
            throw new InvalidOperationException(
                "D3D11 initialisation failed; GDI+ fallback unavailable (no GamePanel in SDL3 mode).");

        Platform.PlatformDetector.SetD3D11Active(false);
        Logger.Log("[Renderer] GDI+ active. Video filters unavailable.");
        return new GdiRenderer(gamePanel, overlayRenderer);
    }
}
