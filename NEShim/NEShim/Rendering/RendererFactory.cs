using System.Diagnostics.CodeAnalysis;

namespace NEShim.Rendering;

/// <summary>
/// Creates the appropriate <see cref="IFrameRenderer"/> for the current hardware.
/// In SDL3 mode D3D11 is required; if initialisation fails the exception propagates.
/// Also sets <see cref="Platform.PlatformDetector.IsD3D11Active"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class RendererFactory
{
    internal static IFrameRenderer Create(
        IOverlayRenderer overlayRenderer,
        int              nesWidth,
        int              nesHeight,
        string           forceRenderer = "auto")
    {
        bool skipD3D11 = forceRenderer.Equals("gdi", StringComparison.OrdinalIgnoreCase);

        if (!skipD3D11 && overlayRenderer.Device is not null && overlayRenderer.SwapChain is not null)
        {
            var renderer = new D3D11Renderer(overlayRenderer.Device, overlayRenderer.SwapChain, nesWidth, nesHeight);
            Platform.PlatformDetector.SetD3D11Active(true);
            Logger.Log("[Renderer] D3D11 active — video filters supported.");
            return renderer;
        }

        throw new InvalidOperationException(
            skipD3D11
                ? "GDI+ renderer is no longer supported (removed with WinForms cleanup)."
                : "D3D11 initialisation failed — no fallback renderer available.");
    }
}
