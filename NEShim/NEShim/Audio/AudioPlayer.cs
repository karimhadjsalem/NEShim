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

                    buffer[offset + i * 2]     = (byte)(s & 0xFF);
                    buffer[offset + i * 2 + 1] = (byte)((s >> 8) & 0xFF);
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
        }
    }

    public void Dispose()
    {
        _device?.Stop();
        _device?.Dispose();
    }
}
