using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class PixelPerfectGdiFilterTests
{
    private PixelPerfectGdiFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new PixelPerfectGdiFilter();

    [Test]
    public void FilterMode_IsPixelPerfect()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));

    [Test]
    public void PixelAspectRatio_IsNesRatio()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void CreateScaler_ReturnsNearestNeighborScaler()
        => Assert.That(_filter.CreateScaler(), Is.InstanceOf<NearestNeighborScaler>());

    [Test]
    public void CreateScaler_ReturnsNewInstanceEachCall()
    {
        var a = _filter.CreateScaler();
        var b = _filter.CreateScaler();
        Assert.That(a, Is.Not.SameAs(b));
    }
}
