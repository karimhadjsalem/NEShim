namespace NEShim.Audio;

/// <summary>
/// Soft look-ahead compressor applied over the NES NTSC base filter chain.
///
/// Filter chain:
///   HP ~37 Hz → HP ~39 Hz → LP ~14 kHz → 5 ms look-ahead RMS compressor → mono→stereo
///
/// HP stages precede the RMS measurement to prevent DC bias from inflating the level
/// reading. The 5 ms look-ahead lets gain reduction ramp up before a transient
/// reaches the output. Running sum-of-squares keeps RMS computation O(1) per sample.
/// </summary>
internal sealed class CompressionProcessor : IAudioProcessor
{
    private const float HpAlpha1  = 0.994742f; // e^(-2π × 37    / 44100)
    private const float HpAlpha2  = 0.994462f; // e^(-2π × 39    / 44100)
    private const float LpBeta14k = 0.136224f; // e^(-2π × 14000 / 44100)

    // −6 dBFS threshold in sample units (full scale = 32767)
    internal static readonly float ThresholdLinear  = 32767f * (float)Math.Pow(10.0, -6.0 / 20.0);
    // +2 dB makeup gain
    internal static readonly float MakeupGainLinear = (float)Math.Pow(10.0, 2.0 / 20.0);
    internal const float Ratio = 3.0f;

    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lpOut;

    private readonly short[] _lookahead;
    private int _pos;
    private double _sumSq;
    private float _gain = 1.0f;

    private readonly float _attackCoeff;
    private readonly float _releaseCoeff;

    internal readonly int LookaheadSamples;

    public CompressionProcessor(int sampleRate = 44100)
    {
        LookaheadSamples = sampleRate * 5 / 1000; // 5 ms look-ahead
        _lookahead    = new short[LookaheadSamples];
        _attackCoeff  = (float)(1.0 - Math.Exp(-1.0 / (sampleRate * 0.002))); // 2 ms
        _releaseCoeff = (float)(1.0 - Math.Exp(-1.0 / (sampleRate * 0.100))); // 100 ms
    }

    public (short L, short R) Process(short monoSample)
    {
        float x = monoSample;

        float hp1 = HpAlpha1 * (_hp1Out + x - _hp1In);
        _hp1In  = x;
        _hp1Out = hp1;
        x       = hp1;

        float hp2 = HpAlpha2 * (_hp2Out + x - _hp2In);
        _hp2In  = x;
        _hp2Out = hp2;
        x       = hp2;

        float lp = LpBeta14k * _lpOut + (1f - LpBeta14k) * x;
        _lpOut = lp;

        short evicted       = _lookahead[_pos];
        _sumSq             -= (double)evicted * evicted;
        var filtered        = (short)Math.Clamp((int)lp, short.MinValue, short.MaxValue);
        _lookahead[_pos]    = filtered;
        _sumSq             += (double)filtered * filtered;
        _pos                = (_pos + 1) % LookaheadSamples;

        float rms        = (float)Math.Sqrt(_sumSq / LookaheadSamples);
        float targetGain = rms > ThresholdLinear
            ? (float)Math.Pow(ThresholdLinear / rms, 1.0 - 1.0 / Ratio)
            : 1.0f;

        float coeff = targetGain < _gain ? _attackCoeff : _releaseCoeff;
        _gain += (targetGain - _gain) * coeff;

        float output = evicted * _gain * MakeupGainLinear;
        short s = (short)Math.Clamp((int)output, short.MinValue, short.MaxValue);
        return (s, s);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = 0f;
        Array.Clear(_lookahead);
        _sumSq = 0.0;
        _gain  = 1.0f;
        _pos   = 0;
    }
}
