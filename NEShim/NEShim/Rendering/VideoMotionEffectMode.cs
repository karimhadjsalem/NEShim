namespace NEShim.Rendering;

public enum VideoMotionEffectMode
{
    None,
    CrtJitter,
    ScanlineBob,
    MagneticDistortion,
}

public static class VideoMotionEffectModeParser
{
    // Most likely used first: None, CRT Jitter, Scanline Bob
    public static readonly VideoMotionEffectMode[] AllModes =
        [VideoMotionEffectMode.None, VideoMotionEffectMode.CrtJitter, VideoMotionEffectMode.ScanlineBob, VideoMotionEffectMode.MagneticDistortion];

    public static VideoMotionEffectMode Parse(string value) => value switch
    {
        "None"               => VideoMotionEffectMode.None,
        "CrtJitter"          => VideoMotionEffectMode.CrtJitter,
        "ScanlineBob"        => VideoMotionEffectMode.ScanlineBob,
        "MagneticDistortion" => VideoMotionEffectMode.MagneticDistortion,
        _ => throw new ArgumentException($"Unknown videoMotionEffect value: '{value}'"),
    };

    public static string DisplayName(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.None               => "None",
        VideoMotionEffectMode.CrtJitter          => "CRT Jitter",
        VideoMotionEffectMode.ScanlineBob        => "Scanline Bob",
        VideoMotionEffectMode.MagneticDistortion => "Magnetic Distortion",
        _                                        => mode.ToString(),
    };
}
