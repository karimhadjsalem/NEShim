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
}
