namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// SDL_GPU / SPIR-V counterpart of <see cref="PhosphorPersistenceMotionEffect"/>. Same DecayFactor
/// and ping-pong temporal-buffer contract, but with a real 2-sampler shader (currentFrame +
/// historyFrame bound simultaneously) rather than D3D11's — SDL3HwRenderer's custom
/// SDL_GPUGraphicsPipeline (SdlGpuPipeline) supports more than one fragment sampler, unlike the
/// older SDL_CreateGPURenderState convenience API this replaced, which only ever provided one.
/// </summary>
internal sealed class PhosphorPersistenceSdlMotionEffect : ISdlMotionEffect
{
    private const float DecayFactor = 0.65f;

    public VideoMotionEffectMode EffectMode          => VideoMotionEffectMode.PhosphorPersistence;
    public string? SpvResourceName                   => "NEShim.Rendering.Shaders.Vulkan.PhosphorPersistence.ps.spv";
    public bool    UseLinearSampler                  => true;
    public bool    NeedsTemporalBuffer               => true;
    public uint    NumFragmentSamplers               => 2;

    public (float Dx, float Dy) GetFrameOffset(long frameCount) => (0f, 0f);

    public void WriteShaderParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = DecayFactor;
    }
}
