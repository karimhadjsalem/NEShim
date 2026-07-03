namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// Simulates magnetic interference on a CRT by warping UV coordinates in a pixel shader.
/// A sine wave sweeps horizontally across the image — each row is displaced by a different
/// amount, so adjacent rows can shift in opposite directions, matching the characteristic
/// appearance of an external magnetic field deflecting the electron beam unevenly.
/// The wave phase and amplitude evolve slowly over time for an organic, non-repeating effect.
/// </summary>
internal sealed class MagneticDistortionMotionEffect : IMotionEffect
{
    // Phase advances 0.025 rad/frame → one full cycle every ~251 frames (~4.2 s at 60 fps).
    private const float PhaseRate = 0.025f;

    // Amplitude pulses between 80 % and 120 % of the base at a different frequency.
    private const float BaseAmplitude   = 0.002f;
    private const float AmplitudePulse  = 0.0005f; // ±25 % of base
    private const float PulseRate       = 0.31f;  // irrational w.r.t. PhaseRate

    // Spatial cycles of the sine wave per frame height in UV space.
    private const float Frequency = 8.0f;

    private long _frameCount;

    public VideoMotionEffectMode EffectMode => VideoMotionEffectMode.MagneticDistortion;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.MagneticDistortion.ps.cso";

    public bool UseLinearSampler => true;

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
