using NEShim.Emulation;

namespace NEShim.Tests.Emulation;

/// <summary>
/// Table-driven coverage of the hand-derived PlayerCount → port-wiring table (see
/// NesPortSelector's doc comment for the ControllerDefinitionMerger trace this mirrors). This
/// pure function is not itself proof the BizHawk core allocates the expected "P{n} …" button
/// names for each wiring — see the BizHawk-facing integration test for that corroborating check.
/// </summary>
[TestFixture]
internal class NesPortSelectorTests
{
    [Test]
    public void ForPlayerCount_1_UsesControllerAndUnplugged()
    {
        var (left, right) = NesPortSelector.ForPlayerCount(1);
        Assert.That(left,  Is.EqualTo("ControllerNES"));
        Assert.That(right, Is.EqualTo("UnpluggedNES"));
    }

    [Test]
    public void ForPlayerCount_2_UsesControllerBothPorts()
    {
        var (left, right) = NesPortSelector.ForPlayerCount(2);
        Assert.That(left,  Is.EqualTo("ControllerNES"));
        Assert.That(right, Is.EqualTo("ControllerNES"));
    }

    [Test]
    public void ForPlayerCount_3_UsesFourScoreBothPorts()
    {
        // Not FourScore/ControllerNES: a real Four Score must be attached to BOTH ports to emit
        // both signature halves games check for before they'll read the "chained" extra-clock
        // data — where player 2 lives. A single-port FourScore silently drops player 2 even
        // though its config-key/button-name exists in the merged definition (see NesPortSelector's
        // doc comment and NesPortSelectorControllerDefinitionMergerTests for the corroborating
        // BizHawk-level check). The unused P4 slot this allocates is harmless.
        var (left, right) = NesPortSelector.ForPlayerCount(3);
        Assert.That(left,  Is.EqualTo("FourScore"));
        Assert.That(right, Is.EqualTo("FourScore"));
    }

    [Test]
    public void ForPlayerCount_4_UsesFourScoreBothPorts()
    {
        var (left, right) = NesPortSelector.ForPlayerCount(4);
        Assert.That(left,  Is.EqualTo("FourScore"));
        Assert.That(right, Is.EqualTo("FourScore"));
    }

    [Test]
    public void ForPlayerCount_ZeroOrNegative_TreatedAsOnePlayer()
    {
        // Defensive floor — ConfigLoader already clamps PlayerCount to [1,4] at load time, but
        // this pure function stays correct even if called directly with an out-of-range value.
        var (left, right) = NesPortSelector.ForPlayerCount(0);
        Assert.That(left,  Is.EqualTo("ControllerNES"));
        Assert.That(right, Is.EqualTo("UnpluggedNES"));
    }

    [Test]
    public void ForPlayerCount_AboveFour_TreatedAsFourPlayers()
    {
        var (left, right) = NesPortSelector.ForPlayerCount(99);
        Assert.That(left,  Is.EqualTo("FourScore"));
        Assert.That(right, Is.EqualTo("FourScore"));
    }
}
