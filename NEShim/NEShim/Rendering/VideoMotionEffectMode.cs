namespace NEShim.Rendering;

public enum VideoMotionEffectMode
{
    None,
    CrtJitter,
}

public static class VideoMotionEffectModeParser
{
    public static readonly VideoMotionEffectMode[] AllModes = Enum.GetValues<VideoMotionEffectMode>();

    public static VideoMotionEffectMode Parse(string value) => value switch
    {
        "None"      => VideoMotionEffectMode.None,
        "CrtJitter" => VideoMotionEffectMode.CrtJitter,
        _ => throw new ArgumentException($"Unknown videoMotionEffect value: '{value}'"),
    };

    public static string DisplayName(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.None      => "None",
        VideoMotionEffectMode.CrtJitter => "CRT Jitter",
        _                               => mode.ToString(),
    };
}
