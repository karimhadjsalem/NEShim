namespace NEShim.Rendering;

public enum OverscanMode { None, Ntsc, Auto }

public static class OverscanModeParser
{
    public static OverscanMode Parse(string value) => value switch
    {
        "None" => OverscanMode.None,
        "NTSC" => OverscanMode.Ntsc,
        "Auto" => OverscanMode.Auto,
        _ => throw new ArgumentException($"Unknown overscanMode value: '{value}'"),
    };
}
