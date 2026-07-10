using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class SdlFilterFactoryTests
{
    [Test]
    public void Create_PixelPerfect_ReturnsPixelPerfectSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.PixelPerfect);
        Assert.That(filter, Is.InstanceOf<PixelPerfectSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));
    }

    [Test]
    public void Create_Bilinear_ReturnsBilinearSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.Bilinear);
        Assert.That(filter, Is.InstanceOf<BilinearSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.Bilinear));
    }

    [Test]
    public void Create_CrtScanlines_ReturnsCrtScanlinesSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.CrtScanlines);
        Assert.That(filter, Is.InstanceOf<CrtScanlinesSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void Create_CrtPhosphor_ReturnsCrtPhosphorSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.CrtPhosphor);
        Assert.That(filter, Is.InstanceOf<CrtPhosphorSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtPhosphor));
    }

    [Test]
    public void Create_NtscComposite_ReturnsNtscCompositeSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.NtscComposite);
        Assert.That(filter, Is.InstanceOf<NtscCompositeSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.NtscComposite));
    }

    [Test]
    public void Create_CrtScreen_ReturnsCrtScreenSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.CrtScreen);
        Assert.That(filter, Is.InstanceOf<CrtScreenSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScreen));
    }

    [Test]
    public void Create_Xbr_ReturnsXbrSdlFilter()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.Xbr);
        Assert.That(filter, Is.InstanceOf<XbrSdlFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.Xbr));
    }

    [Test]
    public void Create_UnsupportedMode_FallsBackToPixelPerfect()
    {
        var filter = SdlFilterFactory.Create(VideoFilterMode.NearestNeighbour);
        Assert.That(filter, Is.InstanceOf<PixelPerfectSdlFilter>());
    }

    [Test]
    public void Create_ReturnsNewInstanceEachCall()
    {
        var a = SdlFilterFactory.Create(VideoFilterMode.PixelPerfect);
        var b = SdlFilterFactory.Create(VideoFilterMode.PixelPerfect);
        Assert.That(a, Is.Not.SameAs(b));
    }

    [Test]
    public void Create_AllD3D11SupportedModes_ReturnNonNull()
    {
        // The SDL factory supports the same 7 modes as D3D11Supported.
        foreach (var mode in VideoFilterModeParser.D3D11Supported)
            Assert.That(SdlFilterFactory.Create(mode), Is.Not.Null, $"Expected non-null for {mode}");
    }
}
