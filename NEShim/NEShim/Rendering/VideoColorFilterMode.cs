namespace NEShim.Rendering;

public enum VideoColorFilterMode
{
    None,
    Warm,
    Greyscale,
    NesColorCorrection,
    Cool,
}

public static class VideoColorFilterModeParser
{
    public static readonly VideoColorFilterMode[] AllModes = Enum.GetValues<VideoColorFilterMode>();

    public static VideoColorFilterMode Parse(string value) => value switch
    {
        "None"               => VideoColorFilterMode.None,
        "Warm"               => VideoColorFilterMode.Warm,
        "Greyscale"          => VideoColorFilterMode.Greyscale,
        "NesColorCorrection" => VideoColorFilterMode.NesColorCorrection,
        "Cool"               => VideoColorFilterMode.Cool,
        _ => throw new ArgumentException($"Unknown videoColorFilter value: '{value}'"),
    };

    public static string DisplayName(VideoColorFilterMode mode) => mode switch
    {
        VideoColorFilterMode.None               => "None",
        VideoColorFilterMode.Warm               => "Warm",
        VideoColorFilterMode.Greyscale          => "Greyscale",
        VideoColorFilterMode.NesColorCorrection => "NES Colors",
        VideoColorFilterMode.Cool               => "Cool",
        _                                       => mode.ToString(),
    };
}
