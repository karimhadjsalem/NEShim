using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class CompressionProcessorTests
{
    private const int TestSampleRate = 44100;

    private static CompressionProcessor Create() => new(sampleRate: TestSampleRate);

    private static (short L, short R) RunAndGetLast(CompressionProcessor proc, short input, int count)
    {
        (short L, short R) result = default;
        for (int i = 0; i < count; i++)
            result = proc.Process(input);
        return result;
    }

    // ---- Basic correctness ----

    [Test]
    public void Process_ZeroInput_ReturnsZero()
    {
        var proc = Create();
        var (L, R) = RunAndGetLast(proc, 0, 500);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }

    [Test]
    public void Process_OutputIsMonoStereo()
    {
        var proc = Create();
        int lookahead = proc.LookaheadSamples;
        // Alternate to pass HP filters
        (short L, short R) last = default;
        for (int i = 0; i < lookahead + 500; i++)
            last = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        Assert.That(last.L, Is.EqualTo(last.R));
    }

    [Test]
    public void Process_DCSignal_IsBlocked()
    {
        var proc = Create();
        var (L, _) = RunAndGetLast(proc, 16000, 3000);
        Assert.That(Math.Abs(L), Is.LessThan(500));
    }

    // ---- Compression behaviour ----

    [Test]
    public void Process_LoudSignal_IsAttenuated()
    {
        // Alternating loud signal — well above −6 dBFS threshold (~16428)
        const short loudSample = 30000;
        var proc    = Create();
        int lookahead = proc.LookaheadSamples;
        int peak    = 0;

        for (int i = 0; i < lookahead + 4000; i++)
        {
            short s   = i % 2 == 0 ? loudSample : (short)-loudSample;
            var (L, _) = proc.Process(s);
            if (i > lookahead + 500)
                peak = Math.Max(peak, Math.Abs(L));
        }

        // Compression should bring peak below the uncompressed level
        Assert.That(peak, Is.LessThan(loudSample));
    }

    [Test]
    public void Process_QuietSignal_IsNotSignificantlyAttenuated()
    {
        // Quiet signal well below threshold should not be attenuated
        const short quietSample = 100;
        var proc      = Create();
        int lookahead = proc.LookaheadSamples;
        int peak      = 0;

        for (int i = 0; i < lookahead + 2000; i++)
        {
            short s   = i % 2 == 0 ? quietSample : (short)-quietSample;
            var (L, _) = proc.Process(s);
            if (i > lookahead + 200)
                peak = Math.Max(peak, Math.Abs(L));
        }

        // Quiet signal should get makeup gain, not attenuation
        Assert.That(peak, Is.GreaterThan(quietSample / 2));
    }

    [Test]
    public void Process_Compression_LevelSettlesLowerThanInitialBurst()
    {
        // The first loud outputs (when gain is still near 1.0) should be louder than
        // the settled outputs where gain has been fully reduced by the compressor.
        // This verifies that compression actively reduces level over time.
        var proc      = Create();
        int lookahead = proc.LookaheadSamples;
        const short loudLevel = 30000;

        int earlyPeak  = 0;
        int settledPeak = 0;

        for (int i = 0; i < lookahead + 3000; i++)
        {
            short s   = i % 2 == 0 ? loudLevel : (short)-loudLevel;
            var (L, _) = proc.Process(s);
            // First 100 outputs after lookahead ring fills (gain still approaching target)
            if (i >= lookahead && i < lookahead + 100)
                earlyPeak = Math.Max(earlyPeak, Math.Abs(L));
            // Last 500 outputs where gain has fully converged
            if (i >= lookahead + 2500)
                settledPeak = Math.Max(settledPeak, Math.Abs(L));
        }

        Assert.That(settledPeak, Is.LessThan(earlyPeak));
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsLookahead()
    {
        var proc = Create();
        int lookahead = proc.LookaheadSamples;
        for (int i = 0; i < lookahead + 100; i++)
            proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        proc.ResetState();
        var (L, _) = proc.Process(0);
        Assert.That(L, Is.EqualTo(0));
    }

    [Test]
    public void ResetState_ResetsGainToUnity()
    {
        // Drive hard compression, reset, then check quiet signal gets makeup gain (not residual attenuation)
        var proc      = Create();
        int lookahead = proc.LookaheadSamples;
        RunAndGetLast(proc, 30000, lookahead + 1000);
        proc.ResetState();

        // After reset, a quiet signal should not be attenuated below its input level
        const short quietLevel = 500;
        int peak = 0;
        for (int i = 0; i < lookahead + 500; i++)
        {
            var (L, _) = proc.Process(i % 2 == 0 ? quietLevel : (short)-quietLevel);
            if (i > lookahead + 50)
                peak = Math.Max(peak, Math.Abs(L));
        }
        Assert.That(peak, Is.GreaterThan(quietLevel / 2));
    }
}
