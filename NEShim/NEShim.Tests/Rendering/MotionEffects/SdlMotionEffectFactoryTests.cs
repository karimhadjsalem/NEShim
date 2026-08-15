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
    public void Create_PhosphorPersistence_ReturnsPhosphorPersistenceSdlMotionEffect()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.PhosphorPersistence);
        Assert.That(effect, Is.InstanceOf<PhosphorPersistenceSdlMotionEffect>());
    }

    [Test]
    public void Create_PhosphorPersistence_ReturnsPhosphorPersistenceEffectMode()
    {
        var effect = SdlMotionEffectFactory.Create(VideoMotionEffectMode.PhosphorPersistence);
        Assert.That(effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.PhosphorPersistence));
    }

    [Test]
    public void Create_UnmappedMode_Throws()
    {
        Assert.That(() => SdlMotionEffectFactory.Create((VideoMotionEffectMode)999),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Create_ReturnsNewInstanceEachCall()
    {
        var a = SdlMotionEffectFactory.Create(VideoMotionEffectMode.CrtJitter);
        var b = SdlMotionEffectFactory.Create(VideoMotionEffectMode.CrtJitter);
        Assert.That(a, Is.Not.SameAs(b));
    }
}
