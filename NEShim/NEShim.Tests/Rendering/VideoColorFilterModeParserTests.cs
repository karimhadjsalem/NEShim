using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class VideoColorFilterModeParserTests
{
    // ---- Parse ----

    [TestCase("None",               VideoColorFilterMode.None)]
    [TestCase("Warm",               VideoColorFilterMode.Warm)]
    [TestCase("Greyscale",          VideoColorFilterMode.Greyscale)]
    [TestCase("NesColorCorrection", VideoColorFilterMode.NesColorCorrection)]
    [TestCase("Cool",               VideoColorFilterMode.Cool)]
    public void Parse_KnownValue_ReturnsCorrectMode(string value, VideoColorFilterMode expected)
    {
        Assert.That(VideoColorFilterModeParser.Parse(value), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_UnknownValue_ThrowsArgumentException()
    {
        Assert.That(() => VideoColorFilterModeParser.Parse("Unknown"),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        Assert.That(() => VideoColorFilterModeParser.Parse(string.Empty),
            Throws.TypeOf<ArgumentException>());
    }

    // ---- DisplayName ----

    [TestCase(VideoColorFilterMode.None,               "None")]
    [TestCase(VideoColorFilterMode.Warm,               "Warm")]
    [TestCase(VideoColorFilterMode.Greyscale,          "Greyscale")]
    [TestCase(VideoColorFilterMode.NesColorCorrection, "NES Colors")]
    [TestCase(VideoColorFilterMode.Cool,               "Cool")]
    public void DisplayName_KnownMode_ReturnsExpectedString(VideoColorFilterMode mode, string expected)
    {
        Assert.That(VideoColorFilterModeParser.DisplayName(mode), Is.EqualTo(expected));
    }

    [Test]
    public void DisplayName_AllModes_ReturnsNonEmptyString()
    {
        foreach (var mode in VideoColorFilterModeParser.AllModes)
            Assert.That(VideoColorFilterModeParser.DisplayName(mode), Is.Not.Empty, $"DisplayName for {mode} was empty");
    }

    // ---- AllModes ----

    [Test]
    public void AllModes_ContainsFiveEntries()
    {
        Assert.That(VideoColorFilterModeParser.AllModes.Length, Is.EqualTo(5));
    }

    [Test]
    public void AllModes_ContainsCool()
    {
        Assert.That(VideoColorFilterModeParser.AllModes, Contains.Item(VideoColorFilterMode.Cool));
    }

    [Test]
    public void AllModes_ContainsNone()
    {
        Assert.That(VideoColorFilterModeParser.AllModes, Contains.Item(VideoColorFilterMode.None));
    }

    [Test]
    public void AllModes_RoundTripsViaDisplayNameAndParse()
    {
        foreach (var mode in VideoColorFilterModeParser.AllModes)
        {
            var name = mode.ToString();
            Assert.That(VideoColorFilterModeParser.Parse(name), Is.EqualTo(mode),
                $"Round-trip failed for {mode}");
        }
    }
}
