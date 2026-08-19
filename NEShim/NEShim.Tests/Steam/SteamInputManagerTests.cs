using NEShim.Steam;

namespace NEShim.Tests.Steam;

/// <summary>
/// Tests for SteamInputManager paths that are reachable when Steam is not available.
/// IsAvailable is false in every test environment (Steam not running); every method that
/// gates on IsAvailable has a no-op / fallback early-return that is safe to call.
/// </summary>
[TestFixture]
internal class SteamInputManagerTests
{
    // ---- IsAvailable / HasConnectedController ----

    [Test]
    public void IsAvailable_IsFalse_InTestEnvironment()
    {
        Assert.That(SteamInputManager.IsAvailable, Is.False);
    }

    [Test]
    public void HasConnectedController_IsFalse_WhenUnavailable()
    {
        Assert.That(SteamInputManager.HasConnectedController, Is.False);
    }

    [Test]
    public void IsUsingNativeActions_ReturnsFalse_WhenUnavailable()
    {
        Assert.That(SteamInputManager.IsUsingNativeActions(), Is.False);
    }

    // ---- No-op lifecycle calls ----

    [Test]
    public void Shutdown_WhenUnavailable_DoesNotThrow()
    {
        Assert.That(() => SteamInputManager.Shutdown(), Throws.Nothing);
    }

    [Test]
    public void ActivateGameplaySet_WhenUnavailable_DoesNotThrow()
    {
        Assert.That(() => SteamInputManager.ActivateGameplaySet(), Throws.Nothing);
    }

    [Test]
    public void ActivateMenuSet_WhenUnavailable_DoesNotThrow()
    {
        Assert.That(() => SteamInputManager.ActivateMenuSet(), Throws.Nothing);
    }

    // ---- Input polling returns empty / default when unavailable ----

    [Test]
    public void GetActiveActions_WhenUnavailable_ReturnsEmptySet()
    {
        Assert.That(SteamInputManager.GetActiveActions(), Is.Empty);
    }

    [Test]
    public void GetMenuNav_WhenUnavailable_ReturnsAllFalseNav()
    {
        var nav = SteamInputManager.GetMenuNav();
        Assert.That(nav.Any, Is.False);
    }

    // ---- GetNativeLabel: formatted fallback when Steam not available ----

    [TestCase("up",       "Up")]
    [TestCase("down",     "Down")]
    [TestCase("left",     "Left")]
    [TestCase("right",    "Right")]
    [TestCase("a_button", "A Button")]
    [TestCase("b_button", "B Button")]
    [TestCase("start",    "Start")]
    [TestCase("select",   "Select")]
    public void GetNativeLabel_WhenUnavailable_ReturnsFormattedName(string action, string expected)
    {
        Assert.That(SteamInputManager.GetNativeLabel(action), Is.EqualTo(expected));
    }

    [Test]
    public void GetNativeLabel_UnknownAction_ReturnsActionNameAsIs()
    {
        Assert.That(SteamInputManager.GetNativeLabel("mystery_action"), Is.EqualTo("mystery_action"));
    }

    // ---- NesButtonFor / ActionFor: player-parameterized action <-> NES button translation
    // (replaces the old fixed P1-only ActionToNesButton/NesButtonToAction dictionaries) ----

    [TestCase("up",       1, "P1 Up")]
    [TestCase("down",     1, "P1 Down")]
    [TestCase("left",     1, "P1 Left")]
    [TestCase("right",    1, "P1 Right")]
    [TestCase("a_button", 1, "P1 A")]
    [TestCase("b_button", 1, "P1 B")]
    [TestCase("start",    1, "P1 Start")]
    [TestCase("select",   1, "P1 Select")]
    [TestCase("up",       2, "P2 Up")]
    [TestCase("select",   4, "P4 Select")]
    public void NesButtonFor_CorrectMapping(string action, int player, string expected)
    {
        Assert.That(SteamInputManager.NesButtonFor(action, player), Is.EqualTo(expected));
    }

    [Test]
    public void NesButtonFor_UnknownAction_ReturnsNull()
    {
        Assert.That(SteamInputManager.NesButtonFor("mystery_action", 1), Is.Null);
    }

    [TestCase("P1 Up",     "up")]
    [TestCase("P1 Down",   "down")]
    [TestCase("P1 Left",   "left")]
    [TestCase("P1 Right",  "right")]
    [TestCase("P1 A",      "a_button")]
    [TestCase("P1 B",      "b_button")]
    [TestCase("P1 Start",  "start")]
    [TestCase("P1 Select", "select")]
    [TestCase("P2 Up",     "up")]
    [TestCase("P4 Select", "select")]
    public void ActionFor_CorrectMapping(string nesButton, string expected)
    {
        Assert.That(SteamInputManager.ActionFor(nesButton), Is.EqualTo(expected));
    }

    [Test]
    public void ActionFor_UnrecognizedSuffix_ReturnsNull()
    {
        Assert.That(SteamInputManager.ActionFor("P1 NotAButton"), Is.Null);
    }

    [Test]
    public void ActionFor_NoSpace_ReturnsNull()
    {
        Assert.That(SteamInputManager.ActionFor("OpenMenu"), Is.Null);
    }

    [TestCase("up", "down", "left", "right", "a_button", "b_button", "start", "select")]
    public void NesButtonForThenActionFor_RoundTrips(params string[] actions)
    {
        foreach (var action in actions)
            Assert.That(SteamInputManager.ActionFor(SteamInputManager.NesButtonFor(action, 3)!), Is.EqualTo(action));
    }

    // ---- AnyMenuActionActive / GetMenuHeldLeftRight ----

    [Test]
    public void AnyMenuActionActive_WhenUnavailable_ReturnsFalse()
    {
        Assert.That(SteamInputManager.AnyMenuActionActive(), Is.False);
    }

    [Test]
    public void GetMenuHeldLeftRight_WhenUnavailable_ReturnsBothFalse()
    {
        var (left, right) = SteamInputManager.GetMenuHeldLeftRight();
        Assert.That(left,  Is.False);
        Assert.That(right, Is.False);
    }

    // ---- IsTouchpadOrGyroOrigin: pure predicate driving the refined IsUsingNativeActions gate.
    // Takes the origin's name (string), not the Steamworks enum, so it's testable without a
    // Steamworks assembly reference from this test project — see its own doc comment. ----

    [TestCase("k_EInputActionOrigin_SteamController_LeftPad_Click")]
    [TestCase("k_EInputActionOrigin_SteamController_RightPad_Touch")]
    [TestCase("k_EInputActionOrigin_SteamController_Gyro_Move")]
    [TestCase("k_EInputActionOrigin_PS4_CenterPad_Click")]
    public void IsTouchpadOrGyroOrigin_TrackpadOrGyroOrigins_ReturnsTrue(string originName)
    {
        Assert.That(SteamInputManager.IsTouchpadOrGyroOrigin(originName), Is.True);
    }

    [TestCase("k_EInputActionOrigin_XBoxOne_A")]
    [TestCase("k_EInputActionOrigin_XBoxOne_DPad_North")]
    [TestCase("k_EInputActionOrigin_PS4_X")]
    [TestCase("k_EInputActionOrigin_None")]
    public void IsTouchpadOrGyroOrigin_RegularButtonOrDpadOrigins_ReturnsFalse(string originName)
    {
        Assert.That(SteamInputManager.IsTouchpadOrGyroOrigin(originName), Is.False);
    }
}
