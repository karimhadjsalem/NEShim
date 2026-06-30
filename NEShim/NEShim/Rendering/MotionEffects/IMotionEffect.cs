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

    /// <summary>
    /// Optional embedded resource name of a pixel shader (.cso) to swap in for the NES
    /// frame quad draw call. When non-null, D3D11Renderer temporarily binds this shader
    /// instead of the structural filter shader and calls <see cref="WriteShaderParams"/>
    /// to populate the constant buffer, restoring the structural shader immediately after.
    /// Null (default) means no shader override — the active structural filter is used.
    /// </summary>
    string? PixelShaderResourceName => null;

    /// <summary>
    /// Whether this motion effect's pixel shader requires bilinear (linear) sampling.
    /// Defaults to false (point sampler). Override to true for effects that warp UVs to
    /// sub-texel positions and need smooth interpolation.
    /// </summary>
    bool UseLinearSampler => false;

    /// <summary>
    /// Populates slots [0..2] of the shared 4-float constant buffer when this effect's
    /// pixel shader is active. Slot [3] (colorMode) is always written by the renderer.
    /// Default implementation is a no-op (all three slots remain zero).
    /// </summary>
    void WriteShaderParams(Span<float> buffer, int nesWidth, int nesHeight) { }
}
