namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// SDL_GPU / SPIR-V counterpart of <see cref="MagneticDistortionMotionEffect"/>.
/// Uses the same wave parameters and <see cref="IMotionEffect.WriteShaderParams"/>
/// contract; the only difference is the shader resource name points at the .spv
/// rather than the D3D11 .cso.
/// </summary>
internal sealed class MagneticDistortionSdlMotionEffect : ISdlMotionEffect
{
    private const float PhaseRate      = 0.025f;
    private const float BaseAmplitude  = 0.002f;
    private const float AmplitudePulse = 0.0005f;
    private const float PulseRate      = 0.31f;
    private const float Frequency      = 8.0f;

    private long _frameCount;

    public VideoMotionEffectMode EffectMode       => VideoMotionEffectMode.MagneticDistortion;
    public string? SpvResourceName                => "NEShim.Rendering.Shaders.Vulkan.MagneticDistortion.ps.spv";
    public bool    UseLinearSampler               => true;

    public (float Dx, float Dy) GetFrameOffset(long frameCount)
    {
        _frameCount = frameCount;
        return (0f, 0f);
    }

    public void WriteShaderParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        float phase     = _frameCount * PhaseRate;
        float amplitude = BaseAmplitude + AmplitudePulse * MathF.Sin(phase * PulseRate);
        buffer[0] = phase;
        buffer[1] = amplitude;
        buffer[2] = Frequency;
    }
}
