using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class CrtScreenD3D11FilterTests
{
    private CrtScreenD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _filter = new CrtScreenD3D11Filter();
    }

    [Test]
    public void ImplementsID3D11Filter()
    {
        Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());
    }

    [Test]
    public void FilterMode_IsCrtScreen()
    {
        Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScreen));
    }

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
    {
        Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));
    }

    [Test]
    public void UseLinearSampler_IsTrue()
    {
        Assert.That(((ID3D11Filter)_filter).UseLinearSampler, Is.True);
    }

    [Test]
    public void PixelShaderResourceName_IsNonNullAndEndWithCso()
    {
        Assert.That(_filter.PixelShaderResourceName, Is.Not.Null);
        Assert.That(_filter.PixelShaderResourceName, Does.EndWith(".cso"));
    }

    [Test]
    public void PixelShaderResourceName_ContainsCrtScreen()
    {
        Assert.That(_filter.PixelShaderResourceName, Does.Contain("CrtScreen"));
    }

    [Test]
    public void WriteBaseParams_WritesBarrelStrengthAtIndex0()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[0], Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
    }

    [Test]
    public void WriteBaseParams_WritesChromaStrengthAtIndex1()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(0.5f));
    }

    [Test]
    public void WriteBaseParams_WritesVignetteStrengthAtIndex2()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[2], Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
    }

    [Test]
    public void WriteBaseParams_DoesNotWriteColorModeAtIndex3()
    {
        Span<float> buffer = stackalloc float[4];
        buffer[3] = 99f;
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[3], Is.EqualTo(99f));
    }
}
