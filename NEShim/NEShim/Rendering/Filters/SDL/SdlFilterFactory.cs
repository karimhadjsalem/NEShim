namespace NEShim.Rendering.Filters;

/// <summary>
/// Creates the <see cref="ISdlFilter"/> implementation for a given <see cref="VideoFilterMode"/>.
/// Mirrors <see cref="D3D11FilterFactory"/> for the SDL render path.
/// </summary>
internal static class SdlFilterFactory
{
    public static ISdlFilter Create(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.PixelPerfect     => new PixelPerfectSdlFilter(),
        VideoFilterMode.Bilinear         => new BilinearSdlFilter(),
        VideoFilterMode.CrtScanlines     => new CrtScanlinesSdlFilter(),
        VideoFilterMode.CrtPhosphor      => new CrtPhosphorSdlFilter(),
        VideoFilterMode.NtscComposite    => new NtscCompositeSdlFilter(),
        VideoFilterMode.CrtScreen        => new CrtScreenSdlFilter(),
        VideoFilterMode.Xbr              => new XbrSdlFilter(),
        // Deprecated config alias only — ConfigLoader migrates "NearestNeighbour" away before
        // it reaches here; kept mapped explicitly so a value that does slip through still
        // resolves to something reasonable instead of hitting the throwing case below.
        VideoFilterMode.NearestNeighbour => new PixelPerfectSdlFilter(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped VideoFilterMode — add a case to SdlFilterFactory.Create."),
    };
}
