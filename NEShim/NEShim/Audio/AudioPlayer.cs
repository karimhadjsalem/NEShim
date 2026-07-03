using NAudio.Wave;

namespace NEShim.Audio;

/// <summary>
/// Bridges the NES APU's sync audio output to a real-time audio device via NAudio.
///
/// Producer: emulation thread calls Enqueue() each frame.
/// Consumer: NAudio's driver thread calls Read() to pull samples.
///
/// An <see cref="AudioRingBuffer"/> decouples the two threads.
/// Audio processing (filtering, volume) is delegated to an <see cref="IAudioProcessor"/>
/// that can be swapped at runtime without stopping the audio device.
/// When paused, Read() fills with silence.
/// </summary>
internal sealed class AudioPlayer : IWaveProvider, IDisposable
{
    // 44100 Hz, 16-bit, stereo — matches NES APU output (mono duplicated to stereo)
    public WaveFormat WaveFormat { get; } = new WaveFormat(44100, 16, 2);

    private readonly AudioRingBuffer _ringBuffer;
    private readonly object          _ringLock = new();

    private volatile bool _paused;
    private IWavePlayer? _device;

    // Active processor — volatile so swaps from the UI thread are immediately visible
    // to the NAudio driver thread calling Read().
    private volatile IAudioProcessor _processor;

    // 3-band EQ applied after the processor. Allocated once and reused; gains are
    // updated via SetEq(). Reads from the NAudio thread are safe: SetGains only
    // updates float fields, which are atomic on naturally-aligned .NET memory.
    private readonly AudioEqProcessor _eq = new();

    // Master volume in [0, 1]. float reads on naturally-aligned .NET memory are atomic;
    // worst case is one call of Read() using a stale value, which is acceptable.
    private float _volume = 1.0f;

    public AudioPlayer(int bufferFrames = 3) : this(bufferFrames, new NesFilterProcessor()) { }

    public AudioPlayer(int bufferFrames, IAudioProcessor processor)
    {
        _ringBuffer = new AudioRingBuffer(Math.Max(bufferFrames * 800 * 2, 4096));
        _processor  = processor;
    }

    /// <summary>
    /// Starts audio output. <paramref name="deviceName"/> is accepted but ignored —
    /// the current implementation always opens the default WASAPI device.
    /// </summary>
    public void Start(string deviceName = "")
    {
        try
        {
            var device = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
            device.Init(this);
            device.Play();
            _device = device;
            Logger.Log("[Audio] WASAPI device started.");
        }
        catch (Exception wasapiEx)
        {
            Logger.Log($"[Audio] WASAPI failed ({wasapiEx.Message}) — falling back to WaveOut.");
            try
            {
                var device = new WaveOutEvent { DesiredLatency = 100 };
                device.Init(this);
                device.Play();
                _device = device;
                Logger.Log("[Audio] WaveOut device started.");
            }
            catch (Exception waveOutEx)
            {
                Logger.Log($"[Audio] WaveOut also failed ({waveOutEx.Message}) — running silent.");
            }
        }
    }

    /// <summary>
    /// Swaps the active audio processor. The replacement's state is reset to zero
    /// to avoid a pop caused by residual filter memory from the previous processor.
    /// Safe to call from any thread while audio is playing.
    /// </summary>
    public void SetProcessor(IAudioProcessor processor)
    {
        processor.ResetState();
        _processor = processor;
        Logger.Log($"[Audio] Processor switched to {processor.GetType().Name}.");
    }

    /// <summary>Sets the master volume. <paramref name="volume"/> is clamped to [0, 1].</summary>
    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
    }

    /// <summary>Updates the 3-band EQ gains (dB, −12..+12 each). 0 = neutral.</summary>
    public void SetEq(int bass, int mid, int treble)
    {
        _eq.SetGains(bass, mid, treble);
    }

    /// <summary>Called by the emulation thread after each FrameAdvance.</summary>
    public void Enqueue(short[] samples, int sampleCount)
    {
        // sampleCount is mono samples; samples[] is already interleaved stereo (L,R,L,R,...)
        int stereoSamples = sampleCount * 2; // already stereo pairs in the array
        if (stereoSamples > samples.Length) stereoSamples = samples.Length;

        lock (_ringLock)
            _ringBuffer.Enqueue(samples, stereoSamples);
    }

    /// <summary>Called by NAudio's driver thread.</summary>
    public int Read(byte[] buffer, int offset, int count)
    {
        int shortCount = count / 2; // count is in bytes; each sample is 2 bytes
        int i = 0;

        if (!_paused)
        {
            // Capture both references once; swaps mid-call take effect on the next call.
            IAudioProcessor proc = _processor;
            float vol = _volume;

            lock (_ringLock)
            {
                // Consume stereo pairs (L, R) from the ring.
                // NES is mono so L == R in the ring; we read L, discard R,
                // and let the processor produce the filtered (L, R) output pair.
                while (i + 1 < shortCount && _ringBuffer.TryDequeueMonoL(out short rawL))
                {
                    var (filtL, filtR) = proc.Process(rawL);

                    if (_eq.IsActive)
                        (filtL, filtR) = _eq.Process(filtL, filtR);

                    short outL = (short)Math.Clamp((int)(filtL * vol), short.MinValue, short.MaxValue);
                    short outR = (short)Math.Clamp((int)(filtR * vol), short.MinValue, short.MaxValue);

                    buffer[offset + i * 2]     = (byte)(outL & 0xFF);
                    buffer[offset + i * 2 + 1] = (byte)((outL >> 8) & 0xFF);
                    i++;

                    buffer[offset + i * 2]     = (byte)(outR & 0xFF);
                    buffer[offset + i * 2 + 1] = (byte)((outR >> 8) & 0xFF);
                    i++;
                }
            }
        }

        // Fill remainder with silence
        while (i < shortCount)
        {
            buffer[offset + i * 2]     = 0;
            buffer[offset + i * 2 + 1] = 0;
            i++;
        }

        return count;
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused)
        {
            // Drain buffer so we don't play stale audio on resume
            lock (_ringLock)
                _ringBuffer.Drain();

            // Reset processor state so the first sample after resume starts clean.
            // Without this, a large DC offset in the filter memory would cause a pop.
            _processor.ResetState();
            _eq.ResetState();
        }
    }

    public void Dispose()
    {
        _device?.Stop();
        _device?.Dispose();
    }
}
