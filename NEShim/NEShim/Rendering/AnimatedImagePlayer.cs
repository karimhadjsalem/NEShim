using System.Linq;
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
    private readonly Action<IntPtr>? _onSurfaceDisposing;
    private readonly long _startTicks;

    internal bool IsAnimated => _frames.Count > 1;
    internal IntPtr CurrentFrame => _frames[ComputeFrameIndex(Environment.TickCount64 - _startTicks, _delaysMs)];

    private AnimatedImagePlayer(IReadOnlyList<IntPtr> frames, IReadOnlyList<int> delaysMs, IntPtr nativeAnimation,
        Action<IntPtr>? onSurfaceDisposing)
    {
        _frames = frames;
        _delaysMs = delaysMs;
        _nativeAnimation = nativeAnimation;
        _onSurfaceDisposing = onSurfaceDisposing;
        _startTicks = Environment.TickCount64;
    }

    /// <summary>
    /// Returns null when the file can't be decoded at all (neither as an animation nor a static
    /// image). <paramref name="onSurfaceDisposing"/> is called for every frame surface right
    /// before it becomes invalid (see Dispose) — forward IFrameRenderer.InvalidateSurfaceTexture
    /// so the overlay paint context's cached GPU texture for that frame is evicted too.
    /// </summary>
    /// <param name="maxWidth">Optional cap, paired with <paramref name="maxHeight"/> (both
    /// required to take effect): every decoded frame larger than this is downscaled once at load
    /// time, never upscaled. Bounds per-frame re-upload cost for a large source asset — this
    /// player's <see cref="CurrentFrame"/> is re-uploaded to the GPU on every draw call rather
    /// than cached (see <c>SDL3PaintContext.BlitSurfaceUncached</c>), since a frame pointer here
    /// can legitimately mean different pixel content at different points in playback and a
    /// persistent cache would freeze the first upload in place.</param>
    /// <param name="maxHeight">See <paramref name="maxWidth"/>.</param>
    internal static AnimatedImagePlayer? LoadFromFile(
        string path, Action<IntPtr>? onSurfaceDisposing = null, int? maxWidth = null, int? maxHeight = null)
    {
        var animation = SdlSurfaceLoader.LoadAnimationFromFile(path);
        if (animation is { } a)
        {
            if (maxWidth is int mw && maxHeight is int mh && AnyFrameExceedsCap(a.Frames, mw, mh) &&
                TryDownscaleAllFrames(a.Frames, mw, mh, out IntPtr[]? downscaled))
            {
                SdlSurfaceLoader.FreeAnimation(a.NativeAnimation);
                return new AnimatedImagePlayer(downscaled!, a.DelaysMs, IntPtr.Zero, onSurfaceDisposing);
            }
            return new AnimatedImagePlayer(a.Frames, a.DelaysMs, a.NativeAnimation, onSurfaceDisposing);
        }

        IntPtr single = SdlSurfaceLoader.LoadFromFile(path);
        if (single == IntPtr.Zero) return null;
        return new AnimatedImagePlayer(new[] { single }, new[] { 0 }, IntPtr.Zero, onSurfaceDisposing);
    }

    /// <summary>Pure and SDL-free: true when at least one frame exceeds the cap and downscaling is worth doing.</summary>
    internal static bool NeedsDownscale(IReadOnlyList<(int width, int height)> frameSizes, int maxWidth, int maxHeight) =>
        frameSizes.Any(s => s.width > maxWidth || s.height > maxHeight);

    private static bool AnyFrameExceedsCap(IReadOnlyList<IntPtr> frames, int maxWidth, int maxHeight) =>
        NeedsDownscale(frames.Select(SDL3PaintContext.GetSurfaceSize).ToList(), maxWidth, maxHeight);

    /// <summary>
    /// Contain-fits source dimensions within maxWidth/maxHeight, preserving aspect ratio — never
    /// upscales. Same formula as <c>GameCarouselScreen.ComputeThumbnailSize</c>, kept as an
    /// independent copy rather than a shared helper since the two callers own unrelated surface
    /// lifetimes (this one must free the native animation handle it reads from; thumbnails don't).
    /// </summary>
    internal static (int width, int height) ComputeDownscaledSize(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
    {
        if (sourceWidth <= maxWidth && sourceHeight <= maxHeight) return (sourceWidth, sourceHeight);

        float scale = Math.Min((float)maxWidth / sourceWidth, (float)maxHeight / sourceHeight);
        return (Math.Max(1, (int)(sourceWidth * scale)), Math.Max(1, (int)(sourceHeight * scale)));
    }

    /// <summary>
    /// Builds an independently-owned, downscaled copy of every frame (a fresh 1:1 copy even for
    /// a frame that didn't need scaling) so the whole set is safe to individually
    /// <c>SDL.DestroySurface</c> once the caller frees the native animation they came from. If
    /// any single allocation fails partway through, every copy made so far is torn back down and
    /// this returns false — the caller falls back to the original, native-owned (uncapped)
    /// frames rather than risk a dangling pointer once the native animation is freed.
    /// </summary>
    private static bool TryDownscaleAllFrames(IReadOnlyList<IntPtr> frames, int maxWidth, int maxHeight, out IntPtr[]? downscaled)
    {
        var result = new IntPtr[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            result[i] = DownscaleFrame(frames[i], maxWidth, maxHeight);
            if (result[i] != IntPtr.Zero) continue;

            for (int j = 0; j < i; j++) SDL.DestroySurface(result[j]);
            Logger.Log("[AnimatedImagePlayer] Downscale allocation failed — using original resolution.");
            downscaled = null;
            return false;
        }
        downscaled = result;
        return true;
    }

    /// <summary>Returns IntPtr.Zero on allocation failure — never falls back to the original surface, which the caller is about to free.</summary>
    private static IntPtr DownscaleFrame(IntPtr frame, int maxWidth, int maxHeight)
    {
        var (width, height) = SDL3PaintContext.GetSurfaceSize(frame);
        var (scaledWidth, scaledHeight) = ComputeDownscaledSize(width, height, maxWidth, maxHeight);

        IntPtr copy = SDL.CreateSurface(scaledWidth, scaledHeight, SDL.PixelFormat.ARGB8888);
        if (copy == IntPtr.Zero) return IntPtr.Zero;

        var destRect = new SDL.Rect { X = 0, Y = 0, W = scaledWidth, H = scaledHeight };
        SDL.BlitSurfaceScaled(frame, IntPtr.Zero, copy, in destRect, SDL.ScaleMode.Linear);
        return copy;
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
        if (_onSurfaceDisposing is not null)
            foreach (var f in _frames) _onSurfaceDisposing(f);

        if (_nativeAnimation != IntPtr.Zero)
            SdlSurfaceLoader.FreeAnimation(_nativeAnimation);
        else
            foreach (var f in _frames) SDL.DestroySurface(f);
    }
}
