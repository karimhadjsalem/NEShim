using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class CrtScanlinesD3D11FilterTests
{
    private CrtScanlinesD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _filter = new CrtScanlinesD3D11Filter();
    }

    [Test]
    public void ImplementsID3D11Filter()
    {
        Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());
    }

    [Test]
    public void FilterMode_IsCrtScanlines()
    {
        Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
    {
        Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));
    }

    [Test]
    public void PixelShaderResourceName_IsNonNullAndEndWithCso()
    {
        Assert.That(_filter.PixelShaderResourceName, Is.Not.Null);
        Assert.That(_filter.PixelShaderResourceName, Does.EndWith(".cso"));
    }

    [Test]
    public void PixelShaderResourceName_ContainsCrtScanlines()
    {
        Assert.That(_filter.PixelShaderResourceName, Does.Contain("CrtScanlines"));
    }

    [Test]
    public void WriteBaseParams_SetsNesWidthAtIndex0()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[0], Is.EqualTo(256f));
    }

    [Test]
    public void WriteBaseParams_SetsNesHeightAtIndex1()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.EqualTo(240f));
    }

    [Test]
    public void WriteBaseParams_SetsScanlineIntensityAtIndex2()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[2], Is.GreaterThan(0f).And.LessThan(1f));
    }

    [Test]
    public void WriteBaseParams_DoesNotWriteColorModeAtIndex3()
    {
        Span<float> buffer = stackalloc float[4];
        buffer[3] = 99f;
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[3], Is.EqualTo(99f));
    }

    [Test]
    public void UseLinearSampler_IsFalse()
    {
        Assert.That(((ID3D11Filter)_filter).UseLinearSampler, Is.False);
    }
}
