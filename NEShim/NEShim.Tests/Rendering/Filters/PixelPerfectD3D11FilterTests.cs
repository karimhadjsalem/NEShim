using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class PixelPerfectD3D11FilterTests
{
    private PixelPerfectD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new PixelPerfectD3D11Filter();

    [Test]
    public void FilterMode_IsPixelPerfect()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.PixelPerfect));

    [Test]
    public void PixelAspectRatio_IsNesRatio()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void Implements_ID3D11Filter()
        => Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());

    // ---- ID3D11Filter default interface methods (via PixelPerfectD3D11Filter which overrides nothing) ----

    [Test]
    public void PixelShaderResourceName_Default_IsNull()
    {
        ID3D11Filter f = _filter;
        Assert.That(f.PixelShaderResourceName, Is.Null);
    }

    [Test]
    public void UseLinearSampler_Default_IsFalse()
    {
        ID3D11Filter f = _filter;
        Assert.That(f.UseLinearSampler, Is.False);
    }

    [Test]
    public void WriteBaseParams_Default_LeavesBufferUnchanged()
    {
        ID3D11Filter f      = _filter;
        Span<float>  buffer = stackalloc float[4];
        buffer[0] = 1f; buffer[1] = 2f; buffer[2] = 3f;
        f.WriteBaseParams(buffer, 256, 240);
        Assert.That(buffer[0], Is.EqualTo(1f));
        Assert.That(buffer[1], Is.EqualTo(2f));
        Assert.That(buffer[2], Is.EqualTo(3f));
    }

    [Test]
    public void NotifyFrame_Default_DoesNotThrow()
    {
        ID3D11Filter f = _filter;
        Assert.That(() => f.NotifyFrame(999), Throws.Nothing);
    }
}
