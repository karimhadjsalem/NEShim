namespace NEShim.Rendering;

public enum OverscanMode { Overscan, Normal, Underscan }

public static class OverscanModeParser
{
    public static OverscanMode Parse(string value) => value switch
    {
        "Overscan"           => OverscanMode.Overscan,
        "Normal"             => OverscanMode.Normal,
        "Underscan"          => OverscanMode.Underscan,
        "NTSC" or "Auto"     => OverscanMode.Overscan,
        "None"               => OverscanMode.Normal,
        _ => throw new ArgumentException($"Unknown overscanMode value: '{value}'"),
    };

    public static string DisplayName(OverscanMode mode) => mode switch
    {
        OverscanMode.Overscan  => "Overscan",
        OverscanMode.Normal    => "Normal",
        OverscanMode.Underscan => "Underscan",
        _                      => mode.ToString(),
    };
}
