namespace NEShim.Emulation;

/// <summary>
/// Pure, BizHawk-independent decision: which NES controller ports to wire up for a given
/// <c>AppConfig.PlayerCount</c>. The vendored BizHawk NES core already fully emulates the
/// hardware Four Score / Satellite adapter (see <c>NESControllers.cs</c>'s <c>FourScore</c>) —
/// no core changes are needed, only picking the right port-config strings.
///
/// <c>BizHawk.Emulation.Common.ControllerDefinitionMerger.Allocate</c> walks each plugged port's
/// button-name fragments left-to-right and allocates them to "P{n}" in order, advancing past
/// however many players the fragment itself represents before merging the next port:
///   1 player  → ControllerNES / UnpluggedNES  → P1 only (today's exact single-player wiring)
///   2 players → ControllerNES / ControllerNES → P1, P2
///   3 players → FourScore     / FourScore     → P1, P2, P3 (P4's slot allocated but never fed
///               input by NEShim's input layer — see below for why this can't be trimmed to
///               FourScore/ControllerNES the way the equivalent button-count table might suggest)
///   4 players → FourScore     / FourScore     → P1, P2, P3, P4
///
/// <b>3 players must still wire FourScore into BOTH ports, not just the left one.</b> A real Four
/// Score is one physical device spanning both NES controller ports simultaneously — each port's
/// electrical half emits its own signature nibble (<c>FourScore.Latch</c>: <c>0x08</c> on the
/// port with <c>RightPort = false</c>, <c>0x04</c> on the one with <c>RightPort = true</c>), and a
/// Four-Score-aware game detects the adapter by checking that BOTH signatures are present before
/// it will read the extra "chained" 8 bits each port carries beyond the normal first 8 (that
/// chained data is where the 2nd/4th controller's input lives — see the class doc comment on
/// <c>FourScore</c>: "one two port thing... we emulate it as two separate halves"). Wiring
/// <c>(FourScore, ControllerNES)</c> for 3 players — the seemingly tidier choice, since it avoids
/// allocating an unused P4 button set — leaves the right port emitting no signature at all, so
/// Four-Score-detection fails and the game never reads the chained data on the LEFT port either,
/// silently dropping player 2 (real regression, reproduced live: player 2's gamepad correctly
/// captured rebinds in the UI, since that path never touches BizHawk, but pressed buttons never
/// reached the game). <c>(FourScore, FourScore)</c> is correct for both 3 and 4 players; the
/// unused 4th slot when <c>PlayerCount == 3</c> is exactly as harmless as a real Four Score with
/// nothing plugged into its 4th port — NEShim's own input layer simply never constructs a 4th
/// player's input source to feed it (see <c>NEShimApp.InitializeInput</c>).
///
/// Extracted as a pure function — mirrors <see cref="Rendering.RenderPassPlanner"/>'s "pure
/// decision function extracted for testability" precedent — so <see cref="BizHawkEmulationCore"/>
/// stays a thin caller and the port-selection rule itself is unit-testable without constructing a
/// real NES core. See <c>NesPortSelectorTests</c>/the BizHawk-facing integration test for the
/// corroborating check against the real merger.
/// </summary>
internal static class NesPortSelector
{
    public static (string NesLeftPort, string NesRightPort) ForPlayerCount(int playerCount) => playerCount switch
    {
        <= 1 => ("ControllerNES", "UnpluggedNES"),
        2    => ("ControllerNES", "ControllerNES"),
        _    => ("FourScore",     "FourScore"), // 3 or 4 players — see class doc comment for why
                                                 // 3 players can't drop to a single FourScore port.
    };
}
