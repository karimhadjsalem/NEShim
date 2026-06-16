namespace NEShim.Rendering;

public enum VideoFilterMode
{
    NearestNeighbour,
    Bilinear,
    PixelPerfect,
    CrtScanlines,
    NtscComposite,
}

public static class VideoFilterModeParser
{
    // Filters available in each rendering mode. Order defines the menu cycle sequence.
    public static readonly VideoFilterMode[] GdiSupported   = [VideoFilterMode.Bilinear, VideoFilterMode.PixelPerfect];
    public static readonly VideoFilterMode[] D3D11Supported = [VideoFilterMode.PixelPerfect];

    public static VideoFilterMode Parse(string value) => value switch
    {
        "NearestNeighbour" => VideoFilterMode.NearestNeighbour,
        "Bilinear"         => VideoFilterMode.Bilinear,
        "PixelPerfect"     => VideoFilterMode.PixelPerfect,
        "CrtScanlines"     => VideoFilterMode.CrtScanlines,
        "NtscComposite"    => VideoFilterMode.NtscComposite,
        _ => throw new ArgumentException($"Unknown videoFilter value: '{value}'"),
    };

    public static string DisplayName(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.Bilinear      => "Smooth",
        VideoFilterMode.PixelPerfect  => "Pixel Perfect",
        VideoFilterMode.CrtScanlines  => "CRT Scanlines",
        VideoFilterMode.NtscComposite => "NTSC Composite",
        _                             => mode.ToString(),
    };
}
