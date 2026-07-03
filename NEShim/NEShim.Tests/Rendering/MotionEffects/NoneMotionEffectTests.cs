using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class NoneMotionEffectTests
{
    private NoneMotionEffect _effect = null!;

    [SetUp]
    public void SetUp() => _effect = new NoneMotionEffect();

    [Test]
    public void EffectMode_IsNone()
        => Assert.That(_effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.None));

    [Test]
    public void Implements_IMotionEffect()
        => Assert.That(_effect, Is.InstanceOf<IMotionEffect>());

    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(1000L)]
    public void GetFrameOffset_AnyFrame_ReturnsZeroZero(long frame)
    {
        var (dx, dy) = _effect.GetFrameOffset(frame);
        Assert.That(dx, Is.EqualTo(0f));
        Assert.That(dy, Is.EqualTo(0f));
    }

    // ---- IMotionEffect default interface methods (via NoneMotionEffect, which overrides none of them) ----

    [Test]
    public void PixelShaderResourceName_Default_IsNull()
    {
        IMotionEffect effect = _effect;
        Assert.That(effect.PixelShaderResourceName, Is.Null);
    }

    [Test]
    public void UseLinearSampler_Default_IsFalse()
    {
        IMotionEffect effect = _effect;
        Assert.That(effect.UseLinearSampler, Is.False);
    }

    [Test]
    public void WriteShaderParams_Default_LeavesBufferUnchanged()
    {
        IMotionEffect effect = _effect;
        float[] buf = { 1f, 2f, 3f };
        effect.WriteShaderParams(buf.AsSpan(), 256, 240);
        Assert.That(buf[0], Is.EqualTo(1f));
        Assert.That(buf[1], Is.EqualTo(2f));
        Assert.That(buf[2], Is.EqualTo(3f));
    }

    [Test]
    public void NotifyLayout_Default_DoesNotThrow()
    {
        IMotionEffect effect = _effect;
        Assert.That(() => effect.NotifyLayout(1920, 1080, 810), Throws.Nothing);
    }
}
