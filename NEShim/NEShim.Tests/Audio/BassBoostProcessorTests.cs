using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class BassBoostProcessorTests
{
    private const int TestSampleRate = 44100;

    private static BassBoostProcessor Create() => new(sampleRate: TestSampleRate);

    // ---- Basic correctness ----

    [Test]
    public void Process_ZeroInput_ReturnsZero()
    {
        var proc = Create();
        (short L, short R) result = default;
        for (int i = 0; i < 500; i++)
            result = proc.Process(0);
        Assert.That(result.L, Is.EqualTo(0));
        Assert.That(result.R, Is.EqualTo(0));
    }

    [Test]
    public void Process_OutputIsMonoStereo()
    {
        var proc = Create();
        (short L, short R) last = default;
        for (int i = 0; i < 1000; i++)
            last = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        Assert.That(last.L, Is.EqualTo(last.R));
    }

    [Test]
    public void Process_DCSignal_IsBlocked()
    {
        var proc = Create();
        (short L, short R) last = default;
        for (int i = 0; i < 5000; i++)
            last = proc.Process(10000);
        Assert.That(Math.Abs(last.L), Is.LessThan(500));
    }

    // ---- Bass boost behaviour ----

    [Test]
    public void Process_LowFrequency_ExceedsNesFilter()
    {
        // 100 Hz sine wave: above the HP cutoffs (~37/39 Hz) and fully within the
        // LP@150 Hz pass band (gain ~0.83).  A sine avoids the HP step-transient
        // spikes that a square wave would produce, giving a clean ~50% margin.
        const short Amplitude = 10000;
        var boost = Create();
        var nes   = new NesFilterProcessor();

        int boostPeak = 0, nesPeak = 0;
        for (int i = 0; i < TestSampleRate; i++)
        {
            short s = (short)(Amplitude * Math.Sin(2.0 * Math.PI * 100.0 * i / TestSampleRate));
            var (bL, _) = boost.Process(s);
            var (nL, _) = nes.Process(s);
            if (i > TestSampleRate / 2)
            {
                boostPeak = Math.Max(boostPeak, Math.Abs(bL));
                nesPeak   = Math.Max(nesPeak,   Math.Abs(nL));
            }
        }

        Assert.That(boostPeak, Is.GreaterThan(nesPeak));
    }

    [Test]
    public void Process_HighFrequency_SimilarToNesFilter()
    {
        // At Nyquist (alternating ±N), the shelf LP@150 Hz output is ~0, so the boost
        // adds nothing — both processors should produce equivalent peaks.
        const short Amplitude = 10000;
        var boost = Create();
        var nes   = new NesFilterProcessor();

        int boostPeak = 0, nesPeak = 0;
        for (int i = 0; i < 2000; i++)
        {
            short s = i % 2 == 0 ? Amplitude : (short)-Amplitude;
            var (bL, _) = boost.Process(s);
            var (nL, _) = nes.Process(s);
            if (i > 500)
            {
                boostPeak = Math.Max(boostPeak, Math.Abs(bL));
                nesPeak   = Math.Max(nesPeak,   Math.Abs(nL));
            }
        }

        // Within 5 % of each other at Nyquist
        Assert.That(boostPeak, Is.InRange(nesPeak - nesPeak / 20, nesPeak + nesPeak / 20));
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsFilterState()
    {
        var proc = Create();
        for (int i = 0; i < 1000; i++)
            proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        proc.ResetState();
        var (L, R) = proc.Process(0);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }
}
