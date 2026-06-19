using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class WarmStereoProcessorTests
{
    private const int TestSampleRate = 44100;
    private const int TestDelayMs    = 5;
    private static int DelayFrames   => TestSampleRate * TestDelayMs / 1000;

    private static WarmStereoProcessor CreateShort() =>
        new(sampleRate: TestSampleRate, delayMs: TestDelayMs);

    private static (short L, short R) RunAndGetLast(WarmStereoProcessor proc, short input, int count)
    {
        (short L, short R) result = default;
        for (int i = 0; i < count; i++)
            result = proc.Process(input);
        return result;
    }

    // ---- Process ----

    [Test]
    public void Process_LargeInput_ReturnsNonZeroOutput()
    {
        var proc = CreateShort();
        // Alternating input passes HP filters
        (short L, short R) last = default;
        for (int i = 0; i < DelayFrames + 500; i++)
            last = proc.Process(i % 2 == 0 ? (short)10000 : (short)-10000);
        Assert.That(last.L, Is.Not.EqualTo(0));
        Assert.That(last.R, Is.Not.EqualTo(0));
    }

    [Test]
    public void Process_ZeroInput_ReturnsZero()
    {
        var proc = CreateShort();
        var (L, R) = RunAndGetLast(proc, 0, 1000);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }

    [Test]
    public void Process_AfterWarmup_LAndRDiffer()
    {
        var proc = CreateShort();
        (short L, short R) last = default;
        for (int i = 0; i < DelayFrames + 100; i++)
            last = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        Assert.That(last.L, Is.Not.EqualTo(last.R));
    }

    [Test]
    public void Process_LChannelLouder_ThanRChannel()
    {
        var proc = CreateShort();
        int louderCount = 0;
        for (int i = 0; i < DelayFrames + 500; i++)
        {
            var (L, R) = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
            if (i > DelayFrames && Math.Abs(L) > Math.Abs(R))
                louderCount++;
        }
        Assert.That(louderCount, Is.GreaterThan(200));
    }

    [Test]
    public void Process_AttenuatesHighFreq_MoreThanPseudoStereo()
    {
        // Feed a 10 kHz sine-like alternating signal. WarmStereoProcessor has an extra
        // LP@8 kHz stage that PseudoStereoProcessor lacks, so its L-channel peak
        // should be lower after the same number of warmup samples.
        var warm   = new WarmStereoProcessor(sampleRate: TestSampleRate, delayMs: TestDelayMs);
        var pseudo = new PseudoStereoProcessor(sampleRate: TestSampleRate, delayMs: TestDelayMs);

        int warmupSamples = DelayFrames + 2000;
        int warmPeak = 0, pseudoPeak = 0;

        for (int i = 0; i < warmupSamples; i++)
        {
            // Approximate 10 kHz: alternate every 2 samples at 44100 Hz ≈ 22 kHz,
            // use every 4 samples to approximate 11 kHz
            short s = (i % 4 < 2) ? (short)8000 : (short)-8000;
            var (wL, _) = warm.Process(s);
            var (pL, _) = pseudo.Process(s);
            if (i > DelayFrames + 100)
            {
                warmPeak   = Math.Max(warmPeak,   Math.Abs(wL));
                pseudoPeak = Math.Max(pseudoPeak, Math.Abs(pL));
            }
        }

        Assert.That(warmPeak, Is.LessThan(pseudoPeak));
    }

    [Test]
    public void Process_DCSignal_IsBlocked()
    {
        var proc = CreateShort();
        // Constant input (DC) should converge to 0 via the HP filters
        (short L, short R) last = default;
        for (int i = 0; i < 5000; i++)
            last = proc.Process(10000);
        Assert.That(Math.Abs(last.L), Is.LessThan(100));
        Assert.That(Math.Abs(last.R), Is.LessThan(100));
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsAll()
    {
        var proc = CreateShort();
        for (int i = 0; i < DelayFrames + 200; i++)
            proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        proc.ResetState();
        var (L, R) = proc.Process(0);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }
}
