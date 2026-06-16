using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class BilinearGdiFilterTests
{
    private BilinearGdiFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new BilinearGdiFilter();

    [Test]
    public void FilterMode_IsBilinear()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.Bilinear));

    [Test]
    public void PixelAspectRatio_IsOne()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(1f));

    [Test]
    public void CreateScaler_ReturnsBilinearScaler()
        => Assert.That(_filter.CreateScaler(), Is.InstanceOf<BilinearScaler>());

    [Test]
    public void CreateScaler_ReturnsNewInstanceEachCall()
    {
        var a = _filter.CreateScaler();
        var b = _filter.CreateScaler();
        Assert.That(a, Is.Not.SameAs(b));
    }
}
