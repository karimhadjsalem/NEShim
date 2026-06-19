using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class NtscCompositeD3D11FilterTests
{
    private NtscCompositeD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _filter = new NtscCompositeD3D11Filter();
    }

    [Test]
    public void ImplementsID3D11Filter()
    {
        Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());
    }

    [Test]
    public void FilterMode_IsNtscComposite()
    {
        Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.NtscComposite));
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
    public void PixelShaderResourceName_ContainsNtscComposite()
    {
        Assert.That(_filter.PixelShaderResourceName, Does.Contain("NtscComposite"));
    }

    [Test]
    public void WriteBaseParams_SetsInvWidthAtIndex0()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[0], Is.EqualTo(1f / 256f).Within(0.000001f));
    }

    [Test]
    public void WriteBaseParams_SetsInvHeightAtIndex1()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.EqualTo(1f / 240f).Within(0.000001f));
    }

    [Test]
    public void WriteBaseParams_SetsChromaStrengthAtIndex2()
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
    public void WriteBaseParams_ZeroWidth_DoesNotDivideByZero()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 0, 240);
        Assert.That(buffer[0], Is.EqualTo(0f));
    }

    [Test]
    public void WriteBaseParams_ZeroHeight_DoesNotDivideByZero()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 0);
        Assert.That(buffer[1], Is.EqualTo(0f));
    }

    [Test]
    public void UseLinearSampler_IsFalse()
    {
        Assert.That(((ID3D11Filter)_filter).UseLinearSampler, Is.False);
    }
}
