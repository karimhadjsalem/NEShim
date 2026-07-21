using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class PhosphorPersistenceSdlMotionEffectTests
{
    private PhosphorPersistenceSdlMotionEffect _effect = null!;

    [SetUp]
    public void SetUp() => _effect = new PhosphorPersistenceSdlMotionEffect();

    [Test]
    public void EffectMode_IsPhosphorPersistence()
        => Assert.That(_effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.PhosphorPersistence));

    [Test]
    public void Implements_ISdlMotionEffect()
        => Assert.That(_effect, Is.InstanceOf<ISdlMotionEffect>());

    [Test]
    public void SpvResourceName_IsNotNull()
        => Assert.That(_effect.SpvResourceName, Is.Not.Null.And.Not.Empty);

    [Test]
    public void SpvResourceName_EndsWithSpv()
        => Assert.That(_effect.SpvResourceName, Does.EndWith(".spv"));

    [Test]
    public void SpvResourceName_ContainsPhosphorPersistence()
        => Assert.That(_effect.SpvResourceName, Does.Contain("PhosphorPersistence"));

    [Test]
    public void UseLinearSampler_IsTrue()
        => Assert.That(_effect.UseLinearSampler, Is.True);

    [Test]
    public void NeedsTemporalBuffer_IsTrue()
        => Assert.That(_effect.NeedsTemporalBuffer, Is.True);

    // Two simultaneous sampler bindings (currentFrame + historyFrame) — the whole reason this
    // effect needs the custom SDL_GPUGraphicsPipeline instead of SDL_CreateGPURenderState.
    [Test]
    public void NumFragmentSamplers_IsTwo()
        => Assert.That(_effect.NumFragmentSamplers, Is.EqualTo(2));

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
    public void WriteShaderParams_SlotsOneAndTwo_AreZero()
    {
        float[] buf = new float[3];
        _effect.WriteShaderParams(buf, 256, 240);
        Assert.That(buf[1], Is.EqualTo(0f));
        Assert.That(buf[2], Is.EqualTo(0f));
    }

    [Test]
    public void NotifyLayout_DoesNotAffectGetFrameOffset()
    {
        var before = _effect.GetFrameOffset(7);
        ((IMotionEffect)_effect).NotifyLayout(1920, 1080, 810);
        var after = _effect.GetFrameOffset(7);
        Assert.That(before, Is.EqualTo(after));
    }
}
