using NEShim.Rendering;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Tests.Rendering.MotionEffects;

[TestFixture]
internal class MagneticDistortionSdlMotionEffectTests
{
    private MagneticDistortionSdlMotionEffect _effect = null!;

    [SetUp]
    public void SetUp() => _effect = new MagneticDistortionSdlMotionEffect();

    // ---- Identity ----

    [Test]
    public void EffectMode_IsMagneticDistortion()
        => Assert.That(_effect.EffectMode, Is.EqualTo(VideoMotionEffectMode.MagneticDistortion));

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
    public void SpvResourceName_ContainsMagneticDistortion()
        => Assert.That(_effect.SpvResourceName, Does.Contain("MagneticDistortion"));

    [Test]
    public void UseLinearSampler_IsTrue()
        => Assert.That(_effect.UseLinearSampler, Is.True);

    // ---- GetFrameOffset ----

    [Test]
    public void GetFrameOffset_AlwaysReturnsZero()
    {
        for (long frame = 0; frame < 200; frame++)
        {
            var (dx, dy) = _effect.GetFrameOffset(frame);
            Assert.That(dx, Is.EqualTo(0f), $"Dx != 0 at frame {frame}");
            Assert.That(dy, Is.EqualTo(0f), $"Dy != 0 at frame {frame}");
        }
    }

    // ---- WriteShaderParams ----

    [Test]
    public void WriteShaderParams_AtFrameZero_PhaseIsZero()
    {
        _effect.GetFrameOffset(0);
        float[] buf = new float[3];
        _effect.WriteShaderParams(buf, 256, 240);
        Assert.That(buf[0], Is.EqualTo(0f));
    }

    [Test]
    public void WriteShaderParams_PhaseIncreasesWithFrameCount()
    {
        _effect.GetFrameOffset(0);
        float[] buf0 = new float[3];
        _effect.WriteShaderParams(buf0, 256, 240);

        _effect.GetFrameOffset(10);
        float[] buf10 = new float[3];
        _effect.WriteShaderParams(buf10, 256, 240);

        Assert.That(buf10[0], Is.GreaterThan(buf0[0]));
    }

    [Test]
    public void WriteShaderParams_AmplitudeIsPositive()
    {
        _effect.GetFrameOffset(0);
        float[] buf = new float[3];
        _effect.WriteShaderParams(buf, 256, 240);
        Assert.That(buf[1], Is.GreaterThan(0f));
    }

    [Test]
    public void WriteShaderParams_AmplitudeStaysWithinExpectedRange()
    {
        // BaseAmplitude = 0.002, AmplitudePulse = 0.0005 → range [0.0015, 0.0025]
        const float minAmp = 0.001f;
        const float maxAmp = 0.004f;
        float[] buf = new float[3];

        for (long frame = 0; frame < 1000; frame++)
        {
            _effect.GetFrameOffset(frame);
            _effect.WriteShaderParams(buf, 256, 240);
            Assert.That(buf[1], Is.InRange(minAmp, maxAmp), $"Amplitude out of range at frame {frame}");
        }
    }

    [Test]
    public void WriteShaderParams_FrequencyIsConstant()
    {
        float[] bufA = new float[3];
        float[] bufB = new float[3];

        _effect.GetFrameOffset(0);
        _effect.WriteShaderParams(bufA, 256, 240);

        _effect.GetFrameOffset(500);
        _effect.WriteShaderParams(bufB, 256, 240);

        Assert.That(bufA[2], Is.EqualTo(bufB[2]));
        Assert.That(bufA[2], Is.GreaterThan(0f));
    }

    [Test]
    public void WriteShaderParams_IsDeterministic_ForSameFrame()
    {
        float[] buf1 = new float[3];
        float[] buf2 = new float[3];

        _effect.GetFrameOffset(42);
        _effect.WriteShaderParams(buf1, 256, 240);

        _effect.GetFrameOffset(42);
        _effect.WriteShaderParams(buf2, 256, 240);

        Assert.That(buf1[0], Is.EqualTo(buf2[0]));
        Assert.That(buf1[1], Is.EqualTo(buf2[1]));
        Assert.That(buf1[2], Is.EqualTo(buf2[2]));
    }

    // ---- NotifyLayout is a no-op (inherited default interface method) ----

    [Test]
    public void NotifyLayout_DoesNotAffectGetFrameOffset()
    {
        var before = _effect.GetFrameOffset(7);
        ((IMotionEffect)_effect).NotifyLayout(1920, 1080, 810);
        var after = _effect.GetFrameOffset(7);
        Assert.That(before, Is.EqualTo(after));
    }
}
