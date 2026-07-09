using System.Runtime.InteropServices;
using SDL3;

namespace NEShim.Audio;

/// <summary>
/// Bridges the NES APU's sync audio output to a real-time audio device via SDL3.
///
/// Producer: emulation thread calls Enqueue() each frame.
/// Consumer: SDL3 audio thread fires OnAudioGetCallback, which calls Read().
///
/// An <see cref="AudioRingBuffer"/> decouples the two threads.
/// Audio processing (filtering, volume) is delegated to an <see cref="IAudioProcessor"/>
/// that can be swapped at runtime without stopping the audio device.
/// When paused, Read() fills with silence.
/// </summary>
internal sealed class AudioPlayer : IDisposable
{
    private const int SampleRate      = 44100;
    private const int ChannelCount    = 2;
    private const int BitsPerSampleCount = 16;

    // Replaces NAudio.Wave.WaveFormat for unit-test compatibility.
    internal readonly struct OutputFormat
    {
        public int SampleRate    { get; } = AudioPlayer.SampleRate;
        public int BitsPerSample { get; } = BitsPerSampleCount;
        public int Channels      { get; } = ChannelCount;
        public OutputFormat() { }
    }

    public OutputFormat WaveFormat { get; } = new();

    private readonly AudioRingBuffer  _ringBuffer;
    private readonly object           _ringLock = new();
    private volatile bool             _paused;
    private IntPtr                    _audioStream = IntPtr.Zero;
    private volatile IAudioProcessor  _processor;
    private readonly AudioEqProcessor _eq = new();
    private float                     _volume = 1.0f;

    // Pre-allocated callback buffer; grows on demand if SDL ever requests a larger chunk.
    private byte[]                    _callbackBuffer = new byte[8192];

    // Held as a field to prevent the delegate from being collected during playback.
    private SDL.AudioStreamCallback?  _getCallbackDelegate;

    public AudioPlayer(int bufferFrames = 3) : this(bufferFrames, new NesFilterProcessor()) { }

    public AudioPlayer(int bufferFrames, IAudioProcessor processor)
    {
        _ringBuffer = new AudioRingBuffer(Math.Max(bufferFrames * 800 * 2, 4096));
        _processor  = processor;
    }

    /// <summary>Starts audio output on the default SDL3 playback device.</summary>
    public void Start(string deviceName = "")
    {
        _getCallbackDelegate = OnAudioGetCallback;
        var spec = new SDL.AudioSpec
        {
            Format   = SDL.AudioFormat.AudioS16LE,
            Channels = ChannelCount,
            Freq     = SampleRate,
        };
        _audioStream = SDL.OpenAudioDeviceStream(
            SDL.AudioDeviceDefaultPlayback, ref spec, _getCallbackDelegate, IntPtr.Zero);
        if (_audioStream == IntPtr.Zero)
        {
            Logger.Log($"[Audio] SDL_OpenAudioDeviceStream failed: {SDL.GetError()}");
            return;
        }
        SDL.ResumeAudioStreamDevice(_audioStream);
        Logger.Log("[Audio] SDL3 audio device started.");
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
        int stereoSamples = sampleCount * 2;
        if (stereoSamples > samples.Length) stereoSamples = samples.Length;
        lock (_ringLock)
            _ringBuffer.Enqueue(samples, stereoSamples);
    }

    // Fired by SDL3 audio thread when the stream needs more data.
    private void OnAudioGetCallback(IntPtr userdata, IntPtr stream, int additionalAmount, int totalAmount)
    {
        if (additionalAmount <= 0) return;
        if (_callbackBuffer.Length < additionalAmount)
            _callbackBuffer = new byte[additionalAmount];
        Read(_callbackBuffer, 0, additionalAmount);
        SDL.PutAudioStreamData(stream, _callbackBuffer, additionalAmount);
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with processed audio from the ring buffer.
    /// Exposed as internal for unit tests; also called by the SDL3 audio callback.
    /// </summary>
    internal int Read(byte[] buffer, int offset, int count)
    {
        int shortCount = count / 2;
        int i = 0;

        if (!_paused)
        {
            // Capture both references once; swaps mid-call take effect on the next call.
            IAudioProcessor proc = _processor;
            float vol = _volume;

            lock (_ringLock)
            {
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
            if (_audioStream != IntPtr.Zero) SDL.PauseAudioStreamDevice(_audioStream);

            lock (_ringLock)
                _ringBuffer.Drain();

            _processor.ResetState();
            _eq.ResetState();
        }
        else
        {
            if (_audioStream != IntPtr.Zero) SDL.ResumeAudioStreamDevice(_audioStream);
        }
    }

    public void Dispose()
    {
        if (_audioStream == IntPtr.Zero) return;
        SDL.DestroyAudioStream(_audioStream);
        _audioStream = IntPtr.Zero;
    }
}
