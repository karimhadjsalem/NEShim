namespace NEShim.Rendering.Filters;

/// <summary>
/// Creates the <see cref="ISdlFilter"/> implementation for a given <see cref="VideoFilterMode"/>.
/// Mirrors <see cref="D3D11FilterFactory"/> for the SDL render path.
/// </summary>
internal static class SdlFilterFactory
{
    public static ISdlFilter Create(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.PixelPerfect  => new PixelPerfectSdlFilter(),
        VideoFilterMode.Bilinear      => new BilinearSdlFilter(),
        VideoFilterMode.CrtScanlines  => new CrtScanlinesSdlFilter(),
        VideoFilterMode.CrtPhosphor   => new CrtPhosphorSdlFilter(),
        VideoFilterMode.NtscComposite => new NtscCompositeSdlFilter(),
        VideoFilterMode.CrtScreen     => new CrtScreenSdlFilter(),
        VideoFilterMode.Xbr           => new XbrSdlFilter(),
        _                             => new PixelPerfectSdlFilter(),
    };
}
