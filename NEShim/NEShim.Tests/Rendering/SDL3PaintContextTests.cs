using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class SDL3PaintContextTests
{
    [Test]
    public void SplitTabText_NoTab_ReturnsWholeStringAsLeft()
    {
        var (left, right) = SDL3PaintContext.SplitTabText("Hello World");
        Assert.That(left,  Is.EqualTo("Hello World"));
        Assert.That(right, Is.Null);
    }

    [Test]
    public void SplitTabText_WithTab_SplitsAtFirstTab()
    {
        var (left, right) = SDL3PaintContext.SplitTabText("P1 Up\tKeyboard: W");
        Assert.That(left,  Is.EqualTo("P1 Up"));
        Assert.That(right, Is.EqualTo("Keyboard: W"));
    }

    [Test]
    public void SplitTabText_MultipleTab_SplitsAtFirstTabOnly()
    {
        var (left, right) = SDL3PaintContext.SplitTabText("A\tB\tC");
        Assert.That(left,  Is.EqualTo("A"));
        Assert.That(right, Is.EqualTo("B\tC"));
    }

    [Test]
    public void SplitTabText_EmptyString_ReturnsEmptyLeftAndNullRight()
    {
        var (left, right) = SDL3PaintContext.SplitTabText("");
        Assert.That(left,  Is.EqualTo(""));
        Assert.That(right, Is.Null);
    }

    [Test]
    public void SplitTabText_TabAtStart_ReturnsEmptyLeftAndRemainder()
    {
        var (left, right) = SDL3PaintContext.SplitTabText("\tValue");
        Assert.That(left,  Is.EqualTo(""));
        Assert.That(right, Is.EqualTo("Value"));
    }
}
