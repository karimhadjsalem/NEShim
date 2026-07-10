using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class NtscCompositeSdlFilterTests
{
    private NtscCompositeSdlFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new NtscCompositeSdlFilter();

    [Test]
    public void ImplementsISdlFilter()
        => Assert.That(_filter, Is.InstanceOf<ISdlFilter>());

    [Test]
    public void FilterMode_IsNtscComposite()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.NtscComposite));

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void PixelShaderResourceName_IsNotNullAndEndsWithSpv()
    {
        Assert.That(_filter.PixelShaderResourceName, Is.Not.Null);
        Assert.That(_filter.PixelShaderResourceName, Does.EndWith(".spv"));
    }

    [Test]
    public void PixelShaderResourceName_ContainsNtscComposite()
        => Assert.That(_filter.PixelShaderResourceName, Does.Contain("NtscComposite"));

    // ---- NotifyFrame / frame parity ----

    [Test]
    public void NotifyFrame_EvenFrame_SetsParityZero()
    {
        _filter.NotifyFrame(2);
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[1], Is.EqualTo(0f));
    }

    [Test]
    public void NotifyFrame_OddFrame_SetsParityOne()
    {
        _filter.NotifyFrame(1);
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[1], Is.EqualTo(1f));
    }

    [Test]
    public void NotifyFrame_ParityAlternates_OverConsecutiveFrames()
    {
        float[] buf = new float[4];
        for (long frame = 0; frame < 10; frame++)
        {
            _filter.NotifyFrame(frame);
            _filter.WriteUniformData(buf, 256, 240);
            float expected = frame % 2 == 0 ? 0f : 1f;
            Assert.That(buf[1], Is.EqualTo(expected), $"Wrong parity at frame {frame}");
        }
    }

    // ---- WriteUniformData ----

    [Test]
    public void WriteUniformData_SetsInvWidthAtIndex0()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[0], Is.EqualTo(1f / 256f).Within(0.0001f));
    }

    [Test]
    public void WriteUniformData_WhenWidthIsZero_Index0IsZero()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 0, 240);
        Assert.That(buf[0], Is.EqualTo(0f));
    }

    [Test]
    public void WriteUniformData_SetsChromaStrengthAtIndex2()
    {
        float[] buf = new float[4];
        _filter.WriteUniformData(buf, 256, 240);
        Assert.That(buf[2], Is.GreaterThan(0f));
    }
}
