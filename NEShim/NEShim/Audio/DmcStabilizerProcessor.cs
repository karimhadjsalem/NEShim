namespace NEShim.Audio;

/// <summary>
/// Slew-rate limiter that softens large DMC sample-to-sample amplitude jumps
/// before the standard NES hardware filter chain.
/// DMC samples are 7-bit unsigned values that change in discrete steps up to the
/// full output range; on the real hardware the RC output stage absorbs the transient,
/// but at 44100 Hz the step is instantaneous and produces audible clicks.
/// When the delta between consecutive input samples exceeds <see cref="PopThreshold"/>,
/// this filter blends the new sample toward the previous value rather than jumping
/// immediately, trading a single-sample softening for a significant reduction in pop.
/// </summary>
internal sealed class DmcStabilizerProcessor : IAudioProcessor
{
    private const float PopThreshold = 6000f;  // ~18% of full short scale
    private const float SmoothAlpha  = 0.5f;

    // Standard NES NTSC hardware RC filter chain (same as NesFilterProcessor).
    private const float HpAlpha1 = 0.994742f;
    private const float HpAlpha2 = 0.994462f;
    private const float LpBeta   = 0.136224f;

    private float _prev;
    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lpOut;

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        // Slew-rate limiter: when the jump is larger than the threshold, partially
        // smooth toward the previous value to reduce DMC pop amplitude.
        float delta = x - _prev;
        if (delta > PopThreshold || delta < -PopThreshold)
            x = _prev + delta * SmoothAlpha;
        _prev = x;

        // High-pass 1 (~37 Hz)
        float hp1 = HpAlpha1 * (_hp1Out + x - _hp1In);
        _hp1In  = x;
        _hp1Out = hp1;
        x       = hp1;

        // High-pass 2 (~39 Hz)
        float hp2 = HpAlpha2 * (_hp2Out + x - _hp2In);
        _hp2In  = x;
        _hp2Out = hp2;
        x       = hp2;

        // Low-pass (~14 kHz)
        float lp = LpBeta * _lpOut + (1f - LpBeta) * x;
        _lpOut = lp;

        short s = (short)Math.Clamp((int)lp, short.MinValue, short.MaxValue);
        return (s, s);
    }

    public void ResetState()
    {
        _prev = _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = 0f;
    }
}
