using NEShim.GameLoop;

namespace NEShim.Tests.GameLoop;

/// <summary>
/// Type-level tests for EmulationThread.PauseReasons.
/// These verify the [Flags] enum contract without instantiating EmulationThread.
/// </summary>
[TestFixture]
internal class PauseReasonsTests
{
    [Test]
    public void None_ValueIsZero()
    {
        Assert.That((int)EmulationThread.PauseReasons.None, Is.EqualTo(0));
    }

    [Test]
    public void Menu_ValueIsPositivePowerOfTwo()
    {
        int v = (int)EmulationThread.PauseReasons.Menu;
        Assert.That(v, Is.GreaterThan(0));
        Assert.That((v & (v - 1)), Is.EqualTo(0));
    }

    [Test]
    public void Overlay_ValueIsPositivePowerOfTwo()
    {
        int v = (int)EmulationThread.PauseReasons.Overlay;
        Assert.That(v, Is.GreaterThan(0));
        Assert.That((v & (v - 1)), Is.EqualTo(0));
    }

    [Test]
    public void FocusLost_ValueIsPositivePowerOfTwo()
    {
        int v = (int)EmulationThread.PauseReasons.FocusLost;
        Assert.That(v, Is.GreaterThan(0));
        Assert.That((v & (v - 1)), Is.EqualTo(0));
    }

    [Test]
    public void MainMenu_ValueIsPositivePowerOfTwo()
    {
        int v = (int)EmulationThread.PauseReasons.MainMenu;
        Assert.That(v, Is.GreaterThan(0));
        Assert.That((v & (v - 1)), Is.EqualTo(0));
    }

    [Test]
    public void AllNonNoneValues_AreDistinct()
    {
        var values = new[]
        {
            EmulationThread.PauseReasons.Menu,
            EmulationThread.PauseReasons.Overlay,
            EmulationThread.PauseReasons.FocusLost,
            EmulationThread.PauseReasons.MainMenu,
        };
        Assert.That(values.Distinct().Count(), Is.EqualTo(values.Length));
    }

    [Test]
    public void AllNonNoneValues_HaveNoOverlappingBits()
    {
        var values = new[]
        {
            (int)EmulationThread.PauseReasons.Menu,
            (int)EmulationThread.PauseReasons.Overlay,
            (int)EmulationThread.PauseReasons.FocusLost,
            (int)EmulationThread.PauseReasons.MainMenu,
        };
        for (int i = 0; i < values.Length; i++)
        for (int j = i + 1; j < values.Length; j++)
            Assert.That(values[i] & values[j], Is.EqualTo(0),
                $"Bit overlap between {(EmulationThread.PauseReasons)values[i]} and {(EmulationThread.PauseReasons)values[j]}");
    }

    [Test]
    public void CombinedFlags_ContainsBothIndividualBits()
    {
        var combined = EmulationThread.PauseReasons.Menu | EmulationThread.PauseReasons.Overlay;
        Assert.That(combined.HasFlag(EmulationThread.PauseReasons.Menu),    Is.True);
        Assert.That(combined.HasFlag(EmulationThread.PauseReasons.Overlay), Is.True);
    }

    [Test]
    public void CombinedFlags_DoesNotContainUnsetBit()
    {
        var combined = EmulationThread.PauseReasons.Menu | EmulationThread.PauseReasons.Overlay;
        Assert.That(combined.HasFlag(EmulationThread.PauseReasons.FocusLost), Is.False);
    }

    [Test]
    public void RemovingFlag_LeavesOtherBitsIntact()
    {
        var combined = EmulationThread.PauseReasons.Menu | EmulationThread.PauseReasons.FocusLost;
        var cleared  = combined & ~EmulationThread.PauseReasons.Menu;
        Assert.That(cleared.HasFlag(EmulationThread.PauseReasons.FocusLost), Is.True);
        Assert.That(cleared.HasFlag(EmulationThread.PauseReasons.Menu),      Is.False);
    }

    [Test]
    public void None_HasNoOverlapWithAnyOtherFlag()
    {
        var all = EmulationThread.PauseReasons.Menu
                | EmulationThread.PauseReasons.Overlay
                | EmulationThread.PauseReasons.FocusLost
                | EmulationThread.PauseReasons.MainMenu;
        Assert.That((EmulationThread.PauseReasons.None & all), Is.EqualTo(EmulationThread.PauseReasons.None));
    }

    [Test]
    public void ZeroBitMask_MeansNotPaused()
    {
        // The emulation thread treats _pauseReasonBits != 0 as paused.
        // Verify None evaluates as 0 in a boolean test.
        int bits = (int)EmulationThread.PauseReasons.None;
        Assert.That(bits != 0, Is.False);
    }
}
