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
            Logger.Log("[Renderer] D3D11 active — video filters supported.");
            return renderer;
        }

        Logger.Log("[Renderer] D3D11 unavailable — using SDL hardware renderer.");
        return new SDL3HwRenderer(windowHost.SdlWindow, nesWidth, nesHeight);
    }
}
