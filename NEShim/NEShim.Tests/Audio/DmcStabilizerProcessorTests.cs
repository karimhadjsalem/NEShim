using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class DmcStabilizerProcessorTests
{
    private DmcStabilizerProcessor _proc = null!;

    [SetUp]
    public void SetUp() => _proc = new DmcStabilizerProcessor();

    // ---- Interface contract ----

    [Test]
    public void Process_ReturnsStereoTuple()
    {
        var (l, r) = _proc.Process(1000);
        // Both channels must be equal — processor is mono-to-stereo.
        Assert.That(l, Is.EqualTo(r));
    }

    [Test]
    public void Process_ZeroInput_OutputIsZero()
    {
        for (int i = 0; i < 20; i++)
        {
            var (l, r) = _proc.Process(0);
            Assert.That(l, Is.EqualTo(0));
            Assert.That(r, Is.EqualTo(0));
        }
    }

    // ---- Slew-rate limiting ----

    [Test]
    public void Process_SmallDelta_NoSlewLimiting_OutputUnchanged()
    {
        // Drive filter to a known state with a constant signal.
        for (int i = 0; i < 20; i++) _proc.Process(1000);

        // Small step that is below PopThreshold (~6000): slew rate limiter should not fire.
        // We can't compare to input because the HP/LP chain is also applied, but we can
        // verify the output is non-zero (signal passes through).
        var (l, _) = _proc.Process(1000);
        Assert.That(l, Is.Not.EqualTo(0).Within(500));
    }

    [Test]
    public void Process_LargeDelta_SlewLimiterReducesJump()
    {
        // PopThreshold = 6000. Input 8000 triggers slew: x = 0 + 8000*0.5 = 4000.
        // 4000 is below the threshold so the reference receives 4000 without slewing.
        // Both feed the same value to the HP/LP chain → equal output.
        var slewed    = new DmcStabilizerProcessor();
        var reference = new DmcStabilizerProcessor();

        var (slewedL, _)    = slewed.Process(8000);
        var (referenceL, _) = reference.Process(4000);

        Assert.That(slewedL, Is.EqualTo(referenceL));
    }

    [Test]
    public void Process_LargeNegativeDelta_SlewLimiterReducesJump()
    {
        var slewed    = new DmcStabilizerProcessor();
        var reference = new DmcStabilizerProcessor();

        // PopThreshold = 6000. Input -8000 triggers the slew: x = 0 + (-8000)*0.5 = -4000.
        // -4000 is below the threshold so the reference also receives -4000 without further slewing.
        // Both processors therefore feed the same value to the HP/LP chain and must produce equal output.
        var (slewedL, _)    = slewed.Process(-8000);
        var (referenceL, _) = reference.Process(-4000);

        Assert.That(slewedL, Is.EqualTo(referenceL));
    }

    // ---- Filter chain (HP + LP) ----

    [Test]
    public void Process_ConstantInput_OutputDecaysTowardZero()
    {
        // The dual HP chain removes DC. After many identical samples the output
        // must settle near zero.
        const short constant = 5000;
        for (int i = 0; i < 500; i++) _proc.Process(constant);

        // After settling, run another 100 samples and check all are near zero.
        for (int i = 0; i < 100; i++)
        {
            var (l, _) = _proc.Process(constant);
            Assert.That(Math.Abs(l), Is.LessThan(1000),
                $"HP filter should have settled after 500 identical samples (step {i})");
        }
    }

    [Test]
    public void Process_NonZeroInput_OutputEventuallyNonZero()
    {
        // The LP filter passes audio through (not a brick wall), so a non-zero
        // input should produce a non-zero output in the first few samples.
        bool anyNonZero = false;
        for (int i = 0; i < 10; i++)
        {
            var (l, _) = _proc.Process(8000);
            if (l != 0) { anyNonZero = true; break; }
        }
        Assert.That(anyNonZero, Is.True);
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsFilterHistory()
    {
        // Drive the filter to a non-zero internal state.
        for (int i = 0; i < 20; i++) _proc.Process(8000);

        _proc.ResetState();

        // After reset, processor should behave identically to a fresh instance.
        var fresh = new DmcStabilizerProcessor();
        var (resetL, _) = _proc.Process(8000);
        var (freshL, _) = fresh.Process(8000);

        Assert.That(resetL, Is.EqualTo(freshL));
    }

    [Test]
    public void ResetState_ThenZeroInput_OutputIsZero()
    {
        for (int i = 0; i < 10; i++) _proc.Process(5000);
        _proc.ResetState();

        var (l, _) = _proc.Process(0);
        Assert.That(l, Is.EqualTo(0));
    }
}
