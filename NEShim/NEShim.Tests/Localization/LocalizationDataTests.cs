using NEShim.Localization;

namespace NEShim.Tests.Localization;

[TestFixture]
internal class LocalizationDataTests
{
    [Test]
    public void DefaultInstance_FontFamily_IsSegoeUI()
    {
        var data = new LocalizationData();
        Assert.That(data.FontFamily, Is.EqualTo("Segoe UI"));
    }

    [Test]
    public void DefaultInstance_Back_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.Back, Is.EqualTo("← Back"));
    }

    [Test]
    public void DefaultInstance_SlotLabel_IsFormatString()
    {
        var data = new LocalizationData();
        Assert.That(data.SlotLabel, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_SlotNoSave_HasLeadingSpaces()
    {
        var data = new LocalizationData();
        Assert.That(data.SlotNoSave, Does.StartWith("  "));
    }

    [Test]
    public void DefaultInstance_SoundVolume_IsEnglishLabel()
    {
        var data = new LocalizationData();
        Assert.That(data.SoundVolume, Is.EqualTo("Volume"));
    }

    [Test]
    public void DefaultInstance_InGameSelectSlotTitle_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.InGameSelectSlotTitle, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_PressKeyTitle_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.PressKeyTitle, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_PressButtonTitle_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.PressButtonTitle, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_InGamePausedTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.InGamePausedTitle, Is.EqualTo("PAUSED"));
    }

    [Test]
    public void DefaultInstance_MainMenuTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.MainMenuTitle, Is.EqualTo("MAIN MENU"));
    }

    [Test]
    public void DefaultInstance_InGameRebindPressKey_ContainsNewline()
    {
        var data = new LocalizationData();
        Assert.That(data.InGameRebindPressKey, Does.Contain("\n"));
    }

    [Test]
    public void DefaultInstance_InGameRebindPressButton_ContainsNewline()
    {
        var data = new LocalizationData();
        Assert.That(data.InGameRebindPressButton, Does.Contain("\n"));
    }

    [Test]
    public void DefaultInstance_VideoFilterSmooth_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterSmooth, Is.EqualTo("Smooth"));
    }

    [Test]
    public void DefaultInstance_VideoFilterPixelPerfect_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterPixelPerfect, Is.EqualTo("Pixel Perfect"));
    }

    [Test]
    public void DefaultInstance_VideoFilterCrtScanlines_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterCrtScanlines, Is.EqualTo("CRT Scanlines"));
    }

    [Test]
    public void DefaultInstance_VideoFilterNtscComposite_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterNtscComposite, Is.EqualTo("NTSC Composite"));
    }

    [Test]
    public void DefaultInstance_OverscanOverscan_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.OverscanOverscan, Is.EqualTo("Overscan"));
    }

    [Test]
    public void DefaultInstance_OverscanNormal_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.OverscanNormal, Is.EqualTo("Normal"));
    }

    [Test]
    public void DefaultInstance_OverscanUnderscan_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.OverscanUnderscan, Is.EqualTo("Underscan"));
    }

    [Test]
    public void DefaultInstance_VideoFilterTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterTitle, Is.EqualTo("VIDEO FILTER"));
    }

    [Test]
    public void DefaultInstance_VideoOverlayLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoOverlayLabel, Is.EqualTo("Overlay"));
    }

    [Test]
    public void DefaultInstance_VideoColorPresetLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorPresetLabel, Is.EqualTo("Color"));
    }

    [Test]
    public void DefaultInstance_VideoColorFilterNone_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterNone, Is.EqualTo("None"));
    }

    [Test]
    public void DefaultInstance_VideoColorFilterWarm_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterWarm, Is.EqualTo("Warm"));
    }

    [Test]
    public void DefaultInstance_VideoColorFilterGreyscale_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterGreyscale, Is.EqualTo("Greyscale"));
    }

    [Test]
    public void DefaultInstance_VideoColorFilterNesColors_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterNesColors, Is.EqualTo("NES Colors"));
    }

    [Test]
    public void DefaultInstance_VideoColorFilterCool_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterCool, Is.EqualTo("Cool"));
    }

    [Test]
    public void DefaultInstance_VideoFilterCrtPhosphor_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterCrtPhosphor, Is.EqualTo("CRT Phosphor"));
    }

    [Test]
    public void DefaultInstance_AudioFilterTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterTitle, Is.EqualTo("AUDIO FILTER"));
    }

    [Test]
    public void DefaultInstance_AudioFilterDefault_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterDefault, Is.EqualTo("Default"));
    }

    [Test]
    public void DefaultInstance_AudioFilterWarm_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterWarm, Is.EqualTo("Warm"));
    }

    [Test]
    public void DefaultInstance_AudioFilterPseudoStereo_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterPseudoStereo, Is.EqualTo("Pseudo Stereo"));
    }

    [Test]
    public void DefaultInstance_AudioFilterWarmStereo_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterWarmStereo, Is.EqualTo("Warm Stereo"));
    }

    [Test]
    public void DefaultInstance_AudioFilterCompression_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterCompression, Is.EqualTo("Compression"));
    }

    [Test]
    public void DefaultInstance_AudioFilterBassBoost_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterBassBoost, Is.EqualTo("Bass Boost"));
    }

    [Test]
    public void DefaultInstance_AudioFilterSaturation_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterSaturation, Is.EqualTo("Saturation"));
    }

    [Test]
    public void DefaultInstance_BindNone_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.BindNone, Is.EqualTo("(none)"));
    }

    [Test]
    public void DefaultInstance_SettingsLanguage_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.SettingsLanguage, Is.EqualTo("Language"));
    }

    [Test]
    public void DefaultInstance_LanguageTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.LanguageTitle, Is.EqualTo("LANGUAGE"));
    }

    [Test]
    public void DefaultInstance_LanguageAuto_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.LanguageAuto, Is.EqualTo("Auto"));
    }

    [Test]
    public void DefaultInstance_VideoFilterXbr_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoFilterXbr, Is.EqualTo("Sharp Pixel"));
    }

    [Test]
    public void DefaultInstance_VideoPictureLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoPictureLabel, Is.EqualTo("Picture"));
    }

    [Test]
    public void DefaultInstance_VideoPictureTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoPictureTitle, Is.EqualTo("PICTURE"));
    }

    [Test]
    public void DefaultInstance_VideoBrightnessLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoBrightnessLabel, Is.EqualTo("Brightness"));
    }

    [Test]
    public void DefaultInstance_VideoContrastLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoContrastLabel, Is.EqualTo("Contrast"));
    }

    [Test]
    public void DefaultInstance_VideoSaturationLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoSaturationLabel, Is.EqualTo("Saturation"));
    }

    [Test]
    public void DefaultInstance_VideoHueLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoHueLabel, Is.EqualTo("Hue"));
    }

    [Test]
    public void DefaultInstance_VideoResetPicture_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoResetPicture, Is.EqualTo("Reset to Default"));
    }

    [Test]
    public void DefaultInstance_VideoMotionEffectMagneticDistortion_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoMotionEffectMagneticDistortion, Is.EqualTo("Magnetic Distortion"));
    }

    [Test]
    public void DefaultInstance_VideoMotionEffectPhosphorPersistence_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoMotionEffectPhosphorPersistence, Is.EqualTo("Screen Glow"));
    }

    [Test]
    public void DefaultInstance_AudioFilterDmcStabilizer_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.AudioFilterDmcStabilizer, Is.EqualTo("Pop Filter"));
    }

    // ---- Carousel (multi-game mode only) ----

    [Test]
    public void DefaultInstance_CarouselNoGamesAvailable_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.CarouselNoGamesAvailable, Is.EqualTo("No games available"));
    }

    [Test]
    public void DefaultInstance_CarouselLegendLine1_MentionsBrowseAndSelect()
    {
        var data = new LocalizationData();
        Assert.That(data.CarouselLegendLine1, Does.Contain("Browse"));
        Assert.That(data.CarouselLegendLine1, Does.Contain("Select"));
    }

    [Test]
    public void DefaultInstance_CarouselLegendLine2_MentionsFullscreen()
    {
        var data = new LocalizationData();
        Assert.That(data.CarouselLegendLine2, Does.Contain("Fullscreen"));
    }

    [Test]
    public void DefaultInstance_CarouselUnavailable_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.CarouselUnavailable, Is.EqualTo("Game Error"));
    }

    [Test]
    public void DefaultInstance_CarouselContactPublisher_MentionsPublisher()
    {
        var data = new LocalizationData();
        Assert.That(data.CarouselContactPublisher, Does.Contain("publisher"));
    }

    [Test]
    public void DefaultInstance_CarouselNoDescription_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.CarouselNoDescription, Is.EqualTo("No description available."));
    }

    // ---- Video presets (No Preset / No Filters) ----

    [Test]
    public void DefaultInstance_VideoPresetNoPreset_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoPresetNoPreset, Is.EqualTo("No Preset"));
    }

    [Test]
    public void DefaultInstance_VideoPresetNoFilters_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoPresetNoFilters, Is.EqualTo("No Filters"));
    }

    // ---- Controller-disconnected screen ----

    [Test]
    public void DefaultInstance_ControllerDisconnectedTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.ControllerDisconnectedTitle, Is.EqualTo("Controller Disconnected"));
    }

    [Test]
    public void DefaultInstance_ControllerDisconnectedHint_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.ControllerDisconnectedHint, Is.EqualTo("Press any button to continue…"));
    }

    // ---- Hotkey toast messages ----

    [Test]
    public void DefaultInstance_ToastSavedToSlot_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.ToastSavedToSlot, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_ToastLoadedSlot_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.ToastLoadedSlot, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_ToastSlotEmpty_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.ToastSlotEmpty, Does.Contain("{0}"));
    }

    [Test]
    public void DefaultInstance_ToastSlotSelected_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.ToastSlotSelected, Does.Contain("{0}"));
    }
}
