using NEShim.Platform;

namespace NEShim.Rendering;

internal static class RendererFactory
{
    internal static IFrameRenderer Create(
        IOverlayRenderer overlayRenderer,
        int              nesWidth,
        int              nesHeight,
        IWindowHost      windowHost)
    {
        if (overlayRenderer.Device is nint devicePtr && overlayRenderer.SwapChain is nint swapChainPtr)
        {
            var renderer = new D3D11Renderer(devicePtr, swapChainPtr, nesWidth, nesHeight);
            PlatformDetector.SetD3D11Active(true);
            PlatformDetector.SetSdlGpuRendererActive(false);
            Logger.Log("[Renderer] D3D11 active — video filters supported.");
            return renderer;
        }

        Logger.Log("[Renderer] D3D11 unavailable — using SDL hardware renderer.");
        var fallback = new SDL3HwRenderer(windowHost.SdlWindow, nesWidth, nesHeight);
        // This factory can be invoked more than once (device-loss recovery re-creates the
        // renderer) — a prior call may have left IsD3D11Active set from a successful D3D11
        // construction. Must be explicitly cleared here, not just left to default false, or a
        // later fallback after D3D11 stops working would leave IsD3D11Active stuck true while
        // the live renderer is actually SDL3HwRenderer — SupportsAdvancedVideoFeatures would then
        // incorrectly report the full D3D11-only feature set as available.
        PlatformDetector.SetD3D11Active(false);
        PlatformDetector.SetSdlGpuRendererActive(fallback.IsGpuRendererActive);
        return fallback;
    }
}
