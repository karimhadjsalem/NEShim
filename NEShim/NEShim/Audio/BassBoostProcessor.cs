namespace NEShim.Audio;

internal sealed class BassBoostProcessor : IAudioProcessor
{
    private const float HpAlpha1  = 0.994742f;
    private const float HpAlpha2  = 0.994462f;
    private const float LpBeta14k = 0.136224f;
    private const float BoostGain = 0.6f;     // +4 dB at DC, ~+2 dB at shelf cutoff

    private float _hp1Out, _hp1In, _hp2Out, _hp2In, _lpOut, _shelfOut;

    // Feedback coefficient for the LP shelf (≈ 0.979 at 44100 Hz / 150 Hz)
    private readonly float _shelfBeta;

    public BassBoostProcessor(int sampleRate = 44100)
    {
        _shelfBeta = MathF.Exp(-2f * MathF.PI * 150f / sampleRate);
    }

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        float hp1 = HpAlpha1 * (_hp1Out + x - _hp1In);
        _hp1In = x; _hp1Out = hp1;

        float hp2 = HpAlpha2 * (_hp2Out + hp1 - _hp2In);
        _hp2In = hp1; _hp2Out = hp2;

        float lp = LpBeta14k * _lpOut + (1f - LpBeta14k) * hp2;
        _lpOut = lp;

        float shelf = _shelfBeta * _shelfOut + (1f - _shelfBeta) * lp;
        _shelfOut = shelf;

        float output = lp + BoostGain * shelf;
        short s = (short)Math.Clamp((int)output, short.MinValue, short.MaxValue);
        return (s, s);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = _shelfOut = 0f;
    }
}
