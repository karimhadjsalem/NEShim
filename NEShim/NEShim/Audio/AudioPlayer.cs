using NAudio.Wave;

namespace NEShim.Audio;

/// <summary>
/// Bridges the NES APU's sync audio output to a real-time audio device via NAudio.
///
/// Producer: emulation thread calls Enqueue() each frame.
/// Consumer: NAudio's driver thread calls Read() to pull samples.
///
/// A short[] ring buffer decouples the two threads.
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

    // ---- NES hardware output RC filter chain (NTSC) ----
    // The real NES has three analog filters on its audio output line.
    // Applied in Read() to every L-channel mono sample; result is duplicated to R.
    // Source: https://www.nesdev.org/wiki/APU_Mixer#Emulation
    private const float HpAlpha1 = 0.994742f; // e^(-2π × 37   / 44100)  high-pass ~37 Hz
    private const float HpAlpha2 = 0.994462f; // e^(-2π × 39   / 44100)  high-pass ~39 Hz
    private const float LpBeta   = 0.136224f; // e^(-2π × 14000 / 44100)  low-pass ~14 kHz

    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lpOut;

    public AudioPlayer(int bufferFrames = 3)
    {
        // bufferFrames × ~735 samples/frame × 2 channels × some headroom
        _capacity = Math.Max(bufferFrames * 800 * 2, 4096);
        _ring = new short[_capacity];
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
            lock (_ringLock)
            {
                while (i < shortCount && _available > 0)
                {
                    short s = _ring[_readPos];
                    _readPos = (_readPos + 1) % _capacity;
                    _available--;

                    // Apply NES hardware output filters to L-channel (even index) only.
                    // Ring buffer holds stereo pairs (L,R) where L == R (NES is mono).
                    // Advancing the filter state on the L sample and reusing its output for R
                    // keeps both channels in sync and avoids double-advancing the filter memory.
                    short filtered;
                    if (i % 2 == 0)
                    {
                        filtered = RunOutputFilters(s);
                    }
                    else
                    {
                        // R channel: output the same filtered value produced for L.
                        filtered = (short)Math.Clamp((int)_lpOut, short.MinValue, short.MaxValue);
                    }

                    buffer[offset + i * 2]     = (byte)(filtered & 0xFF);
                    buffer[offset + i * 2 + 1] = (byte)((filtered >> 8) & 0xFF);
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
            // Reset filter state so the first sample after resume starts clean.
            // Without this, a large DC offset in the filter memory would cause a pop.
            _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = 0f;
        }
    }

    public void Dispose()
    {
        _device?.Stop();
        _device?.Dispose();
    }

    // ---- RC filter chain ----

    /// <summary>
    /// Applies the three-stage NES hardware output filter to one mono sample.
    /// Call only for L-channel samples; reuse <see cref="_lpOut"/> for the paired R channel.
    /// </summary>
    private short RunOutputFilters(short s)
    {
        float x = s;

        // High-pass 1 (~37 Hz): y[n] = α*(y[n-1] + x[n] - x[n-1])
        float hp1 = HpAlpha1 * (_hp1Out + x - _hp1In);
        _hp1In  = x;
        _hp1Out = hp1;
        x       = hp1;

        // High-pass 2 (~39 Hz)
        float hp2 = HpAlpha2 * (_hp2Out + x - _hp2In);
        _hp2In  = x;
        _hp2Out = hp2;
        x       = hp2;

        // Low-pass (~14 kHz): y[n] = β*y[n-1] + (1-β)*x[n]
        float lp = LpBeta * _lpOut + (1f - LpBeta) * x;
        _lpOut = lp;

        return (short)Math.Clamp((int)lp, short.MinValue, short.MaxValue);
    }
}
