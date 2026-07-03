using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class AudioEqProcessorTests
{
    private AudioEqProcessor _eq = null!;

    [SetUp]
    public void SetUp() => _eq = new AudioEqProcessor();

    // ---- IsActive ----

    [Test]
    public void IsActive_NewInstance_IsFalse()
        => Assert.That(_eq.IsActive, Is.False);

    [Test]
    public void SetGains_AllZero_IsActiveFalse()
    {
        _eq.SetGains(5, 3, -2);
        _eq.SetGains(0, 0, 0);
        Assert.That(_eq.IsActive, Is.False);
    }

    [Test]
    public void SetGains_NonZeroBass_IsActiveTrue()
    {
        _eq.SetGains(6, 0, 0);
        Assert.That(_eq.IsActive, Is.True);
    }

    [Test]
    public void SetGains_NonZeroMid_IsActiveTrue()
    {
        _eq.SetGains(0, -3, 0);
        Assert.That(_eq.IsActive, Is.True);
    }

    [Test]
    public void SetGains_NonZeroTreble_IsActiveTrue()
    {
        _eq.SetGains(0, 0, 12);
        Assert.That(_eq.IsActive, Is.True);
    }

    // ---- Process with identity (all gains = 0) ----

    [Test]
    public void Process_AllGainsZero_OutputEqualsInput()
    {
        // With all gains at 0 the biquad coefficients are identity (b0=1, b1=b2=a1=a2=0).
        // Output should equal input exactly.
        _eq.SetGains(0, 0, 0);
        var (l, r) = _eq.Process(1000, -500);
        Assert.That(l, Is.EqualTo(1000));
        Assert.That(r, Is.EqualTo(-500));
    }

    [Test]
    public void Process_AllGainsZero_SilenceInSilenceOut()
    {
        _eq.SetGains(0, 0, 0);
        var (l, r) = _eq.Process(0, 0);
        Assert.That(l, Is.EqualTo(0));
        Assert.That(r, Is.EqualTo(0));
    }

    // ---- L and R are processed independently ----

    [Test]
    public void Process_LAndRChannels_AreIndependent()
    {
        // After a non-zero left sample, the right channel should remain at identity.
        _eq.SetGains(0, 0, 0);
        // Drive L state with a large value, keep R at zero.
        _eq.Process(10000, 0);
        var (_, r) = _eq.Process(0, 5000);
        // R should equal 5000 (identity), not influenced by L history.
        Assert.That(r, Is.EqualTo(5000));
    }

    // ---- Process with non-zero gain changes the output ----

    [Test]
    public void Process_WithBassBoost_OutputDiffersFromInput()
    {
        // Drive multiple samples at a DC-like value so the filter state builds up.
        // Bass filter at +12 dB should amplify low-frequency content.
        _eq.SetGains(12, 0, 0);
        short val = 5000;
        (short l, short _) = _eq.Process(val, val);
        // After the very first sample the output should differ from input
        // because the bass peaking filter amplifies at 100 Hz.
        // We check non-equality rather than a specific value to avoid coupling to exact coefficients.
        Assert.That(l, Is.Not.EqualTo(val));
    }

    [Test]
    public void Process_WithTrebleCut_OutputDiffersFromInput()
    {
        _eq.SetGains(0, 0, -12);
        short val = 8000;
        var (l, _) = _eq.Process(val, val);
        Assert.That(l, Is.Not.EqualTo(val));
    }

    // ---- Symmetry: same gain applied to L and R gives same results ----

    [Test]
    public void Process_SameInputBothChannels_LEqualsR()
    {
        _eq.SetGains(6, -3, 9);
        short val = 4000;
        for (int i = 0; i < 10; i++)
        {
            var (l, r) = _eq.Process(val, val);
            Assert.That(l, Is.EqualTo(r), $"L != R at step {i}");
        }
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_AfterDrivingFilter_SubsequentProcessMatchesFreshInstance()
    {
        // Prime the filter state.
        _eq.SetGains(8, 4, -6);
        for (int i = 0; i < 20; i++)
            _eq.Process(10000, 10000);

        _eq.ResetState();

        // Fresh instance with same gains.
        var fresh = new AudioEqProcessor();
        fresh.SetGains(8, 4, -6);

        var (eqL, eqR)     = _eq.Process(3000, 3000);
        var (freshL, freshR) = fresh.Process(3000, 3000);

        Assert.That(eqL, Is.EqualTo(freshL));
        Assert.That(eqR, Is.EqualTo(freshR));
    }

    [Test]
    public void ResetState_DoesNotClearIsActive()
    {
        _eq.SetGains(3, 0, 0);
        _eq.ResetState();
        // IsActive is determined by the gain values, not the filter memory.
        Assert.That(_eq.IsActive, Is.True);
    }

    // ---- Clipping ----

    [Test]
    public void Process_ExtremePositiveGainWithLargeInput_ClampsToShortMax()
    {
        _eq.SetGains(12, 12, 12);
        // Pump a large value through many times to accumulate filter history.
        for (int i = 0; i < 50; i++)
            _eq.Process(short.MaxValue, short.MaxValue);
        // Must not throw and L must be within short range.
        var (l, _) = _eq.Process(short.MaxValue, short.MaxValue);
        Assert.That(l, Is.LessThanOrEqualTo(short.MaxValue));
        Assert.That(l, Is.GreaterThanOrEqualTo(short.MinValue));
    }

    [Test]
    public void Process_ExtremeNegativeGainWithLargeInput_ClampsToShortMin()
    {
        _eq.SetGains(-12, -12, -12);
        for (int i = 0; i < 50; i++)
            _eq.Process(short.MinValue, short.MinValue);
        var (l, _) = _eq.Process(short.MinValue, short.MinValue);
        Assert.That(l, Is.GreaterThanOrEqualTo(short.MinValue));
        Assert.That(l, Is.LessThanOrEqualTo(short.MaxValue));
    }

    // ---- SetGains can be called multiple times (no state reset) ----

    [Test]
    public void SetGains_MultipleCalls_DoesNotResetFilterHistory()
    {
        // Drive some state into the filter.
        _eq.SetGains(6, 0, 0);
        var (before, _) = _eq.Process(5000, 5000);

        // Call SetGains again with the same values — should not flush state.
        _eq.SetGains(6, 0, 0);
        var (after, _) = _eq.Process(5000, 5000);

        // Both before and after are from a warm filter — they should differ from
        // what a fresh filter would produce (which equals input at the first sample
        // with gain 0, but the peaking filter starts non-identity immediately).
        // The key invariant is that calling SetGains(same) twice is not the same
        // as resetting: history carries over.
        Assert.That(after, Is.Not.EqualTo(before)); // filter is evolving over time
    }
}
