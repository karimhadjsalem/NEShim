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
    public void PixelShaderResourceName_IsNull()
    {
        Assert.That(((ID3D11Filter)_filter).PixelShaderResourceName, Is.Null);
    }

    [Test]
    public void UseLinearSampler_IsTrue()
    {
        Assert.That(_filter.UseLinearSampler, Is.True);
    }

    [Test]
    public void WriteBaseParams_DoesNotModifyBuffer()
    {
        ID3D11Filter filterInterface = _filter;
        Span<float> buffer = stackalloc float[4];
        buffer[0] = 1f;
        buffer[1] = 2f;
        buffer[2] = 3f;
        buffer[3] = 4f;
        filterInterface.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[0], Is.EqualTo(1f));
        Assert.That(buffer[1], Is.EqualTo(2f));
        Assert.That(buffer[2], Is.EqualTo(3f));
        Assert.That(buffer[3], Is.EqualTo(4f));
    }
}
