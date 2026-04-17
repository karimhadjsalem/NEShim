namespace NEShim.Audio;

/// <summary>
/// Applies the NES NTSC hardware output RC filter chain to each mono sample.
/// Matches the three analog filters on the real NES audio output line:
///   High-pass ~37 Hz  (DC bias removal)
///   High-pass ~39 Hz  (secondary DC removal)
///   Low-pass  ~14 kHz (treble cap / warmth)
/// Source: https://www.nesdev.org/wiki/APU_Mixer#Emulation
/// </summary>
internal sealed class NesFilterProcessor : IAudioProcessor
{
    private const float HpAlpha1 = 0.994742f; // e^(-2π × 37    / 44100)
    private const float HpAlpha2 = 0.994462f; // e^(-2π × 39    / 44100)
    private const float LpBeta   = 0.136224f; // e^(-2π × 14000 / 44100)

    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lpOut;

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        // High-pass 1 (~37 Hz): y[n] = α*(y[n-1] + x[n] - x[n-1])
        float hp1 = HpAlpha1 * (_hp1Out + x - _hp1In);
        _hp1In  = x;
        _hp1Out = hp1;
        x       = hp1;

        // High-pass 2 (~39 Hz)
        float hp2 = HpAlpha2 * (_hp2Out + x - _hp2In);
        _hp2In  = x;
        _hp2Out = hp2;
        x       = hp2;

        // Low-pass (~14 kHz): y[n] = β*y[n-1] + (1-β)*x[n]
        float lp = LpBeta * _lpOut + (1f - LpBeta) * x;
        _lpOut = lp;

        short s = (short)Math.Clamp((int)lp, short.MinValue, short.MaxValue);
        return (s, s);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = 0f;
    }
}
