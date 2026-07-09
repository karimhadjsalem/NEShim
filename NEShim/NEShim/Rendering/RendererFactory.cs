using NEShim.Platform;

namespace NEShim.Rendering;

/// <summary>
/// Creates the appropriate <see cref="IFrameRenderer"/> for the current hardware.
/// Prefers D3D11 when the overlay renderer has a live device and swap chain;
/// falls back to <see cref="SDL3HwRenderer"/> (Vulkan on Linux, Metal on macOS) otherwise.
/// Also sets <see cref="PlatformDetector.IsD3D11Active"/>.
/// </summary>
internal static class RendererFactory
{
    internal static IFrameRenderer Create(
        IOverlayRenderer overlayRenderer,
        int              nesWidth,
        int              nesHeight,
        IWindowHost      windowHost)
    {
        if (overlayRenderer.Device is not null && overlayRenderer.SwapChain is not null)
        {
            var renderer = new D3D11Renderer(overlayRenderer.Device, overlayRenderer.SwapChain, nesWidth, nesHeight);
            PlatformDetector.SetD3D11Active(true);
            Logger.Log("[Renderer] D3D11 active — video filters supported.");
            return renderer;
        }

        Logger.Log("[Renderer] D3D11 unavailable — using SDL hardware renderer.");
        return new SDL3HwRenderer(windowHost.SdlWindow, nesWidth, nesHeight);
    }
}
