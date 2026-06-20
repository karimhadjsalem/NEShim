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
    public void DefaultInstance_SoundVolume_ContainsFormatPlaceholder()
    {
        var data = new LocalizationData();
        Assert.That(data.SoundVolume, Does.Contain("{0}"));
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
    public void DefaultInstance_VideoColorFilterLabel_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterLabel, Is.EqualTo("Color Effect"));
    }

    [Test]
    public void DefaultInstance_VideoColorFilterTitle_IsEnglish()
    {
        var data = new LocalizationData();
        Assert.That(data.VideoColorFilterTitle, Is.EqualTo("COLOR EFFECT"));
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
}
