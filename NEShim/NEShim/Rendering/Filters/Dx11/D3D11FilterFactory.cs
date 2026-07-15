namespace NEShim.Rendering.Filters;

/// <summary>
/// Creates the <see cref="ID3D11Filter"/> implementation for a given <see cref="VideoFilterMode"/>.
/// Called by D3D11Renderer's own SetFilter/SetOverlayFilter/InitializeRenderingOptions
/// (see IFrameRenderer) to resolve the platform-neutral mode into a concrete D3D11 filter.
/// </summary>
internal static class D3D11FilterFactory
{
    public static ID3D11Filter Create(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.PixelPerfect  => new PixelPerfectD3D11Filter(),
        VideoFilterMode.Bilinear      => new BilinearD3D11Filter(),
        VideoFilterMode.CrtScanlines  => new CrtScanlinesD3D11Filter(),
        VideoFilterMode.CrtPhosphor   => new CrtPhosphorD3D11Filter(),
        VideoFilterMode.NtscComposite => new NtscCompositeD3D11Filter(),
        VideoFilterMode.CrtScreen     => new CrtScreenD3D11Filter(),
        VideoFilterMode.Xbr           => new XbrD3D11Filter(),
        _                             => new PixelPerfectD3D11Filter(),
    };
}
