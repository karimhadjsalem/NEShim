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
        _effect.NotifyLayout(viewportHeight: 1080, letterboxHeight: 810);
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

    [Test]
    public void NotifyLayout_AmplitudeEqualsHalfNESScanlineInClipSpace()
    {
        // Expected: letterboxH / (240 * viewportH) = 810 / (240 * 1080) ≈ 0.003125
        float expected = 810f / (240f * 1080f);
        var (_, dy) = _effect.GetFrameOffset(0);
        Assert.That(dy, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void NotifyLayout_AmplitudeScalesWithViewportAndLetterbox()
    {
        _effect.NotifyLayout(viewportHeight: 2160, letterboxHeight: 1620);
        float expected = 1620f / (240f * 2160f);
        var (_, dy) = _effect.GetFrameOffset(0);
        Assert.That(dy, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void NotifyLayout_ZeroViewportHeight_DoesNotThrowOrChangeAmplitude()
    {
        float amplitudeBefore = _effect.GetFrameOffset(0).Dy;
        _effect.NotifyLayout(viewportHeight: 0, letterboxHeight: 810);
        Assert.That(_effect.GetFrameOffset(0).Dy, Is.EqualTo(amplitudeBefore));
    }

    [Test]
    public void GetFrameOffset_BeforeNotifyLayout_ReturnsZero()
    {
        var uninitialised = new ScanlineBobMotionEffect();
        var (dx, dy) = uninitialised.GetFrameOffset(0);
        Assert.That(dx, Is.EqualTo(0f));
        Assert.That(dy, Is.EqualTo(0f));
    }
}
