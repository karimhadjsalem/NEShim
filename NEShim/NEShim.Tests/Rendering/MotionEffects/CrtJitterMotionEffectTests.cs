using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class CrtJitterMotionEffectTests
{
    private CrtJitterMotionEffect _effect = null!;

    [SetUp]
    public void SetUp()
    {
        _effect = new CrtJitterMotionEffect();
        _effect.NotifyLayout(viewportWidth: 1920, viewportHeight: 1080, letterboxHeight: 810);
    }

    [Test]
    public void EffectMode_IsCrtJitter()
        => Assert.That(_effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.CrtJitter));

    [Test]
    public void Implements_IMotionEffect()
        => Assert.That(_effect, Is.InstanceOf<IMotionEffect>());

    [Test]
    public void GetFrameOffset_OutputIsBoundedAtReferenceResolution()
    {
        for (long frame = 0; frame < 600; frame++)
        {
            var (dx, dy) = _effect.GetFrameOffset(frame);
            Assert.That(MathF.Abs(dx), Is.LessThanOrEqualTo(0.0004f), $"dx out of range at frame {frame}");
            Assert.That(MathF.Abs(dy), Is.LessThanOrEqualTo(0.00005f), $"dy out of range at frame {frame}");
        }
    }

    [Test]
    public void GetFrameOffset_VariesAcrossFrames()
    {
        var offsets = Enumerable.Range(0, 120)
            .Select(f => _effect.GetFrameOffset(f))
            .ToList();
        bool anyDxVaries = offsets.Any(o => o.Dx != offsets[0].Dx);
        bool anyDyVaries = offsets.Any(o => o.Dy != offsets[0].Dy);
        Assert.That(anyDxVaries, Is.True, "Dx did not vary across 120 frames");
        Assert.That(anyDyVaries, Is.True, "Dy did not vary across 120 frames");
    }

    [Test]
    public void GetFrameOffset_IsDeterministic()
    {
        var first  = _effect.GetFrameOffset(42);
        var second = _effect.GetFrameOffset(42);
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void NotifyLayout_At4K_AmplitudeHalves()
    {
        _effect.NotifyLayout(viewportWidth: 3840, viewportHeight: 2160, letterboxHeight: 1620);
        for (long frame = 0; frame < 600; frame++)
        {
            var (dx, dy) = _effect.GetFrameOffset(frame);
            Assert.That(MathF.Abs(dx), Is.LessThanOrEqualTo(0.0002f), $"dx not halved at frame {frame}");
            Assert.That(MathF.Abs(dy), Is.LessThanOrEqualTo(0.000025f), $"dy not halved at frame {frame}");
        }
    }

    [Test]
    public void NotifyLayout_ZeroViewportHeight_DoesNotChangeAmplitude()
    {
        var before = _effect.GetFrameOffset(0);
        _effect.NotifyLayout(viewportWidth: 1920, viewportHeight: 0, letterboxHeight: 810);
        var after = _effect.GetFrameOffset(0);
        Assert.That(after, Is.EqualTo(before));
    }
}
