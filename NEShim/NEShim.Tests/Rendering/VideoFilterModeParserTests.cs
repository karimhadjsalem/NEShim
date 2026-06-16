using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class VideoFilterModeParserTests
{
    [TestCase("NearestNeighbour", VideoFilterMode.NearestNeighbour)]
    [TestCase("Bilinear",         VideoFilterMode.Bilinear)]
    [TestCase("PixelPerfect",     VideoFilterMode.PixelPerfect)]
    [TestCase("CrtScanlines",     VideoFilterMode.CrtScanlines)]
    [TestCase("NtscComposite",    VideoFilterMode.NtscComposite)]
    public void Parse_KnownValue_ReturnsCorrectMode(string input, VideoFilterMode expected)
    {
        Assert.That(VideoFilterModeParser.Parse(input), Is.EqualTo(expected));
    }

    [TestCase("bilinear")]
    [TestCase("nearestneighbour")]
    [TestCase("")]
    [TestCase("Unknown")]
    public void Parse_UnknownValue_ThrowsArgumentException(string input)
    {
        Assert.That(() => VideoFilterModeParser.Parse(input), Throws.ArgumentException);
    }
}
