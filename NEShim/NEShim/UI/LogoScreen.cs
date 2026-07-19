using SDL3;

namespace NEShim.UI;

internal sealed class LogoScreen : IDisposable
{
    // Recommended: 0.5s fade in, 2.0s hold, 0.5s fade out = 3.0s total.
    // Alternative with slower fade-out: FadeInSeconds=0.5f, HoldSeconds=1.5f, FadeOutSeconds=1.0f.
    private const float FadeInSeconds  = 0.5f;
    private const float HoldSeconds    = 2.0f;
    private const float FadeOutSeconds = 0.5f;

    private static readonly float TotalSeconds = FadeInSeconds + HoldSeconds + FadeOutSeconds;

    private DateTime? _startTime;
    private readonly Action<IntPtr>? _onSurfaceDisposing;

    public IntPtr Image { get; }

    /// <param name="onSurfaceDisposing">
    /// Called with <see cref="Image"/> right before it's destroyed (see <see cref="Dispose"/>) —
    /// the owner must forward this to <c>IFrameRenderer.InvalidateSurfaceTexture</c> so the
    /// overlay paint context's cached GPU texture for this surface is evicted too, before a
    /// later, unrelated surface can be allocated at the same (recycled) address and silently
    /// inherit this texture's stale pixel data. Optional so tests that never touch SDL rendering
    /// can omit it.
    /// </param>
    public LogoScreen(IntPtr image, Action<IntPtr>? onSurfaceDisposing = null)
    {
        Image = image;
        _onSurfaceDisposing = onSurfaceDisposing;
    }

    private float ElapsedSeconds
    {
        get
        {
            _startTime ??= DateTime.UtcNow;
            return (float)(DateTime.UtcNow - _startTime.Value).TotalSeconds;
        }
    }

    public bool  IsComplete   => ElapsedSeconds >= TotalSeconds;
    public float CurrentAlpha => ComputeAlpha(ElapsedSeconds, FadeInSeconds, HoldSeconds, FadeOutSeconds);

    internal static float ComputeAlpha(float elapsedSeconds, float fadeIn, float hold, float fadeOut)
    {
        if (elapsedSeconds <= 0f)           return 0f;
        if (elapsedSeconds < fadeIn)        return elapsedSeconds / fadeIn;
        if (elapsedSeconds < fadeIn + hold) return 1f;
        float fadeElapsed = elapsedSeconds - fadeIn - hold;
        return fadeElapsed >= fadeOut ? 0f : 1f - fadeElapsed / fadeOut;
    }

    public void Dispose()
    {
        if (Image != IntPtr.Zero)
        {
            _onSurfaceDisposing?.Invoke(Image);
            SDL.DestroySurface(Image);
        }
    }
}
