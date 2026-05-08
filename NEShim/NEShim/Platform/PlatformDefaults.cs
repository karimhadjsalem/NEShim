namespace NEShim.Platform;

/// <summary>
/// Resolves runtime-tunable defaults with optional config overrides.
/// </summary>
internal static class PlatformDefaults
{
    /// <summary>Returns the emulation spin window in milliseconds.</summary>
    internal static int ResolveEmulationSpinMs(int? configOverride) =>
        configOverride ?? 1;

    /// <summary>Returns the desired audio output latency in milliseconds.</summary>
    internal static int ResolveAudioLatencyMs(int? configOverride) =>
        configOverride ?? 50;
}
