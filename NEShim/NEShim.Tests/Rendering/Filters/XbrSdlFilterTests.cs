using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class XbrSdlFilterTests
{
    private ISdlFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new XbrSdlFilter();

    [Test]
    public void ImplementsISdlFilter()
        => Assert.That(_filter, Is.InstanceOf<ISdlFilter>());

    [Test]
    public void FilterMode_IsXbr()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.Xbr));

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void UseLinearSampler_IsFalse()
        => Assert.That(_filter.UseLinearSampler, Is.False);

    [Test]
    public void PixelShaderResourceName_IsNotNullAndEndsWithSpv()
    {
        Assert.That(_filter.PixelShaderResourceName, Is.Not.Null);
        Assert.That(_filter.PixelShaderResourceName, Does.EndWith(".spv"));
    }

    [Test]
    public void PixelShaderResourceName_ContainsXbr()
        => Assert.That(_filter.PixelShaderResourceName, Does.Contain("Xbr"));

    // ---- WriteUniformData ----

    [Test]
    public void WriteUniformData_SetsWidthAtIndex0()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[0], Is.EqualTo(256f));
    }

    [Test]
    public void WriteUniformData_SetsHeightAtIndex1()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[1], Is.EqualTo(240f));
    }

    // Unlike the CRT filters, Xbr writes only width/height — slot 2 is left untouched by design.
    [Test]
    public void WriteUniformData_DoesNotTouchSlot2()
    {
        float[] buf = [0f, 0f, 99f, 77f];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[2], Is.EqualTo(99f));
        Assert.That(buf[3], Is.EqualTo(77f));
    }

    [Test]
    public void WriteUniformData_DifferentDimensions_ReflectedInBuffer()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 512, 480);
        Assert.That(buf[0], Is.EqualTo(512f));
        Assert.That(buf[1], Is.EqualTo(480f));
    }
}
