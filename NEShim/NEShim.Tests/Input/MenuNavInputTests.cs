using NEShim.Input;

namespace NEShim.Tests.Input;

[TestFixture]
internal class MenuNavInputTests
{
    [Test]
    public void DefaultInstance_AllFieldsFalse()
    {
        var nav = new MenuNavInput();
        Assert.That(nav.Up,      Is.False);
        Assert.That(nav.Down,    Is.False);
        Assert.That(nav.Left,    Is.False);
        Assert.That(nav.Right,   Is.False);
        Assert.That(nav.Confirm, Is.False);
        Assert.That(nav.Back,    Is.False);
    }

    [Test]
    public void DefaultInstance_Any_IsFalse()
    {
        var nav = new MenuNavInput();
        Assert.That(nav.Any, Is.False);
    }

    [Test]
    public void Up_True_Any_IsTrue()
    {
        var nav = new MenuNavInput { Up = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void Down_True_Any_IsTrue()
    {
        var nav = new MenuNavInput { Down = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void Left_True_Any_IsTrue()
    {
        var nav = new MenuNavInput { Left = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void Right_True_Any_IsTrue()
    {
        var nav = new MenuNavInput { Right = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void Confirm_True_Any_IsTrue()
    {
        var nav = new MenuNavInput { Confirm = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void Back_True_Any_IsTrue()
    {
        var nav = new MenuNavInput { Back = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void MultipleFieldsTrue_Any_IsTrue()
    {
        var nav = new MenuNavInput { Up = true, Confirm = true };
        Assert.That(nav.Any, Is.True);
    }

    [Test]
    public void InitSyntax_SetsExpectedValues()
    {
        var nav = new MenuNavInput { Up = true, Down = false, Left = false, Right = true };
        Assert.That(nav.Up,    Is.True);
        Assert.That(nav.Down,  Is.False);
        Assert.That(nav.Right, Is.True);
    }

    // ── Union ────────────────────────────────────────────────────────────────────

    [Test]
    public void Union_BothDefault_ReturnsDefault()
    {
        var result = MenuNavInput.Union(default, default);
        Assert.That(result.Any, Is.False);
    }

    [Test]
    public void Union_UpFromA_ReturnsUp()
    {
        var result = MenuNavInput.Union(new MenuNavInput { Up = true }, default);
        Assert.That(result.Up, Is.True);
    }

    [Test]
    public void Union_ConfirmFromB_ReturnsConfirm()
    {
        var result = MenuNavInput.Union(default, new MenuNavInput { Confirm = true });
        Assert.That(result.Confirm, Is.True);
    }

    [Test]
    public void Union_AllFieldsFromBothSides_AllTrue()
    {
        var a = new MenuNavInput { Up = true, Left = true, Confirm = true };
        var b = new MenuNavInput { Down = true, Right = true, Back = true };
        var result = MenuNavInput.Union(a, b);

        Assert.That(result.Up,      Is.True);
        Assert.That(result.Down,    Is.True);
        Assert.That(result.Left,    Is.True);
        Assert.That(result.Right,   Is.True);
        Assert.That(result.Confirm, Is.True);
        Assert.That(result.Back,    Is.True);
    }
}
