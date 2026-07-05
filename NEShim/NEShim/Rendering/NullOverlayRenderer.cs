using System.Diagnostics.CodeAnalysis;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace NEShim.Rendering;

/// <summary>
/// No-op <see cref="IOverlayRenderer"/>. Used when <c>forceRenderer = "gdi"</c> is set
/// in configuration, bypassing D3D11 device creation entirely.
/// Device and SwapChain are always null; all methods are no-ops.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class NullOverlayRenderer : IOverlayRenderer
{
    public ID3D11Device?   Device    => null;
    public IDXGISwapChain? SwapChain => null;

    public void Present()                     { }
    public void Resize(int width, int height) { }
    public void Dispose()                     { }
}
