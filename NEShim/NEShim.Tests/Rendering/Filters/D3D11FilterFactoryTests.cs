using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class D3D11FilterFactoryTests
{
    [Test]
    public void Create_PixelPerfect_ReturnsPixelPerfectD3D11Filter()
    {
        var filter = D3D11FilterFactory.Create(VideoFilterMode.PixelPerfect);
        Assert.That(filter, Is.InstanceOf<PixelPerfectD3D11Filter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));
    }

    [Test]
    public void Create_UnsupportedMode_FallsBackToPixelPerfect()
    {
        var filter = D3D11FilterFactory.Create(VideoFilterMode.NearestNeighbour);
        Assert.That(filter, Is.InstanceOf<PixelPerfectD3D11Filter>());
    }

    [Test]
    public void Create_ReturnsNewInstanceEachCall()
    {
        var a = D3D11FilterFactory.Create(VideoFilterMode.PixelPerfect);
        var b = D3D11FilterFactory.Create(VideoFilterMode.PixelPerfect);
        Assert.That(a, Is.Not.SameAs(b));
    }

    [Test]
    public void Create_CrtScanlines_ReturnsCrtScanlinesD3D11Filter()
    {
        var filter = D3D11FilterFactory.Create(VideoFilterMode.CrtScanlines);
        Assert.That(filter, Is.InstanceOf<CrtScanlinesD3D11Filter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void Create_NtscComposite_ReturnsNtscCompositeD3D11Filter()
    {
        var filter = D3D11FilterFactory.Create(VideoFilterMode.NtscComposite);
        Assert.That(filter, Is.InstanceOf<NtscCompositeD3D11Filter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.NtscComposite));
    }

    [Test]
    public void Create_CrtPhosphor_ReturnsCrtPhosphorD3D11Filter()
    {
        var filter = D3D11FilterFactory.Create(VideoFilterMode.CrtPhosphor);
        Assert.That(filter, Is.InstanceOf<CrtPhosphorD3D11Filter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtPhosphor));
    }

    [Test]
    public void Create_Bilinear_ReturnsBilinearD3D11Filter()
    {
        var filter = D3D11FilterFactory.Create(VideoFilterMode.Bilinear);
        Assert.That(filter, Is.InstanceOf<BilinearD3D11Filter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.Bilinear));
    }

    [Test]
    public void Create_AllD3D11SupportedModes_ReturnNonNull()
    {
        foreach (var mode in VideoFilterModeParser.D3D11Supported)
            Assert.That(D3D11FilterFactory.Create(mode), Is.Not.Null, $"Expected non-null for {mode}");
    }
}
