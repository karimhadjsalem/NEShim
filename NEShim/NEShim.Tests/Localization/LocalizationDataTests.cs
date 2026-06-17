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
}
