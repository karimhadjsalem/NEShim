using System.Collections.Immutable;
using BizHawk.Emulation.Common;
using NEShim.Emulation;
using NEShim.Input;

namespace NEShim.Tests.Emulation;

[TestFixture]
internal class NesControllerTests
{
    private static ControllerDefinition TestDefinition()
        => new ControllerDefinition("Test NES");

    [Test]
    public void IsPressed_BeforeUpdate_ReturnsFalse()
    {
        var controller = new NesController(TestDefinition());
        Assert.That(controller.IsPressed("P1 A"), Is.False);
    }

    [Test]
    public void IsPressed_AfterUpdate_ReflectsSnapshot()
    {
        var controller = new NesController(TestDefinition());
        var snapshot   = new InputSnapshot(ImmutableHashSet.Create("P1 A", "P1 Up"));

        controller.Update(snapshot);

        Assert.That(controller.IsPressed("P1 A"),    Is.True);
        Assert.That(controller.IsPressed("P1 Up"),   Is.True);
        Assert.That(controller.IsPressed("P1 Down"), Is.False);
    }

    [Test]
    public void Update_ReplacesSnapshot_ButtonNoLongerPressed()
    {
        var controller = new NesController(TestDefinition());
        controller.Update(new InputSnapshot(ImmutableHashSet.Create("P1 A")));
        controller.Update(new InputSnapshot(ImmutableHashSet<string>.Empty));
        Assert.That(controller.IsPressed("P1 A"), Is.False);
    }

    [Test]
    public void AxisValue_AlwaysReturnsZero()
    {
        var controller = new NesController(TestDefinition());
        Assert.That(controller.AxisValue("LeftStickX"), Is.EqualTo(0));
        Assert.That(controller.AxisValue("LeftStickY"), Is.EqualTo(0));
    }

    [Test]
    public void Definition_ReturnsConstructedDefinition()
    {
        var def        = TestDefinition();
        var controller = new NesController(def);
        Assert.That(controller.Definition, Is.SameAs(def));
    }

    [Test]
    public void GetHapticsSnapshot_ReturnsEmptyCollection()
    {
        var controller = new NesController(TestDefinition());
        Assert.That(controller.GetHapticsSnapshot(), Is.Empty);
    }
}
