using NAudio.Wave;

namespace NEShim.Audio;

/// <summary>
/// Bridges the NES APU's sync audio output to a real-time audio device via NAudio.
///
/// Producer: emulation thread calls Enqueue() each frame.
/// Consumer: NAudio's driver thread calls Read() to pull samples.
///
/// A short[] ring buffer decouples the two threads.
/// Audio processing (filtering, volume) is delegated to an <see cref="IAudioProcessor"/>
/// that can be swapped at runtime without stopping the audio device.
/// When paused, Read() fills with silence.
/// </summary>
internal sealed class AudioPlayer : IWaveProvider, IDisposable
{
    // 44100 Hz, 16-bit, stereo — matches NES APU output (mono duplicated to stereo)
    public WaveFormat WaveFormat { get; } = new WaveFormat(44100, 16, 2);

    // Ring buffer: capacity = ~8 frames (~5880 stereo samples = 11760 shorts)
    private readonly short[] _ring;
    private readonly int _capacity;
    private int _writePos;
    private int _readPos;
    private int _available;
    private readonly object _ringLock = new();

    private volatile bool _paused;
    private IWavePlayer? _device;

    // Active processor — volatile so swaps from the UI thread are immediately visible
    // to the NAudio driver thread calling Read().
    private volatile IAudioProcessor _processor;

    // Master volume in [0, 1]. float reads on naturally-aligned .NET memory are atomic;
    // worst case is one call of Read() using a stale value, which is acceptable.
    private float _volume = 1.0f;

    public AudioPlayer(int bufferFrames = 3) : this(bufferFrames, new NesFilterProcessor()) { }

    public AudioPlayer(int bufferFrames, IAudioProcessor processor)
    {
        _capacity  = Math.Max(bufferFrames * 800 * 2, 4096);
        _ring      = new short[_capacity];
        _processor = processor;
    }

    /// <summary>Starts audio output on the default device.</summary>
    public void Start(string deviceName = "")
    {
        try
        {
            var device = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
            device.Init(this);
            device.Play();
            _device = device;
        }
        catch
        {
            // Fall back to WaveOut if WASAPI fails
            try
            {
                var device = new WaveOutEvent { DesiredLatency = 100 };
                device.Init(this);
                device.Play();
                _device = device;
            }
            catch
            {
                // No audio — silently continue
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
    }

    /// <summary>Sets the master volume. <paramref name="volume"/> is clamped to [0, 1].</summary>
    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
    }

    /// <summary>Called by the emulation thread after each FrameAdvance.</summary>
    public void Enqueue(short[] samples, int sampleCount)
    {
        // sampleCount is mono samples; samples[] is already interleaved stereo (L,R,L,R,...)
        int stereoSamples = sampleCount * 2; // already stereo pairs in the array
        if (stereoSamples > samples.Length) stereoSamples = samples.Length;

        lock (_ringLock)
        {
            for (int i = 0; i < stereoSamples; i++)
            {
                if (_available >= _capacity) break; // Drop oldest if overflow
                _ring[_writePos] = samples[i];
                _writePos = (_writePos + 1) % _capacity;
                _available++;
            }
        }
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
                while (i + 1 < shortCount && _available >= 2)
                {
                    short rawL = _ring[_readPos];
                    _readPos  = (_readPos + 1) % _capacity;
                    _available--;

                    // Discard paired R (identical to L for NES mono)
                    _readPos  = (_readPos + 1) % _capacity;
                    _available--;

                    var (filtL, filtR) = proc.Process(rawL);

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
            {
                _writePos = _readPos = _available = 0;
            }
            // Reset processor state so the first sample after resume starts clean.
            // Without this, a large DC offset in the filter memory would cause a pop.
            _processor.ResetState();
        }
    }

    public void Dispose()
    {
        _device?.Stop();
        _device?.Dispose();
    }
}
