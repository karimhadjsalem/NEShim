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
        Logger.Log("[Renderer] Using SDL hardware renderer.");
        return new SDL3HwRenderer(windowHost.SdlWindow, nesWidth, nesHeight);
    }
}
