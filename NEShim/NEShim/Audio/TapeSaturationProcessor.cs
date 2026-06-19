namespace NEShim.Audio;

internal sealed class TapeSaturationProcessor : IAudioProcessor
{
    private const float HpAlpha1  = 0.994742f;
    private const float HpAlpha2  = 0.994462f;
    private const float LpBeta14k = 0.136224f;

    internal const float Drive = 1.5f;
    internal static readonly float TanhDrive = MathF.Tanh(Drive); // ≈ 0.9051

    private float _hp1Out, _hp1In, _hp2Out, _hp2In, _lpOut;

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        float hp1 = HpAlpha1 * (_hp1Out + x - _hp1In);
        _hp1In = x; _hp1Out = hp1;

        float hp2 = HpAlpha2 * (_hp2Out + hp1 - _hp2In);
        _hp2In = hp1; _hp2Out = hp2;

        float lp = LpBeta14k * _lpOut + (1f - LpBeta14k) * hp2;
        _lpOut = lp;

        float normalized = lp / 32767f;
        float saturated  = MathF.Tanh(Drive * normalized) / TanhDrive;
        short s = (short)Math.Clamp((int)(saturated * 32767f), short.MinValue, short.MaxValue);
        return (s, s);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = 0f;
    }
}
