using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Plays back a decoded image as a sequence of frames, advancing based on wall-clock elapsed
/// time — <see cref="CurrentFrame"/> is computed fresh on every access, no external per-frame
/// <c>Update(dt)</c> call needed, mirroring <c>LogoScreen.CurrentAlpha</c>. Backed by an
/// animated format (GIF/WEBP/APNG, via <see cref="SdlSurfaceLoader.LoadAnimationFromFile"/>)
/// when possible, falling back to a single static frame
/// (<see cref="SdlSurfaceLoader.LoadFromFile"/>) otherwise, so callers get one uniform
/// interface regardless of source format.
/// </summary>
internal sealed class AnimatedImagePlayer : IDisposable
{
    private readonly IReadOnlyList<IntPtr> _frames;
    private readonly IReadOnlyList<int> _delaysMs;
    private readonly IntPtr _nativeAnimation; // Zero when loaded via the static-image fallback
    private readonly long _startTicks;

    internal bool IsAnimated => _frames.Count > 1;
    internal IntPtr CurrentFrame => _frames[ComputeFrameIndex(Environment.TickCount64 - _startTicks, _delaysMs)];

    private AnimatedImagePlayer(IReadOnlyList<IntPtr> frames, IReadOnlyList<int> delaysMs, IntPtr nativeAnimation)
    {
        _frames = frames;
        _delaysMs = delaysMs;
        _nativeAnimation = nativeAnimation;
        _startTicks = Environment.TickCount64;
    }

    /// <summary>Returns null when the file can't be decoded at all (neither as an animation nor a static image).</summary>
    internal static AnimatedImagePlayer? LoadFromFile(string path)
    {
        var animation = SdlSurfaceLoader.LoadAnimationFromFile(path);
        if (animation is { } a)
            return new AnimatedImagePlayer(a.Frames, a.DelaysMs, a.NativeAnimation);

        IntPtr single = SdlSurfaceLoader.LoadFromFile(path);
        if (single == IntPtr.Zero) return null;
        return new AnimatedImagePlayer(new[] { single }, new[] { 0 }, IntPtr.Zero);
    }

    /// <summary>
    /// Pure and SDL-free: picks the frame active at <paramref name="elapsedMs"/>, looping over
    /// the animation's total duration. Zero/negative delay entries are floored to 1ms so a
    /// malformed animation can't stall the loop or divide by zero.
    /// </summary>
    internal static int ComputeFrameIndex(long elapsedMs, IReadOnlyList<int> delaysMs)
    {
        if (delaysMs.Count <= 1) return 0;

        int total = 0;
        foreach (var d in delaysMs) total += Math.Max(d, 1);

        int t = (int)(elapsedMs % total);
        int acc = 0;
        for (int i = 0; i < delaysMs.Count; i++)
        {
            acc += Math.Max(delaysMs[i], 1);
            if (t < acc) return i;
        }
        return delaysMs.Count - 1;
    }

    public void Dispose()
    {
        if (_nativeAnimation != IntPtr.Zero)
            SdlSurfaceLoader.FreeAnimation(_nativeAnimation);
        else
            foreach (var f in _frames) SDL.DestroySurface(f);
    }
}
