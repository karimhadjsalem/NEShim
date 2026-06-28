namespace NEShim.Rendering;

public enum VideoFilterMode
{
    NearestNeighbour,
    Bilinear,
    PixelPerfect,
    CrtScanlines,
    CrtPhosphor,
    NtscComposite,
    CrtScreen,
}

public static class VideoFilterModeParser
{
    // Filters available in each rendering mode. Order defines the menu cycle sequence (most likely used first).
    public static readonly VideoFilterMode[] GdiSupported   = [VideoFilterMode.PixelPerfect, VideoFilterMode.Bilinear];
    public static readonly VideoFilterMode[] D3D11Supported =
        [VideoFilterMode.PixelPerfect, VideoFilterMode.Bilinear, VideoFilterMode.CrtScanlines, VideoFilterMode.CrtPhosphor, VideoFilterMode.CrtScreen, VideoFilterMode.NtscComposite];

    public static VideoFilterMode Parse(string value) => value switch
    {
        "NearestNeighbour" => VideoFilterMode.NearestNeighbour,
        "Bilinear"         => VideoFilterMode.Bilinear,
        "PixelPerfect"     => VideoFilterMode.PixelPerfect,
        "CrtScanlines"     => VideoFilterMode.CrtScanlines,
        "CrtPhosphor"      => VideoFilterMode.CrtPhosphor,
        "NtscComposite"    => VideoFilterMode.NtscComposite,
        "CrtScreen"        => VideoFilterMode.CrtScreen,
        _ => throw new ArgumentException($"Unknown videoFilter value: '{value}'"),
    };

    public static string DisplayName(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.Bilinear      => "Smooth",
        VideoFilterMode.PixelPerfect  => "Pixel Perfect",
        VideoFilterMode.CrtScanlines  => "CRT Scanlines",
        VideoFilterMode.CrtPhosphor   => "CRT Phosphor",
        VideoFilterMode.NtscComposite => "NTSC Composite",
        VideoFilterMode.CrtScreen     => "CRT Screen",
        _                             => mode.ToString(),
    };
}
