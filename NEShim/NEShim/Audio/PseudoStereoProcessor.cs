namespace NEShim.Audio;

/// <summary>
/// Applies Haas-effect pseudo-stereo to the NES mono signal.
///
/// Filter chain:
///   HP ~37 Hz → HP ~39 Hz → LP ~14 kHz → Haas delay
///
/// The left channel receives the filtered signal at 60% amplitude.
/// The right channel receives the same signal delayed by <paramref name="delayMs"/> ms
/// at 40% amplitude. The default 20 ms delay is above the Haas threshold,
/// creating stereo width without an audible echo.
/// </summary>
internal sealed class PseudoStereoProcessor : IAudioProcessor
{
    private const float HpAlpha1  = 0.994742f; // e^(-2π × 37    / 44100)
    private const float HpAlpha2  = 0.994462f; // e^(-2π × 39    / 44100)
    private const float LpBeta14k = 0.136224f; // e^(-2π × 14000 / 44100)
    private const float GainL     = 0.6f;
    private const float GainR     = 0.4f;

    private float _hp1Out, _hp1In;
    private float _hp2Out, _hp2In;
    private float _lpOut;

    private readonly short[] _ring;
    private int _ringPos;

    public PseudoStereoProcessor(int sampleRate = 44100, int delayMs = 20)
    {
        _ring = new short[Math.Max(1, sampleRate * delayMs / 1000)];
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

        short delayed       = _ring[_ringPos];
        _ring[_ringPos]     = (short)Math.Clamp((int)lp, short.MinValue, short.MaxValue);
        _ringPos            = (_ringPos + 1) % _ring.Length;

        short l = (short)Math.Clamp((int)(lp      * GainL), short.MinValue, short.MaxValue);
        short r = (short)Math.Clamp((int)(delayed  * GainR), short.MinValue, short.MaxValue);
        return (l, r);
    }

    public void ResetState()
    {
        _hp1Out = _hp1In = _hp2Out = _hp2In = _lpOut = 0f;
        Array.Clear(_ring);
        _ringPos = 0;
    }
}
