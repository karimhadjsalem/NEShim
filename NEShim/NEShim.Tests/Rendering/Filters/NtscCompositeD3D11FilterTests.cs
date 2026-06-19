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
    public void WriteBaseParams_Index1IsFrameParity_NotInvHeight()
    {
        // Slot [1] carries frameParity (0.0 or 1.0), not invHeight.
        // Verify it is not the invHeight value (1/240) so this is caught if someone reverts the change.
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.Not.EqualTo(1f / 240f).Within(0.000001f));
    }

    [Test]
    public void WriteBaseParams_SetsChromaStrengthAtIndex2()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[2], Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void WriteBaseParams_WritesZeroFrameParityAtIndex1_BeforeNotifyFrame()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.EqualTo(0f));
    }

    [Test]
    public void NotifyFrame_EvenCount_WritesZeroFrameParityAtIndex1()
    {
        _filter.NotifyFrame(0);
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.EqualTo(0f));
    }

    [Test]
    public void NotifyFrame_OddCount_WritesOneFrameParityAtIndex1()
    {
        _filter.NotifyFrame(1);
        Span<float> buffer = stackalloc float[4];
        _filter.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[1], Is.EqualTo(1f));
    }

    [Test]
    public void NotifyFrame_AlternatesParityEachCall()
    {
        Span<float> buffer = stackalloc float[4];
        _filter.NotifyFrame(0);
        _filter.WriteBaseParams(buffer, 256, 240);
        float first = buffer[1];
        _filter.NotifyFrame(1);
        _filter.WriteBaseParams(buffer, 256, 240);
        float second = buffer[1];
        Assert.That(first, Is.Not.EqualTo(second));
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
