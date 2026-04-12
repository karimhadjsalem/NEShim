using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace NEShim.Audio;

/// <summary>
/// Plays a looping audio file (MP3, WAV, or any NAudio-supported format) on the
/// pre-game main menu with smooth fade in / fade out transitions.
///
/// Fade in : 1.0 second  — starts at volume 0, ramps to 1.
/// Fade out: 0.5 seconds — ramps to 0 then stops playback.
///
/// Looping is handled inside the sample provider so playback never needs to restart,
/// avoiding the WaveOut callback-thread re-entry issue.
///
/// Thread safety: fade ticks run on a timer thread; all public methods are safe to
/// call from the UI thread at any time.
/// </summary>
internal sealed class MainMenuMusic : IDisposable
{
    private AudioFileReader?     _reader;
    private LoopingSampleProvider? _looper;
    private WasapiOut?           _output;
    private System.Timers.Timer? _fadeTimer;
    private volatile bool        _disposed;

    // Signed volume delta per tick — positive = fade in, negative = fade out
    private float   _volumeStep;
    private Action? _onFadeOutComplete;

    private const int   FadeTickMs     = 20;
    private const float FadeInSeconds  = 1.0f;
    private const float FadeOutSeconds = 0.5f;

    private static readonly float FadeInStep  = 1.0f / (FadeInSeconds  * 1000f / FadeTickMs);
    private static readonly float FadeOutStep = 1.0f / (FadeOutSeconds * 1000f / FadeTickMs);

    public MainMenuMusic(string filePath)
    {
        try
        {
            _reader  = new AudioFileReader(filePath) { Volume = 0f };
            _looper  = new LoopingSampleProvider(_reader);
            _output  = new WasapiOut(AudioClientShareMode.Shared, 200);
            _output.Init(_looper);
            _output.Play();

            _fadeTimer = new System.Timers.Timer(FadeTickMs) { AutoReset = true };
            _fadeTimer.Elapsed += OnFadeTick;
            StartFadeIn();
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

    /// <summary>Stops playback immediately without fading.</summary>
    public void Stop()
    {
        if (_disposed) return;
        _fadeTimer?.Stop();
        _output?.Stop();
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

        float next = Math.Clamp(_reader.Volume + _volumeStep, 0f, 1f);
        _reader.Volume = next;

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

        // _looper holds no resources — _reader is the owner
        _looper = null;

        _reader?.Dispose();
        _reader = null;
    }

    // ---- Inner type ----

    /// <summary>
    /// Wraps an <see cref="AudioFileReader"/> and loops it infinitely by seeking
    /// back to the start each time the source is exhausted.  Looping at the sample
    /// level avoids calling Play() from a WaveOut callback thread.
    /// </summary>
    private sealed class LoopingSampleProvider : ISampleProvider
    {
        private readonly AudioFileReader _source;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public LoopingSampleProvider(AudioFileReader source) => _source = source;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = _source.Read(buffer, offset + totalRead, count - totalRead);
                if (read > 0)
                {
                    totalRead += read;
                }
                else
                {
                    // End of file — seek to start and loop
                    if (_source.Length == 0) break; // guard against empty file
                    _source.Position = 0;
                }
            }
            return totalRead;
        }
    }
}
