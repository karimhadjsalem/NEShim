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

    // ---- ActionToNesButton table ----

    [Test]
    public void ActionToNesButton_ContainsEightEntries()
    {
        Assert.That(SteamInputManager.ActionToNesButton, Has.Count.EqualTo(8));
    }

    [TestCase("up",       "P1 Up")]
    [TestCase("down",     "P1 Down")]
    [TestCase("left",     "P1 Left")]
    [TestCase("right",    "P1 Right")]
    [TestCase("a_button", "P1 A")]
    [TestCase("b_button", "P1 B")]
    [TestCase("start",    "P1 Start")]
    [TestCase("select",   "P1 Select")]
    public void ActionToNesButton_CorrectMapping(string action, string nesButton)
    {
        Assert.That(SteamInputManager.ActionToNesButton[action], Is.EqualTo(nesButton));
    }

    // ---- NesButtonToAction table ----

    [Test]
    public void NesButtonToAction_ContainsEightEntries()
    {
        Assert.That(SteamInputManager.NesButtonToAction, Has.Count.EqualTo(8));
    }

    [TestCase("P1 Up",     "up")]
    [TestCase("P1 Down",   "down")]
    [TestCase("P1 Left",   "left")]
    [TestCase("P1 Right",  "right")]
    [TestCase("P1 A",      "a_button")]
    [TestCase("P1 B",      "b_button")]
    [TestCase("P1 Start",  "start")]
    [TestCase("P1 Select", "select")]
    public void NesButtonToAction_CorrectMapping(string nesButton, string action)
    {
        Assert.That(SteamInputManager.NesButtonToAction[nesButton], Is.EqualTo(action));
    }

    [Test]
    public void ActionAndNesButton_Tables_AreSymmetric()
    {
        foreach (var (action, nesButton) in SteamInputManager.ActionToNesButton)
            Assert.That(SteamInputManager.NesButtonToAction[nesButton], Is.EqualTo(action));
    }
}
