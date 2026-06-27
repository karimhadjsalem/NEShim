using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class BilinearD3D11FilterTests
{
    private BilinearD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _filter = new BilinearD3D11Filter();
    }

    [Test]
    public void ImplementsID3D11Filter()
    {
        Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());
    }

    [Test]
    public void FilterMode_IsBilinear()
    {
        Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.Bilinear));
    }

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
    {
        Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));
    }

    [Test]
    public void PixelShaderResourceName_IsJinc2Cso()
    {
        Assert.That(((ID3D11Filter)_filter).PixelShaderResourceName,
            Is.EqualTo("NEShim.Rendering.Shaders.Jinc2.ps.cso"));
    }

    [Test]
    public void UseLinearSampler_IsTrue()
    {
        Assert.That(_filter.UseLinearSampler, Is.True);
    }

    [Test]
    public void WriteBaseParams_WritesNesWidthAndNesHeight()
    {
        ID3D11Filter filterInterface = _filter;
        Span<float> buffer = stackalloc float[4];
        filterInterface.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[0], Is.EqualTo(256f));
        Assert.That(buffer[1], Is.EqualTo(240f));
    }

}
