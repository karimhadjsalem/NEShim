using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class GdiFilterFactoryTests
{
    [Test]
    public void Create_Bilinear_ReturnsBilinearFilter()
    {
        var filter = GdiFilterFactory.Create(VideoFilterMode.Bilinear);
        Assert.That(filter, Is.InstanceOf<BilinearGdiFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.Bilinear));
    }

    [Test]
    public void Create_PixelPerfect_ReturnsPixelPerfectFilter()
    {
        var filter = GdiFilterFactory.Create(VideoFilterMode.PixelPerfect);
        Assert.That(filter, Is.InstanceOf<PixelPerfectGdiFilter>());
        Assert.That(filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));
    }

    [Test]
    public void Create_UnsupportedMode_FallsBackToPixelPerfect()
    {
        var filter = GdiFilterFactory.Create(VideoFilterMode.CrtScanlines);
        Assert.That(filter, Is.InstanceOf<PixelPerfectGdiFilter>());
    }

    [Test]
    public void Create_ReturnsNewInstanceEachCall()
    {
        var a = GdiFilterFactory.Create(VideoFilterMode.PixelPerfect);
        var b = GdiFilterFactory.Create(VideoFilterMode.PixelPerfect);
        Assert.That(a, Is.Not.SameAs(b));
    }

    [Test]
    public void Create_AllGdiSupportedModes_ReturnNonNull()
    {
        foreach (var mode in VideoFilterModeParser.GdiSupported)
            Assert.That(GdiFilterFactory.Create(mode), Is.Not.Null, $"Expected non-null for {mode}");
    }
}
