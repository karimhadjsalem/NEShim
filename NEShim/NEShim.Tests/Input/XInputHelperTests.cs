using NEShim.Input;

namespace NEShim.Tests.Input;

/// <summary>
/// Tests for XInputHelper.GetButton — the only path testable without physical hardware.
/// GetButton is a pure switch over a value-type struct; all arms can be exercised by
/// constructing GamepadState directly.
/// </summary>
[TestFixture]
internal class XInputHelperTests
{
    private static XInputHelper.GamepadState AllFalse() => new();

    // ---- Null / unknown names ----

    [Test]
    public void GetButton_Null_ReturnsFalse()
    {
        var s = AllFalse();
        Assert.That(XInputHelper.GetButton(in s, null), Is.False);
    }

    [Test]
    public void GetButton_UnknownName_ReturnsFalse()
    {
        var s = new XInputHelper.GamepadState { A = true };
        Assert.That(XInputHelper.GetButton(in s, "ZButton"), Is.False);
    }

    // ---- Each button returns true when that field is set ----

    [Test]
    public void GetButton_A_True()
    {
        var s = new XInputHelper.GamepadState { A = true };
        Assert.That(XInputHelper.GetButton(in s, "A"), Is.True);
    }

    [Test]
    public void GetButton_B_True()
    {
        var s = new XInputHelper.GamepadState { B = true };
        Assert.That(XInputHelper.GetButton(in s, "B"), Is.True);
    }

    [Test]
    public void GetButton_X_True()
    {
        var s = new XInputHelper.GamepadState { X = true };
        Assert.That(XInputHelper.GetButton(in s, "X"), Is.True);
    }

    [Test]
    public void GetButton_Y_True()
    {
        var s = new XInputHelper.GamepadState { Y = true };
        Assert.That(XInputHelper.GetButton(in s, "Y"), Is.True);
    }

    [Test]
    public void GetButton_Start_True()
    {
        var s = new XInputHelper.GamepadState { Start = true };
        Assert.That(XInputHelper.GetButton(in s, "Start"), Is.True);
    }

    [Test]
    public void GetButton_Back_True()
    {
        var s = new XInputHelper.GamepadState { Back = true };
        Assert.That(XInputHelper.GetButton(in s, "Back"), Is.True);
    }

    [Test]
    public void GetButton_LeftShoulder_True()
    {
        var s = new XInputHelper.GamepadState { LeftShoulder = true };
        Assert.That(XInputHelper.GetButton(in s, "LeftShoulder"), Is.True);
    }

    [Test]
    public void GetButton_RightShoulder_True()
    {
        var s = new XInputHelper.GamepadState { RightShoulder = true };
        Assert.That(XInputHelper.GetButton(in s, "RightShoulder"), Is.True);
    }

    [Test]
    public void GetButton_LeftThumb_True()
    {
        var s = new XInputHelper.GamepadState { LeftThumb = true };
        Assert.That(XInputHelper.GetButton(in s, "LeftThumb"), Is.True);
    }

    [Test]
    public void GetButton_RightThumb_True()
    {
        var s = new XInputHelper.GamepadState { RightThumb = true };
        Assert.That(XInputHelper.GetButton(in s, "RightThumb"), Is.True);
    }

    [Test]
    public void GetButton_DPadUp_True()
    {
        var s = new XInputHelper.GamepadState { DPadUp = true };
        Assert.That(XInputHelper.GetButton(in s, "DPadUp"), Is.True);
    }

    [Test]
    public void GetButton_DPadDown_True()
    {
        var s = new XInputHelper.GamepadState { DPadDown = true };
        Assert.That(XInputHelper.GetButton(in s, "DPadDown"), Is.True);
    }

    [Test]
    public void GetButton_DPadLeft_True()
    {
        var s = new XInputHelper.GamepadState { DPadLeft = true };
        Assert.That(XInputHelper.GetButton(in s, "DPadLeft"), Is.True);
    }

    [Test]
    public void GetButton_DPadRight_True()
    {
        var s = new XInputHelper.GamepadState { DPadRight = true };
        Assert.That(XInputHelper.GetButton(in s, "DPadRight"), Is.True);
    }

    // ---- Each button returns false when that field is false ----

    [Test]
    public void GetButton_A_ReturnsFalse_WhenAIsFalse()
    {
        var s = AllFalse();
        Assert.That(XInputHelper.GetButton(in s, "A"), Is.False);
    }

    [Test]
    public void GetButton_DPadUp_ReturnsFalse_WhenDPadUpIsFalse()
    {
        var s = AllFalse();
        Assert.That(XInputHelper.GetButton(in s, "DPadUp"), Is.False);
    }

    [Test]
    public void GetButton_OnlyNamedButton_ReturnsTrue_OthersReturnFalse()
    {
        var s = new XInputHelper.GamepadState { Y = true };
        Assert.That(XInputHelper.GetButton(in s, "Y"), Is.True);
        Assert.That(XInputHelper.GetButton(in s, "A"), Is.False);
        Assert.That(XInputHelper.GetButton(in s, "Start"), Is.False);
    }
}
