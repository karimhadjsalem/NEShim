using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class SdlMotionEffectFactoryTests
{
    [Test]
    public void Create_None_ReturnsNoneMotionEffect()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.None);
        Assert.That(effect, Is.InstanceOf<NoneMotionEffect>());
    }

    [Test]
    public void Create_CrtJitter_ReturnsCrtJitterMotionEffect()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.CrtJitter);
        Assert.That(effect, Is.InstanceOf<CrtJitterMotionEffect>());
    }

    [Test]
    public void Create_ScanlineBob_ReturnsScanlineBobMotionEffect()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.ScanlineBob);
        Assert.That(effect, Is.InstanceOf<ScanlineBobMotionEffect>());
    }

    [Test]
    public void Create_MagneticDistortion_ReturnsMagneticDistortionSdlMotionEffect()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.MagneticDistortion);
        Assert.That(effect, Is.InstanceOf<MagneticDistortionSdlMotionEffect>());
    }

    [Test]
    public void Create_PhosphorPersistence_ReturnsNoneMotionEffect()
    {
        // PhosphorPersistence requires two texture samplers — incompatible with
        // SDL_GPURenderState. Factory must demote to None rather than throw.
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.PhosphorPersistence);
        Assert.That(effect, Is.InstanceOf<NoneMotionEffect>());
    }

    [Test]
    public void Create_PhosphorPersistence_ReturnsNoneEffectMode()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.PhosphorPersistence);
        Assert.That(effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.None));
    }

    [Test]
    public void Create_Unknown_FallsBackToNoneMotionEffect()
    {
        var effect = SdlMotionEffectFactory.Create((VideoMotionEffectMode)999);
        Assert.That(effect, Is.InstanceOf<NoneMotionEffect>());
    }

    [Test]
    public void Create_ReturnsNewInstanceEachCall()
    {
        var a = SdlMotionEffectFactory.Create(VideoMotionEffectMode.CrtJitter);
        var b = SdlMotionEffectFactory.Create(VideoMotionEffectMode.CrtJitter);
        Assert.That(a, Is.Not.SameAs(b));
    }
}
