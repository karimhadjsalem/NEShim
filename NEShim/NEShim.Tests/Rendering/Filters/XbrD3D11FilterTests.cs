using NEShim.Rendering;
using NEShim.Rendering.Filters;

namespace NEShim.Tests.Rendering.Filters;

[TestFixture]
internal class XbrD3D11FilterTests
{
    private XbrD3D11Filter _filter = null!;

    [SetUp]
    public void SetUp() => _filter = new XbrD3D11Filter();

    [Test]
    public void FilterMode_IsXbr()
        => Assert.That(_filter.FilterMode, Is.EqualTo(VideoFilterMode.Xbr));

    [Test]
    public void Implements_ID3D11Filter()
        => Assert.That(_filter, Is.InstanceOf<ID3D11Filter>());

    [Test]
    public void PixelAspectRatio_Is8Over7()
        => Assert.That(_filter.PixelAspectRatio, Is.EqualTo(8f / 7f).Within(0.0001f));

    [Test]
    public void UseLinearSampler_IsFalse()
        => Assert.That(_filter.UseLinearSampler, Is.False);

    [Test]
    public void PixelShaderResourceName_IsNotNull()
        => Assert.That(_filter.PixelShaderResourceName, Is.Not.Null.And.Not.Empty);

    [Test]
    public void PixelShaderResourceName_EndsCso()
        => Assert.That(_filter.PixelShaderResourceName, Does.EndWith(".cso"));

    [Test]
    public void WriteBaseParams_SetsWidthAtSlot0()
    {
        float[] buf = new float[4];
        _filter.WriteBaseParams(buf, nesWidth: 256, nesHeight: 240);
        Assert.That(buf[0], Is.EqualTo(256f));
    }

    [Test]
    public void WriteBaseParams_SetsHeightAtSlot1()
    {
        float[] buf = new float[4];
        _filter.WriteBaseParams(buf, nesWidth: 256, nesHeight: 240);
        Assert.That(buf[1], Is.EqualTo(240f));
    }

    [Test]
    public void WriteBaseParams_DoesNotTouchSlots2And3()
    {
        float[] buf = [0f, 0f, 99f, 77f];
        _filter.WriteBaseParams(buf, 256, 240);
        Assert.That(buf[2], Is.EqualTo(99f));
        Assert.That(buf[3], Is.EqualTo(77f));
    }

    [Test]
    public void WriteBaseParams_DifferentDimensions_ReflectedInBuffer()
    {
        float[] buf = new float[4];
        _filter.WriteBaseParams(buf, nesWidth: 512, nesHeight: 480);
        Assert.That(buf[0], Is.EqualTo(512f));
        Assert.That(buf[1], Is.EqualTo(480f));
    }
}
