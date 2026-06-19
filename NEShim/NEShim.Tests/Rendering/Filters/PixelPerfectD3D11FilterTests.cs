using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class PixelPerfectD3D11FilterTests
{
    private PixelPerfectD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new PixelPerfectD3D11Filter();

    [Test]
    public void FilterMode_IsPixelPerfect()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));

    [Test]
    public void PixelAspectRatio_IsNesRatio()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void Implements_ID3D11Filter()
        => Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());
}
