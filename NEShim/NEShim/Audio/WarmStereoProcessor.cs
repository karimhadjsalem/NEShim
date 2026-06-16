namespace NEShim.Audio;

/// <summary>
/// Combines the warm tone of <see cref="SoundScrubberProcessor"/> with Haas-effect stereo.
///
/// Filter chain:
///   HP ~80 Hz → HP ~80 Hz → LP ~14 kHz → Haas delay → LP ~8 kHz (per channel)
///
/// LP@8 kHz is applied independently to each channel after the Haas split so the
/// tonal character of the direct and delayed paths evolve separately, unlike
/// <see cref="SoundScrubberProcessor"/> which applies LP@8 kHz before the stereo split.
/// </summary>
internal sealed class WarmStereoProcessor : IAudioProcessor
{
    private const float HpAlpha   = 0.988693f; // e^(-2π × 80    / 44100) — both HP stages
    private const float LpBeta14k = 0.136224f; // e^(-2π × 14000 / 44100)
    private const float LpBeta8k  = 0.317757f; // e^(-2π × 8000  / 44100)
    private const float GainL     = 0.6f;
    private const float GainR     = 0.4f;

    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lp14kOut;
    private float _lp8kL;
    private float _lp8kR;

    private readonly short[] _ring;
    private int _ringPos;

    public WarmStereoProcessor(int sampleRate = 44100, int delayMs = 20)
    {
        _ring = new short[Math.Max(1, sampleRate * delayMs / 1000)];
    }

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        float hp1 = HpAlpha * (_hp1Out + x - _hp1In);
        _hp1In  = x;
        _hp1Out = hp1;
        x       = hp1;

        float hp2 = HpAlpha * (_hp2Out + x - _hp2In);
        _hp2In  = x;
        _hp2Out = hp2;
        x       = hp2;

        float lp14k = LpBeta14k * _lp14kOut + (1f - LpBeta14k) * x;
        _lp14kOut = lp14k;

        short delayed       = _ring[_ringPos];
        _ring[_ringPos]     = (short)Math.Clamp((int)lp14k, short.MinValue, short.MaxValue);
        _ringPos            = (_ringPos + 1) % _ring.Length;

        float lFull = lp14k   * GainL;
        float rFull = delayed * GainR;

        float lp8kL = LpBeta8k * _lp8kL + (1f - LpBeta8k) * lFull;
        _lp8kL = lp8kL;

        float lp8kR = LpBeta8k * _lp8kR + (1f - LpBeta8k) * rFull;
        _lp8kR = lp8kR;

        short l = (short)Math.Clamp((int)lp8kL, short.MinValue, short.MaxValue);
        short r = (short)Math.Clamp((int)lp8kR, short.MinValue, short.MaxValue);
        return (l, r);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lp14kOut = 0f;
        _lp8kL = _lp8kR = 0f;
        Array.Clear(_ring);
        _ringPos = 0;
    }
}
