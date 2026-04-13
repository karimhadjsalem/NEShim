namespace NEShim.Audio;

/// <summary>
/// Departs from NES hardware accuracy to produce a warmer, tighter sound on modern hardware.
///
/// Filter chain:
///   HP ~80 Hz  → HP ~80 Hz  → LP ~14 kHz  → LP ~8 kHz
///
/// vs. the NES hardware spec (HP ~37 Hz → HP ~39 Hz → LP ~14 kHz):
///   - HP cutoffs raised to ~80 Hz: shorter time constant (~12 ms vs ~43 ms) gives tighter
///     bass transients and avoids the slow-bloom "reverb" effect of the hardware high-passes.
///   - Extra LP at ~8 kHz: attenuates harsh high-frequency square-wave harmonics,
///     producing a warmer sound that is less fatiguing on modern speakers.
/// </summary>
internal sealed class SoundScrubberProcessor : IAudioProcessor
{
    // Raised HP cutoffs: ~80 Hz (vs NES hardware ~37/39 Hz)
    private const float HpAlpha   = 0.988693f; // e^(-2π × 80    / 44100) — both HP stages
    private const float LpBeta14k = 0.136224f; // e^(-2π × 14000 / 44100)

    // Scrubber stage: additional LP at ~8 kHz
    private const float LpBeta8k  = 0.317757f; // e^(-2π × 8000  / 44100)

    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lp14kOut;
    private float _lp8kOut;

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        // High-pass 1 (~80 Hz)
        float hp1 = HpAlpha * (_hp1Out + x - _hp1In);
        _hp1In  = x;
        _hp1Out = hp1;
        x       = hp1;

        // High-pass 2 (~80 Hz)
        float hp2 = HpAlpha * (_hp2Out + x - _hp2In);
        _hp2In  = x;
        _hp2Out = hp2;
        x       = hp2;

        // Low-pass (~14 kHz) — NES hardware stage
        float lp14k = LpBeta14k * _lp14kOut + (1f - LpBeta14k) * x;
        _lp14kOut = lp14k;
        x         = lp14k;

        // Low-pass (~8 kHz) — scrubber stage
        float lp8k = LpBeta8k * _lp8kOut + (1f - LpBeta8k) * x;
        _lp8kOut = lp8k;

        short s = (short)Math.Clamp((int)lp8k, short.MinValue, short.MaxValue);
        return (s, s);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lp14kOut = _lp8kOut = 0f;
    }
}
