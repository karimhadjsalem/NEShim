using System.Diagnostics.CodeAnalysis;

namespace NEShim.Rendering;

/// <summary>
/// No-op <see cref="IOverlayRenderer"/>. Used when <c>forceRenderer = "gdi"</c> is set
/// in configuration, bypassing D3D11 device creation entirely. Also used on non-Windows
/// platforms where D3D11 is unavailable.
/// Device and SwapChain are always null; all methods are no-ops.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class NullOverlayRenderer : IOverlayRenderer
{
    public nint? Device    => null;
    public nint? SwapChain => null;

    public void Present()                     { }
    public void Resize(int width, int height) { }
    public void Dispose()                     { }
}
