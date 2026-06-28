using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class ScanlineBobMotionEffectTests
{
    private ScanlineBobMotionEffect _effect = null!;

    [SetUp]
    public void SetUp()
    {
        _effect = new ScanlineBobMotionEffect();
    }

    [Test]
    public void EffectMode_IsScanlineBob()
    {
        Assert.That(_effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.ScanlineBob));
    }

    [Test]
    public void GetFrameOffset_EvenFrame_HasPositiveDy()
    {
        var (_, dy) = _effect.GetFrameOffset(0);
        Assert.That(dy, Is.GreaterThan(0f));
    }

    [Test]
    public void GetFrameOffset_OddFrame_HasNegativeDy()
    {
        var (_, dy) = _effect.GetFrameOffset(1);
        Assert.That(dy, Is.LessThan(0f));
    }

    [Test]
    public void GetFrameOffset_AlwaysZeroDx()
    {
        for (long i = 0; i < 10; i++)
        {
            var (dx, _) = _effect.GetFrameOffset(i);
            Assert.That(dx, Is.EqualTo(0f), $"dx was non-zero at frame {i}");
        }
    }

    [Test]
    public void GetFrameOffset_EvenAndOddOffsetSymmetric()
    {
        var (_, dyEven) = _effect.GetFrameOffset(0);
        var (_, dyOdd)  = _effect.GetFrameOffset(1);
        Assert.That(dyEven, Is.EqualTo(-dyOdd).Within(0.0001f));
    }

    [Test]
    public void GetFrameOffset_Deterministic()
    {
        var (dx1, dy1) = _effect.GetFrameOffset(42);
        var (dx2, dy2) = _effect.GetFrameOffset(42);
        Assert.That(dx1, Is.EqualTo(dx2));
        Assert.That(dy1, Is.EqualTo(dy2));
    }
}
