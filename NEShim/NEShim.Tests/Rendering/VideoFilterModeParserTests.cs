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
    public void D3D11Supported_ContainsSixEntries()
    {
        Assert.That(VideoFilterModeParser.D3D11Supported.Length, Is.EqualTo(6));
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

    // ---- GdiSupported ----

    [Test]
    public void GdiSupported_ContainsTwoEntries()
    {
        Assert.That(VideoFilterModeParser.GdiSupported.Length, Is.EqualTo(2));
    }

    [Test]
    public void GdiSupported_ContainsBilinear()
    {
        Assert.That(VideoFilterModeParser.GdiSupported, Contains.Item(VideoFilterMode.Bilinear));
    }

    [Test]
    public void GdiSupported_ContainsPixelPerfect()
    {
        Assert.That(VideoFilterModeParser.GdiSupported, Contains.Item(VideoFilterMode.PixelPerfect));
    }

    [Test]
    public void GdiSupported_DoesNotContainCrtScanlines()
    {
        Assert.That(VideoFilterModeParser.GdiSupported, Does.Not.Contain(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void GdiSupported_DoesNotContainCrtPhosphor()
    {
        Assert.That(VideoFilterModeParser.GdiSupported, Does.Not.Contain(VideoFilterMode.CrtPhosphor));
    }

    [Test]
    public void GdiSupported_DoesNotContainNtscComposite()
    {
        Assert.That(VideoFilterModeParser.GdiSupported, Does.Not.Contain(VideoFilterMode.NtscComposite));
    }

}
