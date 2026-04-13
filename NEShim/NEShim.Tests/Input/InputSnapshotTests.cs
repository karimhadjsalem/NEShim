using System.Collections.Immutable;
using NEShim.Input;

namespace NEShim.Tests.Input;

[TestFixture]
internal class InputSnapshotTests
{
    [Test]
    public void Empty_IsPressed_AlwaysReturnsFalse()
    {
        Assert.That(InputSnapshot.Empty.IsPressed("P1 Up"),    Is.False);
        Assert.That(InputSnapshot.Empty.IsPressed("P1 A"),     Is.False);
        Assert.That(InputSnapshot.Empty.IsPressed("anything"), Is.False);
    }

    [Test]
    public void IsPressed_ReturnsTrueForPressedButton()
    {
        var pressed  = ImmutableHashSet.Create("P1 A", "P1 B");
        var snapshot = new InputSnapshot(pressed);
        Assert.That(snapshot.IsPressed("P1 A"), Is.True);
        Assert.That(snapshot.IsPressed("P1 B"), Is.True);
    }

    [Test]
    public void IsPressed_ReturnsFalseForUnpressedButton()
    {
        var pressed  = ImmutableHashSet.Create("P1 A");
        var snapshot = new InputSnapshot(pressed);
        Assert.That(snapshot.IsPressed("P1 Up"), Is.False);
    }

    [Test]
    public void IsPressed_IsCaseSensitive()
    {
        var pressed  = ImmutableHashSet.Create("P1 A");
        var snapshot = new InputSnapshot(pressed);
        Assert.That(snapshot.IsPressed("p1 a"), Is.False);
        Assert.That(snapshot.IsPressed("P1 A"), Is.True);
    }

    [Test]
    public void IsPressed_EmptySet_AlwaysReturnsFalse()
    {
        var snapshot = new InputSnapshot(ImmutableHashSet<string>.Empty);
        Assert.That(snapshot.IsPressed("P1 Start"), Is.False);
    }
}
