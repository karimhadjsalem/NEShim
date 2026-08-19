namespace NEShim.Rendering;

public enum VideoFilterMode
{
    /// <summary>Deprecated config alias only — see <see cref="Config.ConfigLoader"/>'s migration
    /// of the old <c>"NearestNeighbour"</c>/<c>graphicsSmoothingEnabled</c> config values. Never
    /// reaches the renderer filter factories via normal config load; both factories still map it
    /// explicitly (to Pixel Perfect) as a defensive fallback for any other caller.</summary>
    NearestNeighbour,
    Bilinear,
    PixelPerfect,
    CrtScanlines,
    CrtPhosphor,
    NtscComposite,
    CrtScreen,
    Xbr,
}

public static class VideoFilterModeParser
{
    // Filters supported by both D3D11 (DXBC) and SDL_GPU (SPIR-V). Order defines the menu cycle sequence.
    public static readonly VideoFilterMode[] D3D11Supported =
        [VideoFilterMode.PixelPerfect, VideoFilterMode.Bilinear, VideoFilterMode.CrtScanlines, VideoFilterMode.CrtPhosphor, VideoFilterMode.CrtScreen, VideoFilterMode.NtscComposite, VideoFilterMode.Xbr];

    // Overlay-eligible filters — work as a second pass on an already-scaled frame.
    // Order defines the VideoOverlay sub-menu sequence (most useful first).
    public static readonly VideoFilterMode[] OverlaySupported =
        [VideoFilterMode.CrtScanlines, VideoFilterMode.CrtPhosphor, VideoFilterMode.CrtScreen];

    public static VideoFilterMode? ParseOverlay(string value) => value switch
    {
        "CrtScanlines" => VideoFilterMode.CrtScanlines,
        "CrtPhosphor"  => VideoFilterMode.CrtPhosphor,
        "CrtScreen"    => VideoFilterMode.CrtScreen,
        _              => null,
    };

    public static VideoFilterMode Parse(string value) => value switch
    {
        "NearestNeighbour" => VideoFilterMode.NearestNeighbour,
        "Bilinear"         => VideoFilterMode.Bilinear,
        "PixelPerfect"     => VideoFilterMode.PixelPerfect,
        "CrtScanlines"     => VideoFilterMode.CrtScanlines,
        "CrtPhosphor"      => VideoFilterMode.CrtPhosphor,
        "NtscComposite"    => VideoFilterMode.NtscComposite,
        "CrtScreen"        => VideoFilterMode.CrtScreen,
        "Xbr"              => VideoFilterMode.Xbr,
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
        VideoFilterMode.Xbr           => "Sharp Pixel",
        _                             => mode.ToString(),
    };
}
