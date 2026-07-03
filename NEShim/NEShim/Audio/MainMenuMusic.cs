using System.Diagnostics.CodeAnalysis;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace NEShim.Audio;

/// <summary>
/// Plays a looping audio file (MP3, WAV, or any NAudio-supported format) on the
/// pre-game main menu with smooth fade in / fade out transitions.
///
/// Fade in : 1.0 second  — starts at volume 0, ramps to master volume.
/// Fade out: 0.5 seconds — ramps to 0 then stops playback.
///
/// Volume is split into two independent concerns:
///   _fadeLevel   — 0–1 fade progress driven by the timer
///   _masterVolume — 0–1 user-controlled multiplier (SetMasterVolume)
///   _reader.Volume = _fadeLevel × _masterVolume at every tick
/// This separation ensures that changing master volume during a fade works correctly
/// and does not interfere with the fade state machine.
///
/// Looping is handled inside the sample provider so playback never needs to restart,
/// avoiding the WaveOut callback-thread re-entry issue.
///
/// Thread safety: fade ticks run on a timer thread; all public methods are safe to
/// call from the UI thread at any time.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MainMenuMusic : IDisposable
{
    private AudioFileReader?        _reader;
    private AudioFileReaderSource? _readerSource;
    private LoopingSampleProvider? _looper;
    private WasapiOut?            _output;
    private System.Timers.Timer?  _fadeTimer;
    private volatile bool         _disposed;

    // Fade state: _fadeLevel tracks 0→1 progress; _reader.Volume = _fadeLevel × _masterVolume
    private float   _fadeLevel;
    private float   _masterVolume = 1.0f;

    // Signed fade delta per tick — positive = fade in, negative = fade out
    private float   _volumeStep;
    private Action? _onFadeOutComplete;

    private const int   FadeTickMs     = 20;
    private const float FadeInSeconds  = 1.0f;
    private const float FadeOutSeconds = 0.5f;

    private static readonly float FadeInStep  = 1.0f / (FadeInSeconds  * 1000f / FadeTickMs);
    private static readonly float FadeOutStep = 1.0f / (FadeOutSeconds * 1000f / FadeTickMs);

    public MainMenuMusic(string filePath, bool autoStart = true)
    {
        try
        {
            _reader       = new AudioFileReader(filePath) { Volume = 0f };
            _readerSource = new AudioFileReaderSource(_reader);
            _looper       = new LoopingSampleProvider(_readerSource);
            _output  = new WasapiOut(AudioClientShareMode.Shared, 200);
            _output.Init(_looper);

            _fadeTimer = new System.Timers.Timer(FadeTickMs) { AutoReset = true };
            _fadeTimer.Elapsed += OnFadeTick;

            if (autoStart)
            {
                _output.Play();
                StartFadeIn();
            }
        }
        catch
        {
            // Clean up any partially-constructed resources before re-throwing
            // so CreateMainMenuMusic can catch and degrade gracefully
            DisposeResources();
            throw;
        }
    }

    // ---- Public API ----

    /// <summary>
    /// Restarts playback (seeking to start) and fades in.
    /// Safe to call when already playing — reverses any active fade out.
    /// </summary>
    public void FadeIn()
    {
        if (_disposed || _reader == null || _output == null) return;

        if (_output.PlaybackState != PlaybackState.Playing)
        {
            _reader.Position = 0;
            _fadeLevel       = 0f;
            _reader.Volume   = 0f;
            _output.Play();
        }

        StartFadeIn();
    }

    /// <summary>
    /// Fades volume to zero over 0.5 seconds then stops playback.
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
    /// Designed for Steam overlay open/close — does nothing if not currently playing.
    /// </summary>
    public void Pause()
    {
        if (_disposed || _output == null) return;
        _fadeTimer?.Stop();
        if (_output.PlaybackState == PlaybackState.Playing)
            _output.Pause();
    }

    /// <summary>
    /// Resumes playback from where <see cref="Pause"/> left off and fades back in.
    /// Does nothing if the output was not paused (e.g. music was never started or was stopped).
    /// </summary>
    public void Resume()
    {
        if (_disposed || _output == null) return;
        if (_output.PlaybackState == PlaybackState.Paused)
        {
            _output.Play();
            StartFadeIn();
        }
    }

    /// <summary>Stops playback immediately without fading.</summary>
    public void Stop()
    {
        if (_disposed) return;
        _fadeTimer?.Stop();
        _output?.Stop();
    }

    /// <summary>
    /// Sets the master volume multiplier (0–1).
    /// The current fade level is preserved; the audible output adjusts immediately.
    /// Changing master volume during a fade-in or fade-out works correctly because
    /// fade progress (<c>_fadeLevel</c>) is tracked independently of <c>_reader.Volume</c>.
    /// </summary>
    public void SetMasterVolume(float masterVolume)
    {
        _masterVolume = Math.Clamp(masterVolume, 0f, 1f);
        if (_disposed || _reader == null) return;
        _reader.Volume = _fadeLevel * _masterVolume;
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
        if (_disposed || _reader == null) return;

        float next = Math.Clamp(_fadeLevel + _volumeStep, 0f, 1f);
        _fadeLevel     = next;
        _reader.Volume = _fadeLevel * _masterVolume;

        bool reachedTarget = _volumeStep >= 0f ? next >= 1f : next <= 0f;
        if (!reachedTarget) return;

        _fadeTimer?.Stop();

        if (_volumeStep < 0f) // fade-out completed
        {
            _output?.Stop();
            var callback = _onFadeOutComplete;
            _onFadeOutComplete = null;
            callback?.Invoke();
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
        _fadeTimer?.Stop();
        _fadeTimer?.Dispose();
        _fadeTimer = null;

        _output?.Stop();
        _output?.Dispose();
        _output = null;

        // _looper and _readerSource hold no resources — _reader is the owner
        _looper       = null;
        _readerSource = null;

        _reader?.Dispose();
        _reader = null;
    }

}
