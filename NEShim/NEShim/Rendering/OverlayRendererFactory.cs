using NEShim.Platform;

namespace NEShim.Rendering;

internal static class OverlayRendererFactory
{
    internal static IOverlayRenderer Create(IWindowHost windowHost) => new NullOverlayRenderer();
}
