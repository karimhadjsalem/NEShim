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
    public static VideoFilterMode Parse(string value) => value switch
    {
        "NearestNeighbour" => VideoFilterMode.NearestNeighbour,
        "Bilinear"         => VideoFilterMode.Bilinear,
        "PixelPerfect"     => VideoFilterMode.PixelPerfect,
        "CrtScanlines"     => VideoFilterMode.CrtScanlines,
        "NtscComposite"    => VideoFilterMode.NtscComposite,
        _ => throw new ArgumentException($"Unknown videoFilter value: '{value}'"),
    };
}
