using System.Linq;
using BizHawk.Emulation.Cores.Nintendo.NES;
using NEShim.Emulation;

namespace NEShim.Tests.Emulation;

/// <summary>
/// Confirms NesPortSelector.ForPlayerCount's hand-derived port-wiring table actually allocates
/// the expected "P{n} …" button names when run through the real BizHawk
/// ControllerDefinitionMerger/NesDeck — not just traced by reading the algorithm (see
/// NesPortSelectorTests, and NesPortSelector's own doc comment). No ROM/CoreComm/full NES core is
/// needed: NesDeck merges controller-port fragments directly from the same INesPort
/// implementations (ControllerNES/UnpluggedNES/FourScore) BizHawkEmulationCore.LoadRom wires up,
/// so this is pure in-memory object construction — no file/hardware boundary crossed, hence a
/// unit test rather than an Integration/ one despite exercising vendored BizHawk classes.
/// </summary>
[TestFixture]
internal class NesPortSelectorControllerDefinitionMergerTests
{
    private static INesPort CreatePort(string name) => name switch
    {
        "ControllerNES" => new ControllerNES(),
        "UnpluggedNES"  => new UnpluggedNES(),
        "FourScore"     => new FourScore(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static string[] AllocatedNesButtons(int playerCount)
    {
        var (leftName, rightName) = NesPortSelector.ForPlayerCount(playerCount);
        var deck = new NesDeck(CreatePort(leftName), CreatePort(rightName), (_, _) => false);
        return deck.ControllerDef.BoolButtons
            .Where(b => b.StartsWith("P", StringComparison.Ordinal) && b.Length >= 2 && char.IsDigit(b[1]))
            .Select(b => b[..2]) // "P1 Up" -> "P1"
            .Distinct()
            .OrderBy(p => p)
            .ToArray();
    }

    [Test]
    public void PlayerCount1_AllocatesOnlyP1()
    {
        Assert.That(AllocatedNesButtons(1), Is.EqualTo(new[] { "P1" }));
    }

    [Test]
    public void PlayerCount2_AllocatesP1AndP2()
    {
        Assert.That(AllocatedNesButtons(2), Is.EqualTo(new[] { "P1", "P2" }));
    }

    [Test]
    public void PlayerCount3_AllocatesP1ThroughP4()
    {
        // Wired as FourScore/FourScore (same as 4 players), not FourScore/ControllerNES — a real
        // Four Score must occupy BOTH ports to emit both signature halves a Four-Score-aware game
        // checks before it reads the chained extra-clock data (where player 2 lives). The
        // resulting unused P4 button set is harmless: NEShim's own input layer never constructs a
        // 4th player's input source to feed it when PlayerCount == 3. See NesPortSelectorTests
        // and the PlayerCount3_BothPortsAreFourScore test below for the full story.
        Assert.That(AllocatedNesButtons(3), Is.EqualTo(new[] { "P1", "P2", "P3", "P4" }));
    }

    [Test]
    public void PlayerCount3_BothPortsAreFourScore_SoBothSignatureNibblesArePresent()
    {
        // The actual regression this guards against: FourScore/ControllerNES for 3 players looks
        // tidier (no unused P4 button set) but silently drops player 2, because a real Four Score
        // only gets detected by a game — and only then will it read the "chained" data holding
        // player 2's input — when BOTH ports emit their half of the signature (FourScore.Latch:
        // 0x08 on the left/non-RightPort half, 0x04 on the right/RightPort half). A plain
        // ControllerNES on the right port emits neither, so detection silently fails.
        var (leftName, rightName) = NesPortSelector.ForPlayerCount(3);
        Assert.That(leftName,  Is.EqualTo("FourScore"));
        Assert.That(rightName, Is.EqualTo("FourScore"));
    }

    [Test]
    public void PlayerCount4_AllocatesP1ThroughP4()
    {
        Assert.That(AllocatedNesButtons(4), Is.EqualTo(new[] { "P1", "P2", "P3", "P4" }));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void EveryAllocatedPlayer_HasAllEightNesButtons(int playerCount)
    {
        var (leftName, rightName) = NesPortSelector.ForPlayerCount(playerCount);
        var deck = new NesDeck(CreatePort(leftName), CreatePort(rightName), (_, _) => false);
        var buttons = deck.ControllerDef.BoolButtons;

        for (int player = 1; player <= playerCount; player++)
        {
            foreach (var suffix in new[] { "Up", "Down", "Left", "Right", "A", "B", "Start", "Select" })
                Assert.That(buttons, Has.Member($"P{player} {suffix}"),
                    $"Missing P{player} {suffix} for PlayerCount={playerCount}");
        }
    }
}
