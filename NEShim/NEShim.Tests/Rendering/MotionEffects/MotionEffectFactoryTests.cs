using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class MotionEffectFactoryTests
{
    [Test]
    public void Create_None_ReturnsNoneMotionEffect()
    {
        var effect = MotionEffectFactory.Create(VideoMotionEffectMode.None);
        Assert.That(effect, Is.InstanceOf<NoneMotionEffect>());
    }

    [Test]
    public void Create_CrtJitter_ReturnsCrtJitterMotionEffect()
    {
        var effect = MotionEffectFactory.Create(VideoMotionEffectMode.CrtJitter);
        Assert.That(effect, Is.InstanceOf<CrtJitterMotionEffect>());
    }

    [Test]
    public void Create_ReturnsEffectWithMatchingMode()
    {
        foreach (var mode in VideoMotionEffectModeParser.AllModes)
        {
            var effect = MotionEffectFactory.Create(mode);
            Assert.That(effect.EffectMode, Is.EqualTo(mode));
        }
    }
}
