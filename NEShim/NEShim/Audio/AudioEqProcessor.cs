namespace NEShim.Audio;

/// <summary>
/// Three-band peaking EQ applied after the main audio processor.
/// Uses Direct Form 1 biquad filters (bass@100 Hz, mid@1 kHz, treble@8 kHz).
/// Processes stereo (short L, short R) pairs; maintains separate filter state per channel.
/// When all three gains are 0 the processor reports IsActive = false so AudioPlayer
/// can skip it entirely with no arithmetic overhead.
/// </summary>
internal sealed class AudioEqProcessor
{
    private readonly EqBand _bassL, _bassR, _midL, _midR, _trebleL, _trebleR;

    public bool IsActive { get; private set; }

    public AudioEqProcessor()
    {
        _bassL   = new EqBand(100f);
        _bassR   = new EqBand(100f);
        _midL    = new EqBand(1000f);
        _midR    = new EqBand(1000f);
        _trebleL = new EqBand(8000f);
        _trebleR = new EqBand(8000f);
    }

    public void SetGains(int bass, int mid, int treble)
    {
        IsActive = bass != 0 || mid != 0 || treble != 0;
        _bassL.SetGainDb(bass);     _bassR.SetGainDb(bass);
        _midL.SetGainDb(mid);       _midR.SetGainDb(mid);
        _trebleL.SetGainDb(treble); _trebleR.SetGainDb(treble);
    }

    public (short L, short R) Process(short inL, short inR)
    {
        float l = inL, r = inR;
        l = _bassL.Process(l);   r = _bassR.Process(r);
        l = _midL.Process(l);    r = _midR.Process(r);
        l = _trebleL.Process(l); r = _trebleR.Process(r);
        return ((short)Math.Clamp((int)MathF.Round(l), short.MinValue, short.MaxValue),
                (short)Math.Clamp((int)MathF.Round(r), short.MinValue, short.MaxValue));
    }

    public void ResetState()
    {
        _bassL.ResetState();   _bassR.ResetState();
        _midL.ResetState();    _midR.ResetState();
        _trebleL.ResetState(); _trebleR.ResetState();
    }

    private sealed class EqBand
    {
        private readonly float _sinW0;
        private readonly float _cosW0;

        private float _b0, _b1, _b2, _a1, _a2;
        private float _x1, _x2, _y1, _y2;

        private const float Q = 0.9f;

        public EqBand(float freq, int sampleRate = 44100)
        {
            float w0 = 2f * MathF.PI * freq / sampleRate;
            _sinW0   = MathF.Sin(w0);
            _cosW0   = MathF.Cos(w0);
            SetGainDb(0);
        }

        public void SetGainDb(int dBgain)
        {
            if (dBgain == 0)
            {
                _b0 = 1f; _b1 = 0f; _b2 = 0f; _a1 = 0f; _a2 = 0f;
                return;
            }
            float alpha = _sinW0 / (2f * Q);
            float A     = MathF.Pow(10f, dBgain / 40f);
            float a0    = 1f + alpha / A;

            _b0 = (1f + alpha * A) / a0;
            _b1 = (-2f * _cosW0)   / a0;
            _b2 = (1f - alpha * A) / a0;
            _a1 = (-2f * _cosW0)   / a0;
            _a2 = (1f - alpha / A) / a0;
        }

        public float Process(float x)
        {
            float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x;
            _y2 = _y1; _y1 = y;
            return y;
        }

        public void ResetState()
        {
            _x1 = _x2 = _y1 = _y2 = 0f;
        }
    }
}
