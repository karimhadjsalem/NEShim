using System.Collections.Generic;
using NEShim.Config;
using NUnit.Framework;

namespace NEShim.Tests.Config;

[TestFixture]
internal class UserConfigTests
{
    // ── ApplyTo ───────────────────────────────────────────────────────────────

    [Test]
    public void ApplyTo_WhenAllFieldsNull_LeavesConfigUnchanged()
    {
        var userConfig = new UserConfig();
        var config     = new AppConfig { Volume = 77, ShowFps = true };

        userConfig.ApplyTo(config);

        Assert.That(config.Volume,  Is.EqualTo(77));
        Assert.That(config.ShowFps, Is.True);
    }

    [Test]
    public void ApplyTo_Volume_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { Volume = 42 };
        var config     = new AppConfig  { Volume = 100 };

        userConfig.ApplyTo(config);

        Assert.That(config.Volume, Is.EqualTo(42));
    }

    [Test]
    public void ApplyTo_ShowFps_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { ShowFps = true };
        var config     = new AppConfig  { ShowFps = false };

        userConfig.ApplyTo(config);

        Assert.That(config.ShowFps, Is.True);
    }

    [Test]
    public void ApplyTo_VideoFilter_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { VideoFilter = "CrtScanlines" };
        var config     = new AppConfig  { VideoFilter = "PixelPerfect" };

        userConfig.ApplyTo(config);

        Assert.That(config.VideoFilter, Is.EqualTo("CrtScanlines"));
    }

    [Test]
    public void ApplyTo_OverscanMode_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { OverscanMode = "Overscan" };
        var config     = new AppConfig  { OverscanMode = "Normal" };

        userConfig.ApplyTo(config);

        Assert.That(config.OverscanMode, Is.EqualTo("Overscan"));
    }

    [Test]
    public void ApplyTo_AudioFilter_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { AudioFilter = "Warm" };
        var config     = new AppConfig  { AudioFilter = "Default" };

        userConfig.ApplyTo(config);

        Assert.That(config.AudioFilter, Is.EqualTo("Warm"));
    }

    [Test]
    public void ApplyTo_Language_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { Language = "french" };
        var config     = new AppConfig  { Language = "Auto" };

        userConfig.ApplyTo(config);

        Assert.That(config.Language, Is.EqualTo("french"));
    }

    [Test]
    public void ApplyTo_ActiveSlot_OverridesPublisherValue()
    {
        var userConfig = new UserConfig { ActiveSlot = 5 };
        var config     = new AppConfig  { ActiveSlot = 0 };

        userConfig.ApplyTo(config);

        Assert.That(config.ActiveSlot, Is.EqualTo(5));
    }

    [Test]
    public void ApplyTo_InputMappings_OverridesPublisherValue()
    {
        var customMappings = new Dictionary<string, InputBinding>
        {
            ["P1 Up"] = new InputBinding("Up", "DPadUp"),
        };
        var userConfig = new UserConfig { InputMappings = customMappings };
        var config     = new AppConfig();

        userConfig.ApplyTo(config);

        Assert.That(config.InputMappings, Is.SameAs(customMappings));
    }

    [Test]
    public void ApplyTo_PictureAdjustments_OverridePublisherValues()
    {
        var userConfig = new UserConfig { VideoBrightness = 10, VideoContrast = -5, VideoSaturation = 20, VideoHue = 3 };
        var config     = new AppConfig();

        userConfig.ApplyTo(config);

        Assert.That(config.VideoBrightness, Is.EqualTo(10));
        Assert.That(config.VideoContrast,   Is.EqualTo(-5));
        Assert.That(config.VideoSaturation, Is.EqualTo(20));
        Assert.That(config.VideoHue,        Is.EqualTo(3));
    }

    [Test]
    public void ApplyTo_DeprecatedSoundScrubberEnabled_WhenSet_AppliesField()
    {
        var userConfig = new UserConfig { SoundScrubberEnabled = true };
        var config     = new AppConfig  { SoundScrubberEnabled = false };

        userConfig.ApplyTo(config);

        Assert.That(config.SoundScrubberEnabled, Is.True);
    }

    [Test]
    public void ApplyTo_DeprecatedGraphicsSmoothingEnabled_WhenSet_AppliesField()
    {
        var userConfig = new UserConfig { GraphicsSmoothingEnabled = true };
        var config     = new AppConfig  { GraphicsSmoothingEnabled = false };

        userConfig.ApplyTo(config);

        Assert.That(config.GraphicsSmoothingEnabled, Is.True);
    }

    // ── FromConfig ────────────────────────────────────────────────────────────

    [Test]
    public void FromConfig_CopiesVolume()
    {
        var config     = new AppConfig { Volume = 63 };
        var userConfig = UserConfig.FromConfig(config);
        Assert.That(userConfig.Volume, Is.EqualTo(63));
    }

    [Test]
    public void FromConfig_CopiesVideoFilter()
    {
        var config     = new AppConfig { VideoFilter = "CrtPhosphor" };
        var userConfig = UserConfig.FromConfig(config);
        Assert.That(userConfig.VideoFilter, Is.EqualTo("CrtPhosphor"));
    }

    [Test]
    public void FromConfig_CopiesLanguage()
    {
        var config     = new AppConfig { Language = "japanese" };
        var userConfig = UserConfig.FromConfig(config);
        Assert.That(userConfig.Language, Is.EqualTo("japanese"));
    }

    [Test]
    public void FromConfig_DoesNotSetDeprecatedFields()
    {
        var config = new AppConfig { SoundScrubberEnabled = true, GraphicsSmoothingEnabled = true };

        var userConfig = UserConfig.FromConfig(config);

        Assert.That(userConfig.SoundScrubberEnabled,     Is.Null);
        Assert.That(userConfig.GraphicsSmoothingEnabled, Is.Null);
    }

    [Test]
    public void FromConfig_DoesNotCopyPublisherOnlyFields()
    {
        // RomPath, WindowTitle, AchievementPublicKey etc. have no property on UserConfig.
        // This test verifies that FromConfig round-trips cleanly — if we apply
        // the resulting UserConfig back, publisher-only fields are untouched.
        var config = new AppConfig
        {
            RomPath              = "special.nes",
            WindowTitle          = "MyGame",
            AchievementPublicKey = "somekey",
            Volume               = 55,
        };

        var userConfig = UserConfig.FromConfig(config);
        var target     = new AppConfig
        {
            RomPath              = "other.nes",
            WindowTitle          = "OtherGame",
            AchievementPublicKey = "otherkey",
        };
        userConfig.ApplyTo(target);

        Assert.That(target.RomPath,              Is.EqualTo("other.nes"));
        Assert.That(target.WindowTitle,          Is.EqualTo("OtherGame"));
        Assert.That(target.AchievementPublicKey, Is.EqualTo("otherkey"));
        Assert.That(target.Volume,               Is.EqualTo(55));
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_FromConfig_ThenApplyTo_PreservesAllUserFields()
    {
        var original = new AppConfig
        {
            WindowMode         = "Windowed",
            MainMenuPosition   = "Center",
            ShowFps            = true,
            VideoFilter        = "CrtScanlines",
            OverscanMode       = "Overscan",
            VideoMotionEffect  = "CrtJitter",
            VideoFilterOverlay = "CrtPhosphor",
            VideoColorFilter   = "Warm",
            VideoBrightness    = 5,
            VideoContrast      = -3,
            VideoSaturation    = 10,
            VideoHue           = -1,
            VideoPreset        = "LivingRoom",
            AudioDevice        = "Speakers",
            Volume             = 80,
            AudioFilter        = "PseudoStereo",
            AudioEqBass        = 4,
            AudioEqMid         = -2,
            AudioEqTreble      = 6,
            MainMenuMusicEnabled = false,
            MainMenuMusicVolume  = 70,
            GamepadDeadzone    = 10000,
            ActiveSlot         = 3,
            AnalogStickMode    = "Diagonal",
            Language           = "spanish",
        };

        var target = new AppConfig();
        UserConfig.FromConfig(original).ApplyTo(target);

        Assert.That(target.WindowMode,           Is.EqualTo(original.WindowMode));
        Assert.That(target.MainMenuPosition,     Is.EqualTo(original.MainMenuPosition));
        Assert.That(target.ShowFps,              Is.EqualTo(original.ShowFps));
        Assert.That(target.VideoFilter,          Is.EqualTo(original.VideoFilter));
        Assert.That(target.OverscanMode,         Is.EqualTo(original.OverscanMode));
        Assert.That(target.VideoMotionEffect,    Is.EqualTo(original.VideoMotionEffect));
        Assert.That(target.VideoFilterOverlay,   Is.EqualTo(original.VideoFilterOverlay));
        Assert.That(target.VideoColorFilter,     Is.EqualTo(original.VideoColorFilter));
        Assert.That(target.VideoBrightness,      Is.EqualTo(original.VideoBrightness));
        Assert.That(target.VideoContrast,        Is.EqualTo(original.VideoContrast));
        Assert.That(target.VideoSaturation,      Is.EqualTo(original.VideoSaturation));
        Assert.That(target.VideoHue,             Is.EqualTo(original.VideoHue));
        Assert.That(target.VideoPreset,          Is.EqualTo(original.VideoPreset));
        Assert.That(target.AudioDevice,          Is.EqualTo(original.AudioDevice));
        Assert.That(target.Volume,               Is.EqualTo(original.Volume));
        Assert.That(target.AudioFilter,          Is.EqualTo(original.AudioFilter));
        Assert.That(target.AudioEqBass,          Is.EqualTo(original.AudioEqBass));
        Assert.That(target.AudioEqMid,           Is.EqualTo(original.AudioEqMid));
        Assert.That(target.AudioEqTreble,        Is.EqualTo(original.AudioEqTreble));
        Assert.That(target.MainMenuMusicEnabled, Is.EqualTo(original.MainMenuMusicEnabled));
        Assert.That(target.MainMenuMusicVolume,  Is.EqualTo(original.MainMenuMusicVolume));
        Assert.That(target.GamepadDeadzone,      Is.EqualTo(original.GamepadDeadzone));
        Assert.That(target.ActiveSlot,           Is.EqualTo(original.ActiveSlot));
        Assert.That(target.AnalogStickMode,      Is.EqualTo(original.AnalogStickMode));
        Assert.That(target.Language,             Is.EqualTo(original.Language));
    }
}
