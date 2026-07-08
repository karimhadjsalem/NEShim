using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class ControllerSpritesTests
{
    [TestCase(null,          0)]
    [TestCase("",            0)]
    [TestCase("unknown",     0)]
    [TestCase("P1 Up",       1)]
    [TestCase("P1 Down",     2)]
    [TestCase("P1 Left",     3)]
    [TestCase("P1 Right",    4)]
    [TestCase("P1 A",        5)]
    [TestCase("P1 B",        6)]
    [TestCase("P1 Start",    7)]
    [TestCase("P1 Select",   8)]
    public void GetVariantIndex_ReturnsExpectedIndex(string? button, int expected) =>
        Assert.That(ControllerSprites.GetVariantIndex(button), Is.EqualTo(expected));
}
