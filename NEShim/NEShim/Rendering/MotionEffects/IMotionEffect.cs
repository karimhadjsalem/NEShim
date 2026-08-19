namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// Strategy interface for per-frame screen-space motion effects applied to the NES frame quad.
/// Implementations return a clip-space (dx, dy) offset that the active renderer (D3D11Renderer
/// or SDL3HwRenderer — a CPU quad-offset effect uses the same implementation on both paths, see
/// CLAUDE.md's "Adding a new motion effect") adds to all four quad corner positions before
/// drawing. A zero offset is a no-op.
/// </summary>
internal interface IMotionEffect
{
    VideoMotionEffectMode EffectMode { get; }

    /// <summary>
    /// Returns the clip-space offset to apply to the NES frame quad this frame.
    /// <paramref name="frameCount"/> is the monotonically increasing draw-frame counter
    /// maintained by the active renderer.
    /// </summary>
    (float Dx, float Dy) GetFrameOffset(long frameCount);

    /// <summary>
    /// Called by the active renderer whenever the viewport or letterbox dimensions change so
    /// that effects that scale with screen size can recalibrate. Default implementation is a no-op.
    /// </summary>
    void NotifyLayout(int viewportWidth, int viewportHeight, int letterboxHeight) { }

    /// <summary>
    /// D3D11-only: optional embedded resource name of a pixel shader (.cso) to swap in for the
    /// NES frame quad draw call. When non-null, D3D11Renderer temporarily binds this shader
    /// instead of the structural filter shader and calls <see cref="WriteShaderParams"/>
    /// to populate the constant buffer, restoring the structural shader immediately after.
    /// Null (default) means no shader override — the active structural filter is used.
    /// The SDL_GPU path has its own separate <c>ISdlMotionEffect</c> interface for
    /// shader-backed effects instead of using this member (see CLAUDE.md's "Adding a new
    /// motion effect").
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

    /// <summary>
    /// When true, D3D11Renderer allocates a ping-pong pair of letterbox-sized intermediate
    /// render targets (_temporalRtA / _temporalRtB) and supplies the previous frame's output
    /// as <c>t1</c> when invoking the pixel shader. The effect's shader reads t0 (current)
    /// and t1 (history) to produce a temporally-blended result.
    /// Default is false — only temporal effects (e.g. PhosphorPersistence) override this.
    /// </summary>
    bool NeedsTemporalBuffer => false;
}
