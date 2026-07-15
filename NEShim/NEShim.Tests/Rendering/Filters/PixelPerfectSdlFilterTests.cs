using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class PixelPerfectSdlFilterTests
{
    private ISdlFilter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new PixelPerfectSdlFilter();

    [Test]
    public void ImplementsISdlFilter()
        => Assert.That(_filter, Is.InstanceOf<ISdlFilter>());

    [Test]
    public void FilterMode_IsPixelPerfect()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));

    [Test]
    public void PixelAspectRatio_IsEighthSevenths()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void UseLinearSampler_IsFalse()
        => Assert.That(_filter.UseLinearSampler, Is.False);

    // The passthrough filter — null selects SDL's default no-shader path (see ISdlFilter's doc
    // comment), unlike every other structural filter which supplies a SPIR-V shader name.
    [Test]
    public void PixelShaderResourceName_IsNull()
        => Assert.That(_filter.PixelShaderResourceName, Is.Null);

    [Test]
    public void NumFragmentSamplers_IsOne()
        => Assert.That(_filter.NumFragmentSamplers, Is.EqualTo(1u));

    // The one structural filter with zero uniform buffers — it has no WriteUniformData override
    // (uses ISdlFilter's no-op default), so the factory must not bind a uniform buffer for it.
    [Test]
    public void NumFragmentUniformBuffers_IsZero()
        => Assert.That(_filter.NumFragmentUniformBuffers, Is.EqualTo(0u));

    [Test]
    public void WriteUniformData_DefaultNoOp_DoesNotThrowOrTouchBuffer()
    {
        float[] buf = [1f, 2f, 3f, 4f];
        Assert.That(() => _filter.WriteUniformData(buf, 256, 240), Throws.Nothing);
        Assert.That(buf, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
    }
}
