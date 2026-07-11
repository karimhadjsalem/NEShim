using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class VideoFilterModeParserTests
{
    // ---- Parse ----

    [TestCase("NearestNeighbour", VideoFilterMode.NearestNeighbour)]
    [TestCase("Bilinear",         VideoFilterMode.Bilinear)]
    [TestCase("PixelPerfect",     VideoFilterMode.PixelPerfect)]
    [TestCase("CrtScanlines",     VideoFilterMode.CrtScanlines)]
    [TestCase("CrtPhosphor",      VideoFilterMode.CrtPhosphor)]
    [TestCase("NtscComposite",    VideoFilterMode.NtscComposite)]
    [TestCase("CrtScreen",        VideoFilterMode.CrtScreen)]
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

    // ---- DisplayName ----

    [TestCase(VideoFilterMode.Bilinear,      "Smooth")]
    [TestCase(VideoFilterMode.PixelPerfect,  "Pixel Perfect")]
    [TestCase(VideoFilterMode.CrtScanlines,  "CRT Scanlines")]
    [TestCase(VideoFilterMode.CrtPhosphor,   "CRT Phosphor")]
    [TestCase(VideoFilterMode.NtscComposite, "NTSC Composite")]
    [TestCase(VideoFilterMode.CrtScreen,     "CRT Screen")]
    public void DisplayName_KnownMode_ReturnsExpectedString(VideoFilterMode mode, string expected)
    {
        Assert.That(VideoFilterModeParser.DisplayName(mode), Is.EqualTo(expected));
    }

    // ---- D3D11Supported ----

    [Test]
    public void D3D11Supported_ContainsSevenEntries()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported.Length, Is.EqualTo(7));
    }

    [Test]
    public void D3D11Supported_ContainsPixelPerfect()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.PixelPerfect));
    }

    [Test]
    public void D3D11Supported_ContainsBilinear()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.Bilinear));
    }

    [Test]
    public void D3D11Supported_ContainsCrtScanlines()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void D3D11Supported_ContainsCrtPhosphor()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.CrtPhosphor));
    }

    [Test]
    public void D3D11Supported_ContainsNtscComposite()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.NtscComposite));
    }

    [Test]
    public void D3D11Supported_ContainsCrtScreen()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.CrtScreen));
    }

    // ---- OverlaySupported ----

    [Test]
    public void OverlaySupported_ContainsThreeEntries()
    {
        Assert.That(VideoFilterModeParser.OverlaySupported.Length, Is.EqualTo(3));
    }

    [Test]
    public void OverlaySupported_ContainsCrtScanlines()
    {
        Assert.That(VideoFilterModeParser.OverlaySupported, Contains.Item(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void OverlaySupported_ContainsCrtPhosphor()
    {
        Assert.That(VideoFilterModeParser.OverlaySupported, Contains.Item(VideoFilterMode.CrtPhosphor));
    }

    [Test]
    public void OverlaySupported_ContainsCrtScreen()
    {
        Assert.That(VideoFilterModeParser.OverlaySupported, Contains.Item(VideoFilterMode.CrtScreen));
    }

    // ---- ParseOverlay ----

    [Test]
    public void ParseOverlay_None_ReturnsNull()
    {
        Assert.That(VideoFilterModeParser.ParseOverlay("None"), Is.Null);
    }

    [Test]
    public void ParseOverlay_CrtScanlines_ReturnsCrtScanlines()
    {
        Assert.That(VideoFilterModeParser.ParseOverlay("CrtScanlines"), Is.EqualTo(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void ParseOverlay_CrtScreen_ReturnsCrtScreen()
    {
        Assert.That(VideoFilterModeParser.ParseOverlay("CrtScreen"), Is.EqualTo(VideoFilterMode.CrtScreen));
    }

    [Test]
    public void ParseOverlay_Unknown_ReturnsNull()
    {
        Assert.That(VideoFilterModeParser.ParseOverlay("Bilinear"), Is.Null);
    }

    [Test]
    public void ParseOverlay_EmptyString_ReturnsNull()
    {
        Assert.That(VideoFilterModeParser.ParseOverlay(""), Is.Null);
    }

    // ---- Xbr ----

    [Test]
    public void Parse_Xbr_ReturnsXbrMode()
    {
        Assert.That(VideoFilterModeParser.Parse("Xbr"), Is.EqualTo(VideoFilterMode.Xbr));
    }

    [Test]
    public void DisplayName_Xbr_ReturnsSharpPixel()
    {
        Assert.That(VideoFilterModeParser.DisplayName(VideoFilterMode.Xbr), Is.EqualTo("Sharp Pixel"));
    }

    [Test]
    public void D3D11Supported_ContainsXbr()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported, Contains.Item(VideoFilterMode.Xbr));
    }

}
