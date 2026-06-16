using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverscanModeParserTests
{
    [TestCase("None", OverscanMode.None)]
    [TestCase("NTSC", OverscanMode.Ntsc)]
    [TestCase("Auto", OverscanMode.Auto)]
    public void Parse_KnownValue_ReturnsCorrectMode(string input, OverscanMode expected)
    {
        Assert.That(OverscanModeParser.Parse(input), Is.EqualTo(expected));
    }

    [TestCase("none")]
    [TestCase("ntsc")]
    [TestCase("auto")]
    [TestCase("")]
    [TestCase("Unknown")]
    public void Parse_UnknownValue_ThrowsArgumentException(string input)
    {
        Assert.That(() => OverscanModeParser.Parse(input), Throws.ArgumentException);
    }
}
