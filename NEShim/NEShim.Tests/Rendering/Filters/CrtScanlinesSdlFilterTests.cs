using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class CrtScanlinesSdlFilterTests
{
    private ISdlFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new CrtScanlinesSdlFilter();

    [Test]
    public void ImplementsISdlFilter()
        => Assert.That(_filter, Is.InstanceOf<ISdlFilter>());

    [Test]
    public void FilterMode_IsCrtScanlines()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.CrtScanlines));

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
    public void PixelShaderResourceName_ContainsCrtScanlines()
        => Assert.That(_filter.PixelShaderResourceName, Does.Contain("CrtScanlines"));

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

    [Test]
    public void WriteUniformData_SetsScanlineIntensityAtIndex2()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[2], Is.EqualTo(0.45f).Within(0.0001f));
    }

    [Test]
    public void WriteUniformData_DoesNotTouchSlot3()
    {
        float[] buf = [0f, 0f, 0f, 77f];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[3], Is.EqualTo(77f));
    }

    // ---- Cross-check vs. CrtPhosphorSdlFilter ----
    // These two filters share identical uniform data and PAR — the shader they select is the
    // only axis that distinguishes them. A future edit that accidentally diverges their shared
    // constants (or collapses their distinct shader names) should fail one of these.

    [Test]
    public void WriteUniformData_MatchesCrtPhosphor_ExceptShaderName()
    {
        var phosphor = new CrtPhosphorSdlFilter();
        float[] scanlinesBuf = new float[4];
        float[] phosphorBuf = new float[4];
        _filter.WriteUniformData(scanlinesBuf, 256, 240);
        phosphor.WriteUniformData(phosphorBuf, 256, 240);

        Assert.That(scanlinesBuf, Is.EqualTo(phosphorBuf));
        Assert.That(_filter.PixelShaderResourceName, Is.Not.EqualTo(phosphor.PixelShaderResourceName));
    }
}
