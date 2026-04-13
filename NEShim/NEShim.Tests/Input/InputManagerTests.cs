using System.Windows.Forms;
using NEShim.Config;
using NEShim.Input;

namespace NEShim.Tests.Input;

[TestFixture]
internal class InputManagerTests
{
    private InputManager _manager = null!;
    private AppConfig    _config  = null!;

    [SetUp]
    public void SetUp()
    {
        _manager = new InputManager();
        _config  = new AppConfig(); // has default hotkey mappings (OpenMenu→Escape, etc.)
    }

    // ---- Key state ----

    [Test]
    public void OnKeyDown_ThenOnKeyUp_KeyIsNoLongerTracked()
    {
        // Verify via PollSnapshot: W maps to P1 Up by default
        _manager.OnKeyDown(Keys.W);
        var before = _manager.PollSnapshot(_config);
        Assert.That(before.IsPressed("P1 Up"), Is.True);

        _manager.OnKeyUp(Keys.W);
        var after = _manager.PollSnapshot(_config);
        Assert.That(after.IsPressed("P1 Up"), Is.False);
    }

    [Test]
    public void PollSnapshot_MapsKeyToNesButton()
    {
        // Default: P1 Up → W key
        _manager.OnKeyDown(Keys.W);
        var snapshot = _manager.PollSnapshot(_config);
        Assert.That(snapshot.IsPressed("P1 Up"), Is.True);
    }

    [Test]
    public void PollSnapshot_UnpressedKey_ButtonNotInSnapshot()
    {
        // No key held — P1 Up should not be pressed
        var snapshot = _manager.PollSnapshot(_config);
        Assert.That(snapshot.IsPressed("P1 Up"), Is.False);
    }

    [Test]
    public void PollSnapshot_MultipleKeysMapped_AllAppearInSnapshot()
    {
        _manager.OnKeyDown(Keys.W);          // P1 Up
        _manager.OnKeyDown(Keys.S);          // P1 Down
        _manager.OnKeyDown(Keys.Return);     // P1 Start

        var snapshot = _manager.PollSnapshot(_config);
        Assert.That(snapshot.IsPressed("P1 Up"),    Is.True);
        Assert.That(snapshot.IsPressed("P1 Down"),  Is.True);
        Assert.That(snapshot.IsPressed("P1 Start"), Is.True);
    }

    // ---- Hotkey edge detection ----

    [Test]
    public void IsHotkeyJustPressed_KeyPressedThisFrame_ReturnsTrue()
    {
        _manager.OnKeyDown(Keys.Escape); // OpenMenu → Escape
        bool result = _manager.IsHotkeyJustPressed("OpenMenu", _config);
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsHotkeyJustPressed_KeyHeldAcrossFrames_ReturnsFalseOnSecondFrame()
    {
        _manager.OnKeyDown(Keys.Escape);
        _manager.IsHotkeyJustPressed("OpenMenu", _config); // consume first press
        _manager.AdvanceHotkeyState();

        bool secondFrame = _manager.IsHotkeyJustPressed("OpenMenu", _config);
        Assert.That(secondFrame, Is.False);
    }

    [Test]
    public void IsHotkeyJustPressed_KeyNotDown_ReturnsFalse()
    {
        bool result = _manager.IsHotkeyJustPressed("OpenMenu", _config);
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsHotkeyJustPressed_UnknownAction_ReturnsFalse()
    {
        _manager.OnKeyDown(Keys.Escape);
        bool result = _manager.IsHotkeyJustPressed("NonExistentAction", _config);
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsHotkeyJustPressed_ActionWithBadKeyName_ReturnsFalse()
    {
        var config = new AppConfig();
        config.HotkeyMappings["TestAction"] = "NotAValidKey!!!";
        bool result = _manager.IsHotkeyJustPressed("TestAction", config);
        Assert.That(result, Is.False);
    }

    [Test]
    public void AdvanceHotkeyState_AfterKeyUp_SubsequentFrameNotDetected()
    {
        _manager.OnKeyDown(Keys.F5);        // SaveActiveSlot → F5
        _manager.IsHotkeyJustPressed("SaveActiveSlot", _config);
        _manager.AdvanceHotkeyState();

        _manager.OnKeyUp(Keys.F5);
        _manager.AdvanceHotkeyState();

        // Key released — should not fire
        bool result = _manager.IsHotkeyJustPressed("SaveActiveSlot", _config);
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsHotkeyJustPressed_DifferentActionsIndependent()
    {
        _manager.OnKeyDown(Keys.F5);   // SaveActiveSlot
        _manager.OnKeyDown(Keys.F9);   // LoadActiveSlot

        bool save = _manager.IsHotkeyJustPressed("SaveActiveSlot", _config);
        bool load = _manager.IsHotkeyJustPressed("LoadActiveSlot", _config);
        Assert.That(save, Is.True);
        Assert.That(load, Is.True);
    }
}
