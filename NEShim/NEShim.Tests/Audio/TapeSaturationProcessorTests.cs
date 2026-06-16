using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class TapeSaturationProcessorTests
{
    private static TapeSaturationProcessor Create() => new();

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
            last = proc.Process(16000);
        Assert.That(Math.Abs(last.L), Is.LessThan(500));
    }

    // ---- Saturation behaviour ----

    [Test]
    public void Process_MidLevelSignal_OutputExceedsNesFilter()
    {
        // tanh soft-clip maps x → tanh(Drive×x)/tanh(Drive), which is super-linear
        // below full scale — a mid-level signal gets a small gain boost.
        const short Level = 16384;
        var sat = Create();
        var nes = new NesFilterProcessor();

        int satPeak = 0, nesPeak = 0;
        for (int i = 0; i < 2000; i++)
        {
            short s = i % 2 == 0 ? Level : (short)-Level;
            var (sL, _) = sat.Process(s);
            var (nL, _) = nes.Process(s);
            if (i > 500)
            {
                satPeak = Math.Max(satPeak, Math.Abs(sL));
                nesPeak = Math.Max(nesPeak, Math.Abs(nL));
            }
        }

        Assert.That(satPeak, Is.GreaterThan(nesPeak));
    }

    [Test]
    public void Process_OutputBounded_BelowShortMax()
    {
        // Saturation maps all inputs into the short range — never clips.
        const short FullScale = short.MaxValue;
        var proc = Create();
        for (int i = 0; i < 2000; i++)
        {
            short s = i % 2 == 0 ? FullScale : short.MinValue;
            var (L, R) = proc.Process(s);
            Assert.That(Math.Abs(L), Is.LessThanOrEqualTo(short.MaxValue));
        }
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
