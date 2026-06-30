using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class VideoMotionEffectModeParserTests
{
    // ---- Parse ----

    [TestCase("None",        VideoMotionEffectMode.None)]
    [TestCase("CrtJitter",   VideoMotionEffectMode.CrtJitter)]
    [TestCase("ScanlineBob", VideoMotionEffectMode.ScanlineBob)]
    public void Parse_KnownValue_ReturnsCorrectMode(string value, VideoMotionEffectMode expected)
    {
        Assert.That(VideoMotionEffectModeParser.Parse(value), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_UnknownValue_ThrowsArgumentException()
    {
        Assert.That(() => VideoMotionEffectModeParser.Parse("Unknown"),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        Assert.That(() => VideoMotionEffectModeParser.Parse(string.Empty),
            Throws.TypeOf<ArgumentException>());
    }

    // ---- DisplayName ----

    [TestCase(VideoMotionEffectMode.None,        "None")]
    [TestCase(VideoMotionEffectMode.CrtJitter,   "CRT Jitter")]
    [TestCase(VideoMotionEffectMode.ScanlineBob, "Scanline Bob")]
    public void DisplayName_KnownMode_ReturnsExpectedString(VideoMotionEffectMode mode, string expected)
    {
        Assert.That(VideoMotionEffectModeParser.DisplayName(mode), Is.EqualTo(expected));
    }

    [Test]
    public void DisplayName_AllModes_ReturnsNonEmptyString()
    {
        foreach (var mode in VideoMotionEffectModeParser.AllModes)
            Assert.That(VideoMotionEffectModeParser.DisplayName(mode), Is.Not.Empty, $"DisplayName for {mode} was empty");
    }

    // ---- AllModes ----

    [Test]
    public void AllModes_ContainsThreeEntries()
    {
        Assert.That(VideoMotionEffectModeParser.AllModes.Length, Is.EqualTo(4));
    }

    [Test]
    public void AllModes_ContainsNone()
    {
        Assert.That(VideoMotionEffectModeParser.AllModes, Contains.Item(VideoMotionEffectMode.None));
    }

    [Test]
    public void AllModes_ContainsCrtJitter()
    {
        Assert.That(VideoMotionEffectModeParser.AllModes, Contains.Item(VideoMotionEffectMode.CrtJitter));
    }

    [Test]
    public void AllModes_ContainsScanlineBob()
    {
        Assert.That(VideoMotionEffectModeParser.AllModes, Contains.Item(VideoMotionEffectMode.ScanlineBob));
    }

    [Test]
    public void AllModes_RoundTripsViaToStringAndParse()
    {
        foreach (var mode in VideoMotionEffectModeParser.AllModes)
        {
            var name = mode.ToString();
            Assert.That(VideoMotionEffectModeParser.Parse(name), Is.EqualTo(mode),
                $"Round-trip failed for {mode}");
        }
    }
}
