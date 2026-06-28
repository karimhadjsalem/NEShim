namespace NEShim.Rendering;

public enum VideoColorFilterMode
{
    None,
    Warm,
    Greyscale,
    NesColorCorrection,
    Cool,
    PhosphorAmber,
    PhosphorGreen,
}

public static class VideoColorFilterModeParser
{
    // Most likely used first: None, Warm, Cool, NES Colors, Greyscale, Phosphor Amber, Phosphor Green
    public static readonly VideoColorFilterMode[] AllModes =
        [VideoColorFilterMode.None, VideoColorFilterMode.Warm, VideoColorFilterMode.Cool,
         VideoColorFilterMode.NesColorCorrection, VideoColorFilterMode.Greyscale,
         VideoColorFilterMode.PhosphorAmber, VideoColorFilterMode.PhosphorGreen];

    public static VideoColorFilterMode Parse(string value) => value switch
    {
        "None"               => VideoColorFilterMode.None,
        "Warm"               => VideoColorFilterMode.Warm,
        "Greyscale"          => VideoColorFilterMode.Greyscale,
        "NesColorCorrection" => VideoColorFilterMode.NesColorCorrection,
        "Cool"               => VideoColorFilterMode.Cool,
        "PhosphorAmber"      => VideoColorFilterMode.PhosphorAmber,
        "PhosphorGreen"      => VideoColorFilterMode.PhosphorGreen,
        _ => throw new ArgumentException($"Unknown videoColorFilter value: '{value}'"),
    };

    public static string DisplayName(VideoColorFilterMode mode) => mode switch
    {
        VideoColorFilterMode.None               => "None",
        VideoColorFilterMode.Warm               => "Warm",
        VideoColorFilterMode.Greyscale          => "Greyscale",
        VideoColorFilterMode.NesColorCorrection => "NES Colors",
        VideoColorFilterMode.Cool               => "Cool",
        VideoColorFilterMode.PhosphorAmber      => "Phosphor Amber",
        VideoColorFilterMode.PhosphorGreen      => "Phosphor Green",
        _                                       => mode.ToString(),
    };
}
