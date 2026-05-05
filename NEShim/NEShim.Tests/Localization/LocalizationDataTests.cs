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
}
