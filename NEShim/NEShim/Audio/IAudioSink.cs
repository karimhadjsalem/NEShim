namespace NEShim.Audio;

/// <summary>
/// Abstraction over <see cref="AudioPlayer"/> for consumers that only need to push samples and
/// control playback — <see cref="GameLoop.FramePipeline"/>, <see cref="GameLoop.EmulationThread"/>,
/// and <see cref="NEShimApp"/>. Lets those crossings be exercised with <c>Substitute.For&lt;IAudioSink&gt;()</c>
/// in unit tests instead of a real SDL3 audio stream, mirroring the existing <c>IRenderCoordinator</c>
/// seam for the same class of hardware-boundary dependency.
/// </summary>
internal interface IAudioSink : IDisposable
{
    /// <summary>Starts audio output on the given SDL3 playback device (default device if empty).</summary>
    void Start(string deviceName = "");

    /// <summary>Called by the emulation thread after each frame advance.</summary>
    void Enqueue(short[] samples, int sampleCount);

    /// <summary>Pauses/resumes playback, draining the ring buffer and resetting filter state on pause.</summary>
    void SetPaused(bool paused);

    /// <summary>Sets the master volume, clamped to [0, 1].</summary>
    void SetVolume(float volume);

    /// <summary>Updates the 3-band EQ gains (dB, -12..+12 each). 0 = neutral.</summary>
    void SetEq(int bass, int mid, int treble);

    /// <summary>Swaps the active audio processor; the replacement's state is reset before use.</summary>
    void SetProcessor(IAudioProcessor processor);
}
