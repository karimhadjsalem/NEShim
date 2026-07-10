namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// Simulates CRT phosphor persistence by temporally accumulating frames.
/// Each output pixel is the current frame plus a decayed copy of the previous output,
/// producing a soft after-image trail that fades over roughly 10–15 frames.
/// The effect uses a ping-pong pair of intermediate render targets managed by
/// D3D11Renderer; NeedsTemporalBuffer signals this requirement.
/// </summary>
internal sealed class PhosphorPersistenceMotionEffect : IMotionEffect
{
    private const float DecayFactor = 0.65f;

    public VideoMotionEffectMode EffectMode          => VideoMotionEffectMode.PhosphorPersistence;
    public string?               PixelShaderResourceName
        => "NEShim.Rendering.Shaders.Dx11.PhosphorPersistence.ps.cso";
    public bool                  UseLinearSampler    => true;
    public bool                  NeedsTemporalBuffer => true;

    public (float Dx, float Dy) GetFrameOffset(long frameCount) => (0f, 0f);

    public void WriteShaderParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = DecayFactor;
    }
}

