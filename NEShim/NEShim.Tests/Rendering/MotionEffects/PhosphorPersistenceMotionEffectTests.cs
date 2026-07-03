using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class PhosphorPersistenceMotionEffectTests
{
    private PhosphorPersistenceMotionEffect _effect = null!;

    [SetUp]
    public void SetUp() => _effect = new PhosphorPersistenceMotionEffect();

    [Test]
    public void EffectMode_IsPhosphorPersistence()
        => Assert.That(_effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.PhosphorPersistence));

    [Test]
    public void Implements_IMotionEffect()
        => Assert.That(_effect, Is.InstanceOf<IMotionEffect>());

    [Test]
    public void PixelShaderResourceName_IsNotNull()
        => Assert.That(_effect.PixelShaderResourceName, Is.Not.Null.And.Not.Empty);

    [Test]
    public void PixelShaderResourceName_EndsCso()
        => Assert.That(_effect.PixelShaderResourceName, Does.EndWith(".cso"));

    [Test]
    public void UseLinearSampler_IsTrue()
        => Assert.That(_effect.UseLinearSampler, Is.True);

    [Test]
    public void NeedsTemporalBuffer_IsTrue()
        => Assert.That(_effect.NeedsTemporalBuffer, Is.True);

    [Test]
    public void GetFrameOffset_AlwaysReturnsZero()
    {
        for (long frame = 0; frame < 100; frame++)
        {
            var (dx, dy) = _effect.GetFrameOffset(frame);
            Assert.That(dx, Is.EqualTo(0f));
            Assert.That(dy, Is.EqualTo(0f));
        }
    }

    [Test]
    public void WriteShaderParams_WritesDecayFactorAtSlot0()
    {
        float[] buf = new float[3];
        _effect.WriteShaderParams(buf, 256, 240);
        // DecayFactor = 0.65f (10-15 frame trail at 60 fps)
        Assert.That(buf[0], Is.EqualTo(0.65f));
    }

    [Test]
    public void WriteShaderParams_DecayFactor_IsInValidRange()
    {
        float[] buf = new float[3];
        _effect.WriteShaderParams(buf, 256, 240);
        Assert.That(buf[0], Is.InRange(0f, 1f));
    }

    [Test]
    public void WriteShaderParams_SlotsTwoAndThree_AreZero()
    {
        float[] buf = new float[3];
        _effect.WriteShaderParams(buf, 256, 240);
        Assert.That(buf[1], Is.EqualTo(0f));
        Assert.That(buf[2], Is.EqualTo(0f));
    }
}
