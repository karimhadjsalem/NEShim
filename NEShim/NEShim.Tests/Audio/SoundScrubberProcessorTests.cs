using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class SoundScrubberProcessorTests
{
    private static SoundScrubberProcessor Create() => new();

    // ---- Interface contract ----

    [Test]
    public void Process_LargeInput_OutputIsNonZero()
    {
        var proc = Create();
        var (L, _) = proc.Process(10000);
        Assert.That(L, Is.Not.EqualTo(0));
    }

    [Test]
    public void Process_LAndRChannels_AreEqual()
    {
        var proc = Create();
        var (L, R) = proc.Process(10000);
        Assert.That(L, Is.EqualTo(R));
    }

    [Test]
    public void Process_ZeroInput_OutputIsZero()
    {
        var proc = Create();
        var (L, R) = proc.Process(0);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }

    // ---- DC blocking ----

    [Test]
    public void Process_ConstantInput_DecaysTowardZero()
    {
        var proc = Create();

        for (int i = 0; i < 500; i++)
            proc.Process(5000);

        short[] tail = new short[100];
        for (int i = 0; i < tail.Length; i++)
            tail[i] = proc.Process(5000).L;

        Assert.That(tail.All(v => Math.Abs(v) < 1000), Is.True,
            "Constant input should decay toward zero once HP filter settles");
    }

    // ---- Extra high-frequency attenuation ----

    [Test]
    public void HighFrequencySignal_IsMoreAttenuated_ThanNesFilterProcessor()
    {
        // A signal at 10 kHz should be further attenuated by the scrubber's extra LP@8kHz
        // compared with the baseline NES filter chain.
        const float freq       = 10000f;
        const float sampleRate = 44100f;
        const int   warmup     = 200;
        const int   measure    = 100;

        var nesProc  = new NesFilterProcessor();
        var scrubProc = Create();

        // Warm both up with the same signal
        for (int n = 0; n < warmup; n++)
        {
            short s = (short)(8000 * Math.Sin(2 * Math.PI * freq * n / sampleRate));
            nesProc.Process(s);
            scrubProc.Process(s);
        }

        short nesPeak   = 0;
        short scrubPeak = 0;
        for (int n = warmup; n < warmup + measure; n++)
        {
            short s = (short)(8000 * Math.Sin(2 * Math.PI * freq * n / sampleRate));
            short nesOut  = nesProc.Process(s).L;
            short scrubOut = scrubProc.Process(s).L;

            if (Math.Abs(nesOut)   > Math.Abs(nesPeak))   nesPeak   = nesOut;
            if (Math.Abs(scrubOut) > Math.Abs(scrubPeak)) scrubPeak = scrubOut;
        }

        Assert.That(Math.Abs(scrubPeak), Is.LessThan(Math.Abs(nesPeak)),
            "Scrubber should attenuate 10 kHz more than the baseline NES filter");
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsFilterMemory()
    {
        var proc = Create();

        for (int i = 0; i < 100; i++)
            proc.Process(5000);

        proc.ResetState();

        var fresh = Create();
        const short input = 8000;
        var (resetL, _) = proc.Process(input);
        var (freshL, _) = fresh.Process(input);

        Assert.That(resetL, Is.EqualTo(freshL));
    }
}
