namespace NEShim.Audio;

/// <summary>
/// Processes mono NES APU samples into stereo output pairs.
/// Implementations apply different filter chains; the active processor
/// is injected into <see cref="AudioPlayer"/> and can be swapped at runtime.
/// </summary>
internal interface IAudioProcessor
{
    /// <summary>
    /// Process one mono NES sample (L-channel input).
    /// Returns the (L, R) stereo pair to write into the output buffer.
    /// Called once per stereo sample pair from AudioPlayer.Read().
    /// </summary>
    (short L, short R) Process(short monoSample);

    /// <summary>
    /// Reset all internal filter state to zero.
    /// Called when audio is paused or the processor is swapped to avoid pops.
    /// </summary>
    void ResetState();
}
