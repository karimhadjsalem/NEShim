using NEShim.Input;

namespace NEShim.Tests.Input;

/// <summary>
/// Tests for GamepadState.GetButton — pure switch over a value-type struct; all arms
/// can be exercised by constructing GamepadState directly without hardware.
/// </summary>
[TestFixture]
internal class GamepadStateTests
{
    private static GamepadState AllFalse() => new();

    // ---- Null / unknown names ----

    [Test]
    public void GetButton_Null_ReturnsFalse()
    {
        var state = AllFalse();
        Assert.That(state.GetButton(null), Is.False);
    }

    [Test]
    public void GetButton_UnknownName_ReturnsFalse()
    {
        var state = new GamepadState { A = true };
        Assert.That(state.GetButton("ZButton"), Is.False);
    }

    // ---- Each button returns true when that field is set ----

    [Test]
    public void GetButton_A_True()
    {
        var state = new GamepadState { A = true };
        Assert.That(state.GetButton("A"), Is.True);
    }

    [Test]
    public void GetButton_B_True()
    {
        var state = new GamepadState { B = true };
        Assert.That(state.GetButton("B"), Is.True);
    }

    [Test]
    public void GetButton_X_True()
    {
        var state = new GamepadState { X = true };
        Assert.That(state.GetButton("X"), Is.True);
    }

    [Test]
    public void GetButton_Y_True()
    {
        var state = new GamepadState { Y = true };
        Assert.That(state.GetButton("Y"), Is.True);
    }

    [Test]
    public void GetButton_Start_True()
    {
        var state = new GamepadState { Start = true };
        Assert.That(state.GetButton("Start"), Is.True);
    }

    [Test]
    public void GetButton_Back_True()
    {
        var state = new GamepadState { Back = true };
        Assert.That(state.GetButton("Back"), Is.True);
    }

    [Test]
    public void GetButton_LeftShoulder_True()
    {
        var state = new GamepadState { LeftShoulder = true };
        Assert.That(state.GetButton("LeftShoulder"), Is.True);
    }

    [Test]
    public void GetButton_RightShoulder_True()
    {
        var state = new GamepadState { RightShoulder = true };
        Assert.That(state.GetButton("RightShoulder"), Is.True);
    }

    [Test]
    public void GetButton_LeftThumb_True()
    {
        var state = new GamepadState { LeftThumb = true };
        Assert.That(state.GetButton("LeftThumb"), Is.True);
    }

    [Test]
    public void GetButton_RightThumb_True()
    {
        var state = new GamepadState { RightThumb = true };
        Assert.That(state.GetButton("RightThumb"), Is.True);
    }

    [Test]
    public void GetButton_DPadUp_True()
    {
        var state = new GamepadState { DPadUp = true };
        Assert.That(state.GetButton("DPadUp"), Is.True);
    }

    [Test]
    public void GetButton_DPadDown_True()
    {
        var state = new GamepadState { DPadDown = true };
        Assert.That(state.GetButton("DPadDown"), Is.True);
    }

    [Test]
    public void GetButton_DPadLeft_True()
    {
        var state = new GamepadState { DPadLeft = true };
        Assert.That(state.GetButton("DPadLeft"), Is.True);
    }

    [Test]
    public void GetButton_DPadRight_True()
    {
        var state = new GamepadState { DPadRight = true };
        Assert.That(state.GetButton("DPadRight"), Is.True);
    }

    // ---- Each button returns false when that field is false ----

    [Test]
    public void GetButton_A_ReturnsFalse_WhenAIsFalse()
    {
        var state = AllFalse();
        Assert.That(state.GetButton("A"), Is.False);
    }

    [Test]
    public void GetButton_DPadUp_ReturnsFalse_WhenDPadUpIsFalse()
    {
        var state = AllFalse();
        Assert.That(state.GetButton("DPadUp"), Is.False);
    }

    [Test]
    public void GetButton_OnlyNamedButton_ReturnsTrue_OthersReturnFalse()
    {
        var state = new GamepadState { Y = true };
        Assert.That(state.GetButton("Y"), Is.True);
        Assert.That(state.GetButton("A"), Is.False);
        Assert.That(state.GetButton("Start"), Is.False);
    }
}
