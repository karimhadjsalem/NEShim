using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class CrtScreenSdlFilterTests
{
    private CrtScreenSdlFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new CrtScreenSdlFilter();

    [Test]
    public void ImplementsISdlFilter()
        => Assert.That(_filter, Is.InstanceOf<ISdlFilter>());

    [Test]
    public void FilterMode_IsCrtScreen()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScreen));

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void UseLinearSampler_IsTrue()
        => Assert.That(_filter.UseLinearSampler, Is.True);

    [Test]
    public void PixelShaderResourceName_IsNotNullAndEndsWithSpv()
    {
        Assert.That(_filter.PixelShaderResourceName, Is.Not.Null);
        Assert.That(_filter.PixelShaderResourceName, Does.EndWith(".spv"));
    }

    [Test]
    public void PixelShaderResourceName_ContainsCrtScreen()
        => Assert.That(_filter.PixelShaderResourceName, Does.Contain("CrtScreen"));

    // ---- WriteUniformData ----
    // Unlike the other structural filters, CrtScreen's uniform slots don't carry NES
    // width/height at all — all three are fixed distortion-effect constants, independent of the
    // nesWidth/nesHeight arguments.

    [Test]
    public void WriteUniformData_SetsBarrelStrengthAtIndex0()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[0], Is.EqualTo(0.12f).Within(0.0001f));
    }

    [Test]
    public void WriteUniformData_SetsChromaStrengthAtIndex1()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[1], Is.EqualTo(0.006f).Within(0.0001f));
    }

    [Test]
    public void WriteUniformData_SetsVignetteStrengthAtIndex2()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[2], Is.EqualTo(0.35f).Within(0.0001f));
    }

    [Test]
    public void WriteUniformData_DoesNotTouchSlot3()
    {
        float[] buf = [0f, 0f, 0f, 77f];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[3], Is.EqualTo(77f));
    }

    [Test]
    public void WriteUniformData_IndependentOfNesDimensions()
    {
        float[] smallBuf = new float[4];
        float[] largeBuf = new float[4];
        _filter.WriteUniformData(smallBuf, 256, 240);
        _filter.WriteUniformData(largeBuf, 512, 480);
        Assert.That(smallBuf, Is.EqualTo(largeBuf));
    }
}
