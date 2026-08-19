using NEShim.Input;

namespace NEShim.Tests.Input;

[TestFixture]
internal class AnalogStickHelperTests
{
    private const int Dz = 8000;

    // ── StickUp ─────────────────────────────────────────────────────────────────

    [Test]
    public void StickUp_AboveDeadzone_ReturnsTrue()
        => Assert.That(AnalogStickHelper.StickUp(0, Dz + 1, Dz), Is.True);

    [Test]
    public void StickUp_AtDeadzone_ReturnsFalse()
        => Assert.That(AnalogStickHelper.StickUp(0, Dz, Dz), Is.False);

    [Test]
    public void StickUp_BelowDeadzone_ReturnsFalse()
        => Assert.That(AnalogStickHelper.StickUp(0, Dz - 1, Dz), Is.False);

    [Test]
    public void StickUp_FullDeflection_ReturnsTrue()
        => Assert.That(AnalogStickHelper.StickUp(0, short.MaxValue, Dz), Is.True);

    [Test]
    public void StickUp_NegativeFullDeflection_ReturnsFalse()
        // Verifies the clamped ThumbLY value (short.MinValue = -32768) never triggers StickUp.
        // SDL3GamepadDevice clamps negated LeftY to [short.MinValue+1, short.MaxValue] so the
        // pre-fix overflow value of -32768 can no longer appear; this pins the expected behaviour.
        => Assert.That(AnalogStickHelper.StickUp(0, short.MinValue, Dz), Is.False);

    // ── StickDown ───────────────────────────────────────────────────────────────

    [Test]
    public void StickDown_BelowNegativeDeadzone_ReturnsTrue()
        => Assert.That(AnalogStickHelper.StickDown(0, -(Dz + 1), Dz), Is.True);

    [Test]
    public void StickDown_AtNegativeDeadzone_ReturnsFalse()
        => Assert.That(AnalogStickHelper.StickDown(0, -Dz, Dz), Is.False);

    [Test]
    public void StickDown_AboveNegativeDeadzone_ReturnsFalse()
        => Assert.That(AnalogStickHelper.StickDown(0, -(Dz - 1), Dz), Is.False);

    // ── StickLeft / StickRight ───────────────────────────────────────────────────

    [Test]
    public void StickLeft_BelowNegativeDeadzone_ReturnsTrue()
        => Assert.That(AnalogStickHelper.StickLeft(-(Dz + 1), 0, Dz), Is.True);

    [Test]
    public void StickLeft_AtNegativeDeadzone_ReturnsFalse()
        => Assert.That(AnalogStickHelper.StickLeft(-Dz, 0, Dz), Is.False);

    [Test]
    public void StickRight_AboveDeadzone_ReturnsTrue()
        => Assert.That(AnalogStickHelper.StickRight(Dz + 1, 0, Dz), Is.True);

    [Test]
    public void StickRight_AtDeadzone_ReturnsFalse()
        => Assert.That(AnalogStickHelper.StickRight(Dz, 0, Dz), Is.False);

    // ── Cardinal dominant-axis logic ─────────────────────────────────────────────

    [Test]
    public void StickUp_DiagonalWithYDominant_ReturnsTrue()
    {
        // Y is dominant (|ly| >= |lx|), both above deadzone — only Up fires.
        int lx = Dz + 1000;
        int ly = Dz + 2000;
        Assert.That(AnalogStickHelper.StickUp(lx, ly, Dz),    Is.True);
        Assert.That(AnalogStickHelper.StickRight(lx, ly, Dz), Is.False);
    }

    [Test]
    public void StickRight_DiagonalWithXDominant_ReturnsTrue()
    {
        // X is dominant (|lx| > |ly|), both above deadzone — only Right fires.
        int lx = Dz + 2000;
        int ly = Dz + 1000;
        Assert.That(AnalogStickHelper.StickRight(lx, ly, Dz), Is.True);
        Assert.That(AnalogStickHelper.StickUp(lx, ly, Dz),    Is.False);
    }

    [Test]
    public void StickUp_EqualAxes_ReturnsTrueForUpNotRight()
    {
        // |ly| == |lx|: StickUp uses >=, StickRight uses >, so Up wins.
        int v = Dz + 1000;
        Assert.That(AnalogStickHelper.StickUp(v, v, Dz),    Is.True);
        Assert.That(AnalogStickHelper.StickRight(v, v, Dz), Is.False);
    }

    [Test]
    public void StickDown_DiagonalWithYDominant_NoOtherDirectionFires()
    {
        int lx = Dz + 500;
        int ly = -(Dz + 2000);
        Assert.That(AnalogStickHelper.StickDown(lx, ly, Dz),  Is.True);
        Assert.That(AnalogStickHelper.StickRight(lx, ly, Dz), Is.False);
        Assert.That(AnalogStickHelper.StickLeft(lx, ly, Dz),  Is.False);
        Assert.That(AnalogStickHelper.StickUp(lx, ly, Dz),    Is.False);
    }

    [Test]
    public void AllDirections_AtRest_ReturnFalse()
    {
        Assert.That(AnalogStickHelper.StickUp(0, 0, Dz),    Is.False);
        Assert.That(AnalogStickHelper.StickDown(0, 0, Dz),  Is.False);
        Assert.That(AnalogStickHelper.StickLeft(0, 0, Dz),  Is.False);
        Assert.That(AnalogStickHelper.StickRight(0, 0, Dz), Is.False);
    }
}
