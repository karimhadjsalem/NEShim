using NEShim.Config;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class MenuBindingHelpersTests
{
    private AppConfig _config = null!;

    [SetUp]
    public void SetUp() => _config = new AppConfig();

    // ── SetGamepadBinding: cross-action duplicate prevention ────────────────────

    [Test]
    public void SetGamepadBinding_ClearsSameButtonFromOtherActions_GamepadButton()
    {
        // DPadUp is the default GamepadButton for P1 Up; rebinding P1 Down to DPadUp
        // must clear it from P1 Up.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "DPadUp");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.Null);
        Assert.That(_config.InputMappings["P1 Down"].GamepadButton, Is.EqualTo("DPadUp"));
    }

    [Test]
    public void SetGamepadBinding_ClearsSameButtonFromOtherActions_GamepadButton2()
    {
        // AnalogUp is the default GamepadButton2 for P1 Up; rebinding P1 Down to AnalogUp
        // must clear it from P1 Up.GamepadButton2 so P1 Up stops responding to AnalogUp.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "AnalogUp");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton2, Is.Null,
            "AnalogUp must be cleared from P1 Up.GamepadButton2 to prevent double-binding");
    }

    // ── SetGamepadBinding: same-action cross-axis prevention ────────────────────

    [Test]
    public void SetGamepadBinding_AnalogIdentifier_ClearsGamepadButton2OnSameAction()
    {
        // Default: P1 Down.GamepadButton2 = "AnalogDown".
        // If user rebinds P1 Down.GamepadButton to "AnalogUp", GamepadButton2 must be cleared
        // so P1 Down doesn't fire for both AnalogUp (primary) and AnalogDown (secondary).
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "AnalogUp");

        Assert.That(_config.InputMappings["P1 Down"].GamepadButton,  Is.EqualTo("AnalogUp"));
        Assert.That(_config.InputMappings["P1 Down"].GamepadButton2, Is.Null,
            "Secondary analog slot must be cleared when primary is set to an analog identifier");
    }

    [Test]
    public void SetGamepadBinding_AnalogDown_ClearsGamepadButton2OnSameAction()
    {
        // Same-axis rebind: P1 Down.GamepadButton2 = "AnalogDown" by default.
        // Explicitly binding GamepadButton to "AnalogDown" should also clear GamepadButton2
        // (becomes redundant; keeps state clean).
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "AnalogDown");

        Assert.That(_config.InputMappings["P1 Down"].GamepadButton2, Is.Null);
    }

    // ── SetGamepadBinding: non-analog primary preserves GamepadButton2 ──────────

    [Test]
    public void SetGamepadBinding_NonAnalogButton_PreservesGamepadButton2()
    {
        // Rebinding P1 Up's primary button to a face button should keep the default
        // AnalogUp secondary binding so the analog stick still triggers P1 Up.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Up", "A");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton,  Is.EqualTo("A"));
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton2, Is.EqualTo("AnalogUp"),
            "Analog secondary slot must survive a non-analog primary rebind");
    }

    [Test]
    public void SetGamepadBinding_DPadButton_PreservesGamepadButton2()
    {
        // Re-confirming DPadDown as the primary button for P1 Down should not erase
        // the AnalogDown secondary slot.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "DPadDown");

        Assert.That(_config.InputMappings["P1 Down"].GamepadButton2, Is.EqualTo("AnalogDown"));
    }

    // ── SetBinding: keyboard duplicate prevention ───────────────────────────────

    [Test]
    public void SetBinding_ClearsSameKeyFromOtherActions()
    {
        // "W" is default Key for P1 Up; rebinding P1 Down to "W" must clear it from P1 Up.
        MenuBindingHelpers.SetBinding(_config, "P1 Down", "W");

        Assert.That(_config.InputMappings["P1 Up"].Key,   Is.Null);
        Assert.That(_config.InputMappings["P1 Down"].Key, Is.EqualTo("W"));
    }

    [Test]
    public void SetBinding_DoesNotClearGamepadSlots()
    {
        // Keyboard rebind must leave GamepadButton and GamepadButton2 intact.
        MenuBindingHelpers.SetBinding(_config, "P1 Up", "Up");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton,  Is.EqualTo("DPadUp"));
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton2, Is.EqualTo("AnalogUp"));
    }
}
