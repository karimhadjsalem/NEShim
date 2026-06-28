using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverscanModeParserTests
{
    [TestCase("Overscan",  OverscanMode.Overscan)]
    [TestCase("Normal",    OverscanMode.Normal)]
    [TestCase("Underscan", OverscanMode.Underscan)]
    public void Parse_KnownValue_ReturnsCorrectMode(string input, OverscanMode expected)
    {
        Assert.That(OverscanModeParser.Parse(input), Is.EqualTo(expected));
    }

    [TestCase("None", OverscanMode.Normal)]
    [TestCase("NTSC", OverscanMode.Overscan)]
    [TestCase("Auto", OverscanMode.Overscan)]
    public void Parse_LegacyAlias_ReturnsMigratedMode(string input, OverscanMode expected)
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

    [TestCase(OverscanMode.Overscan,  "Overscan")]
    [TestCase(OverscanMode.Normal,    "Normal")]
    [TestCase(OverscanMode.Underscan, "Underscan")]
    public void DisplayName_KnownMode_ReturnsExpectedString(OverscanMode mode, string expected)
    {
        Assert.That(OverscanModeParser.DisplayName(mode), Is.EqualTo(expected));
    }
}
