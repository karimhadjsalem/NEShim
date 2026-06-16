namespace NEShim.Audio;

public enum AudioFilterMode
{
    Default,
    Warm,
    PseudoStereo,
    WarmStereo,
    Compression,
    BassBoost,
    Saturation,
}

public static class AudioFilterModeParser
{
    public static AudioFilterMode Parse(string value) => value switch
    {
        "Default"      => AudioFilterMode.Default,
        "Warm"         => AudioFilterMode.Warm,
        "PseudoStereo" => AudioFilterMode.PseudoStereo,
        "WarmStereo"   => AudioFilterMode.WarmStereo,
        "Compression"  => AudioFilterMode.Compression,
        "BassBoost"    => AudioFilterMode.BassBoost,
        "Saturation"   => AudioFilterMode.Saturation,
        _ => throw new ArgumentException($"Unknown audioFilter value: '{value}'"),
    };

    public static string DisplayName(AudioFilterMode mode) => mode switch
    {
        AudioFilterMode.PseudoStereo => "Pseudo Stereo",
        AudioFilterMode.WarmStereo   => "Warm Stereo",
        AudioFilterMode.BassBoost    => "Bass Boost",
        _                            => mode.ToString(),
    };
}
