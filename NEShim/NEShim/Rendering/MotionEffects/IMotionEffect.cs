namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// Strategy interface for per-frame screen-space motion effects applied to the NES frame quad.
/// Implementations return a clip-space (dx, dy) offset that D3D11Renderer adds to all four
/// quad corner positions before drawing. A zero offset is a no-op.
/// </summary>
internal interface IMotionEffect
{
    VideoMotionEffectMode EffectMode { get; }

    /// <summary>
    /// Returns the clip-space offset to apply to the NES frame quad this frame.
    /// <paramref name="frameCount"/> is the monotonically increasing draw-frame counter
    /// maintained by D3D11Renderer.
    /// </summary>
    (float Dx, float Dy) GetFrameOffset(long frameCount);

    /// <summary>
    /// Called by D3D11Renderer whenever the viewport or letterbox dimensions change so that
    /// effects that scale with screen size can recalibrate. Default implementation is a no-op.
    /// </summary>
    void NotifyLayout(int viewportWidth, int viewportHeight, int letterboxHeight) { }
}
