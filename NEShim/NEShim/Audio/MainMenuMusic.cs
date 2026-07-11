using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using SDL3;

namespace NEShim.Audio;

/// <summary>
/// Plays a looping WAV file on the main menu with smooth fade-in / fade-out transitions.
/// Uses SDL3 audio: SDL_LoadWAV decodes the file; a background thread streams chunks
/// into an SDL_AudioStream that the device consumes.
///
/// Fade in : 1.0 second  — ramps gain from 0 to master volume.
/// Fade out: 0.5 seconds — ramps gain to 0, then pauses the device.
///
/// Volume is split into two independent concerns:
///   _fadeLevel    — 0–1 fade progress driven by the timer
///   _masterVolume — 0–1 user-controlled multiplier (SetMasterVolume)
///   SDL stream gain = _fadeLevel × _masterVolume at every tick
///
/// Music file must be in WAV format (SDL_LoadWAV limitation). For playback of OGG or
/// other compressed formats, add an external decoder and feed raw PCM to the stream.
///
/// Thread safety: fade ticks run on a timer thread; stream pushes run on a dedicated
/// background thread. All public methods are safe to call from the UI thread.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MainMenuMusic : IDisposable
{
    private byte[]?                _audioData;
    private IntPtr                 _audioStream = IntPtr.Zero;
    private Thread?                _streamThread;
    private volatile bool          _stopStreaming;
    private volatile int           _streamPosition;
    private bool                   _isPlaying;
    private System.Timers.Timer?   _fadeTimer;
    private volatile bool          _disposed;

    private float   _fadeLevel;
    private float   _masterVolume = 1.0f;
    private float   _volumeStep;
    private Action? _onFadeOutComplete;

    private const int   FadeTickMs     = 20;
    private const float FadeInSeconds  = 1.0f;
    private const float FadeOutSeconds = 0.5f;

    private static readonly float FadeInStep  = 1.0f / (FadeInSeconds  * 1000f / FadeTickMs);
    private static readonly float FadeOutStep = 1.0f / (FadeOutSeconds * 1000f / FadeTickMs);

    private const int MusicChunkBytes       = 4096;
    private const int StreamQueueThreshold  = MusicChunkBytes * 8;

    // Pre-allocated streaming chunk buffer.
    private readonly byte[] _streamChunk = new byte[MusicChunkBytes];

    public MainMenuMusic(string filePath, bool autoStart = true)
    {
        try
        {
            if (!SDL.LoadWAV(filePath, out SDL.AudioSpec wavSpec, out IntPtr rawBuf, out uint rawLen))
                throw new InvalidOperationException($"SDL_LoadWAV failed: {SDL.GetError()}");

            _audioData = new byte[(int)rawLen];
            Marshal.Copy(rawBuf, _audioData, 0, (int)rawLen);
            SDL.Free(rawBuf);

            _audioStream = SDL.OpenAudioDeviceStream(
                SDL.AudioDeviceDefaultPlayback, in wavSpec, null, IntPtr.Zero);
            if (_audioStream == IntPtr.Zero)
                throw new InvalidOperationException($"SDL_OpenAudioDeviceStream failed: {SDL.GetError()}");

            SDL.SetAudioStreamGain(_audioStream, 0f);

            _fadeTimer = new System.Timers.Timer(FadeTickMs) { AutoReset = true };
            _fadeTimer.Elapsed += OnFadeTick;

            _streamThread = new Thread(StreamLoop)
            {
                IsBackground = true,
                Name         = "MusicStream",
            };
            _streamThread.Start();

            if (autoStart)
            {
                SDL.ResumeAudioStreamDevice(_audioStream);
                _isPlaying = true;
                StartFadeIn();
            }
        }
        catch
        {
            DisposeResources();
            throw;
        }
    }

    // ---- Public API ----

    /// <summary>
    /// Restarts playback from the beginning and fades in.
    /// Safe to call when already playing — reverses any active fade-out.
    /// </summary>
    public void FadeIn()
    {
        if (_disposed || _audioStream == IntPtr.Zero) return;

        if (!_isPlaying)
        {
            _streamPosition = 0;
            _fadeLevel      = 0f;
            SDL.SetAudioStreamGain(_audioStream, 0f);
            SDL.ClearAudioStream(_audioStream); // discard any stale queued data
            SDL.ResumeAudioStreamDevice(_audioStream);
            _isPlaying = true;
        }

        StartFadeIn();
    }

    /// <summary>
    /// Fades volume to zero over 0.5 seconds then pauses playback.
    /// <paramref name="onComplete"/> fires on the timer thread when the fade finishes.
    /// </summary>
    public void FadeOut(Action? onComplete = null)
    {
        if (_disposed) return;
        _onFadeOutComplete = onComplete;
        _volumeStep = -FadeOutStep;
        _fadeTimer?.Start();
    }

    /// <summary>
    /// Suspends playback without losing position or resetting the fade level.
    /// Call <see cref="Resume"/> to continue from the same point.
    /// </summary>
    public void Pause()
    {
        if (_disposed || _audioStream == IntPtr.Zero) return;
        _fadeTimer?.Stop();
        if (_isPlaying)
        {
            SDL.PauseAudioStreamDevice(_audioStream);
            _isPlaying = false;
        }
    }

    /// <summary>
    /// Resumes playback from where <see cref="Pause"/> left off and fades back in.
    /// Does nothing if the output was not paused.
    /// </summary>
    public void Resume()
    {
        if (_disposed || _audioStream == IntPtr.Zero) return;
        if (!_isPlaying)
        {
            SDL.ResumeAudioStreamDevice(_audioStream);
            _isPlaying = true;
            StartFadeIn();
        }
    }

    /// <summary>Stops playback immediately without fading.</summary>
    public void Stop()
    {
        if (_disposed) return;
        _fadeTimer?.Stop();
        if (_audioStream != IntPtr.Zero) SDL.PauseAudioStreamDevice(_audioStream);
        _isPlaying = false;
    }

    /// <summary>
    /// Sets the master volume multiplier (0–1).
    /// The current fade level is preserved; audible output adjusts immediately.
    /// </summary>
    public void SetMasterVolume(float masterVolume)
    {
        _masterVolume = Math.Clamp(masterVolume, 0f, 1f);
        if (_disposed || _audioStream == IntPtr.Zero) return;
        SDL.SetAudioStreamGain(_audioStream, _fadeLevel * _masterVolume);
    }

    // ---- Internal ----

    private void StartFadeIn()
    {
        _onFadeOutComplete = null;
        _volumeStep = FadeInStep;
        _fadeTimer?.Start();
    }

    private void OnFadeTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_disposed || _audioStream == IntPtr.Zero) return;

        float next = Math.Clamp(_fadeLevel + _volumeStep, 0f, 1f);
        _fadeLevel = next;
        SDL.SetAudioStreamGain(_audioStream, _fadeLevel * _masterVolume);

        bool reachedTarget = _volumeStep >= 0f ? next >= 1f : next <= 0f;
        if (!reachedTarget) return;

        _fadeTimer?.Stop();

        if (_volumeStep < 0f) // fade-out completed
        {
            SDL.PauseAudioStreamDevice(_audioStream);
            _isPlaying = false;
            var callback = _onFadeOutComplete;
            _onFadeOutComplete = null;
            callback?.Invoke();
        }
    }

    // Background thread: pushes looping WAV chunks into the SDL audio stream.
    private void StreamLoop()
    {
        while (!_stopStreaming)
        {
            IntPtr stream = _audioStream;
            byte[]? data  = _audioData;

            if (stream == IntPtr.Zero || data == null || data.Length == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            int queued = SDL.GetAudioStreamQueued(stream);
            if (queued > StreamQueueThreshold)
            {
                Thread.Sleep(5);
                continue;
            }

            int from      = _streamPosition;
            int available = data.Length - from;
            int toPush    = Math.Min(available, MusicChunkBytes);

            if (toPush > 0)
            {
                Buffer.BlockCopy(data, from, _streamChunk, 0, toPush);
                SDL.PutAudioStreamData(stream, _streamChunk, toPush);
                int newPos    = from + toPush;
                _streamPosition = newPos >= data.Length ? 0 : newPos;
            }
            else
            {
                _streamPosition = 0;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeResources();
    }

    private void DisposeResources()
    {
        _stopStreaming = true;
        _streamThread?.Join(millisecondsTimeout: 100);
        _streamThread = null;

        _fadeTimer?.Stop();
        _fadeTimer?.Dispose();
        _fadeTimer = null;

        if (_audioStream != IntPtr.Zero)
        {
            SDL.DestroyAudioStream(_audioStream);
            _audioStream = IntPtr.Zero;
        }

        _audioData = null;
    }
}
