using System.Diagnostics.CodeAnalysis;

namespace NEShim.Rendering;

/// <summary>
/// No-op <see cref="IOverlayRenderer"/>, selected by <c>OverlayRendererFactory.Create</c> on
/// non-Windows platforms (via the <c>.Windows.cs</c>/plain-file MSBuild split — see
/// <c>OverlayRendererFactory.Windows.cs</c> for the Windows-only <see cref="SteamOverlayRenderer"/>
/// path), where D3D11 is unavailable and Steam's overlay is injected via LD_PRELOAD instead of a
/// swap-chain hook. Device and SwapChain are always null; all methods are no-ops.
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
