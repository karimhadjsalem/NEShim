using System.IO;
using System.Windows.Forms;
using BizHawk.Emulation.Common;
using Moq;
using NEShim.Config;
using NEShim.Saves;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class MainMenuScreenTests
{
    private string           _tempDir      = null!;
    private Mock<IStatable>  _mockStatable = null!;
    private SaveStateManager _saveStates   = null!;
    private AppConfig        _config       = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir      = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _mockStatable = new Mock<IStatable>();
        _saveStates   = new SaveStateManager(_mockStatable.Object, _tempDir);
        _config       = new AppConfig();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // No background image path — avoids any file I/O in the constructor
    private MainMenuScreen CreateScreen(
        Action<int>?  onVolumeChanged   = null,
        Action<bool>? onScrubberToggled = null,
        Action<bool>? onMenuMusicToggled = null) =>
        new(_saveStates, _config, null,
            _ => { },
            () => { },
            onVolumeChanged    ?? (_ => { }),
            onScrubberToggled  ?? (_ => { }),
            onMenuMusicToggled ?? (_ => { }));

    private void CreateSlotFile(int slot) =>
        File.WriteAllBytes(Path.Combine(_tempDir, $"slot{slot}.state"), Array.Empty<byte>());

    private void CreateAutoSaveFile() =>
        File.WriteAllBytes(Path.Combine(_tempDir, "autosave.state"), Array.Empty<byte>());

    // ---- CanResume ----

    [Test]
    public void CanResume_ReturnsFalse_WhenNoSavesExist()
    {
        using var screen = CreateScreen();
        Assert.That(screen.CanResume, Is.False);
    }

    [Test]
    public void CanResume_ReturnsTrue_WhenSlotSaveExists()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        Assert.That(screen.CanResume, Is.True);
    }

    [Test]
    public void CanResume_ReturnsTrue_WhenAutoSaveExists()
    {
        CreateAutoSaveFile();
        using var screen = CreateScreen();
        Assert.That(screen.CanResume, Is.True);
    }

    // ---- IsItemEnabled ----

    [Test]
    public void IsItemEnabled_Resume_ReturnsFalse_WhenNoSavesExist()
    {
        using var screen = CreateScreen();
        // Main menu index 1 = "Resume Game"
        Assert.That(screen.IsItemEnabled(1), Is.False);
    }

    [Test]
    public void IsItemEnabled_Resume_ReturnsTrue_WhenSaveExists()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        Assert.That(screen.IsItemEnabled(1), Is.True);
    }

    // ---- Show ----

    [Test]
    public void Show_SetsIsVisible_AndResetsToMainScreen()
    {
        using var screen = CreateScreen();
        // Navigate away from Main
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return); // enter Settings (Down skips disabled Resume → lands on Settings)
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));

        screen.Show();

        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
        Assert.That(screen.IsVisible,     Is.True);
    }

    [Test]
    public void Show_CanResumeUpdates_AfterSaveCreatedDuringSession()
    {
        using var screen = CreateScreen();
        Assert.That(screen.CanResume, Is.False);

        // Simulate save being created during play
        CreateSlotFile(2);

        screen.Show();
        Assert.That(screen.CanResume, Is.True);
    }

    // ---- Navigation ----

    [Test]
    public void HandleKey_WhenNotVisible_ReturnsFalse()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Return); // select New Game → sets IsVisible = false
        bool consumed = screen.HandleKey(Keys.Down);
        Assert.That(consumed, Is.False);
    }

    [Test]
    public void HandleKey_Down_SkipsDisabledResume_LandsOnSettings()
    {
        using var screen = CreateScreen();
        // From index 0 (New Game), Down should skip index 1 (Resume, disabled) → land on 2 (Settings)
        screen.HandleKey(Keys.Down);
        Assert.That(screen.SelectedIndex, Is.EqualTo(2));
    }

    [Test]
    public void HandleKey_Down_DoesNotSkipResume_WhenSaveExists()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        Assert.That(screen.SelectedIndex, Is.EqualTo(1)); // Resume is enabled
    }

    [Test]
    public void HandleKey_Escape_OnMainScreen_FiresExitChosenEvent()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.ExitChosen += () => fired = true;
        screen.HandleKey(Keys.Escape);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void HandleKey_Escape_OnSettingsScreen_ReturnsToMain()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);   // skip to Settings (index 2)
        screen.HandleKey(Keys.Return); // enter Settings
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));

        screen.HandleKey(Keys.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    // ---- Events ----

    [Test]
    public void HandleKey_Return_OnNewGame_FiresNewGameChosenEvent()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.NewGameChosen += () => fired = true;
        // SelectedIndex starts at 0 (New Game)
        screen.HandleKey(Keys.Return);
        Assert.That(fired,            Is.True);
        Assert.That(screen.IsVisible, Is.False);
    }

    [Test]
    public void HandleKey_Return_OnResume_NavigatesToResumeSlots_WhenSaveExists()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);   // SelectedIndex → 1 (Resume, now enabled)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.ResumeSlots));
    }

    [Test]
    public void HandleKey_Return_OnSettings_NavigatesToSettingsScreen()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);   // skip to Settings
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Settings: Window Mode single toggle ----

    [Test]
    public void Settings_GetCurrentItems_HasFourItems_WithWindowModeToggle()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down); // Settings
        screen.HandleKey(Keys.Return);
        string[] items = screen.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(4));
        Assert.That(items[1], Does.Contain("Window Mode"));
    }

    // ---- Sound screen ----

    // Helper: navigate to Settings → Sound
    private static void OpenSoundScreen(MainMenuScreen screen)
    {
        screen.HandleKey(Keys.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(Keys.Return); // enter Settings
        for (int i = 0; i < 3; i++) screen.HandleKey(Keys.Down); // to Sound (index 3)
        screen.HandleKey(Keys.Return); // enter Sound
    }

    [Test]
    public void Sound_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void Sound_GetCurrentItems_ReturnsFourItems()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        // Volume, Sound Scrubber, Menu Music, ← Back
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(4));
    }

    [Test]
    public void Sound_GetTitle_ReturnsSound()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("SOUND"));
    }

    [Test]
    public void Sound_VolumeLeft_DecreasesVolume()
    {
        var config = new AppConfig { Volume = 60 };
        int received = -1;
        using var screen = new MainMenuScreen(
            _saveStates, config, null,
            _ => { }, () => { },
            v => received = v, _ => { }, _ => { });

        OpenSoundScreen(screen);          // SelectedIndex = 0 (Volume)
        screen.HandleKey(Keys.Left);
        Assert.That(config.Volume, Is.EqualTo(55));
        Assert.That(received, Is.EqualTo(55));
    }

    [Test]
    public void Sound_VolumeRight_IncreasesVolume()
    {
        var config = new AppConfig { Volume = 60 };
        int received = -1;
        using var screen = new MainMenuScreen(
            _saveStates, config, null,
            _ => { }, () => { },
            v => received = v, _ => { }, _ => { });

        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Right);
        Assert.That(config.Volume, Is.EqualTo(65));
        Assert.That(received, Is.EqualTo(65));
    }

    [Test]
    public void Sound_ScrubberToggle_UpdatesConfigAndCallsBack()
    {
        var config = new AppConfig { SoundScrubberEnabled = false };
        bool callbackReceived = false;
        using var screen = new MainMenuScreen(
            _saveStates, config, null,
            _ => { }, () => { },
            _ => { }, on => callbackReceived = on, _ => { });

        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Down);   // select Scrubber (index 1)
        screen.HandleKey(Keys.Return);

        Assert.That(config.SoundScrubberEnabled, Is.True);
        Assert.That(callbackReceived, Is.True);
    }

    [Test]
    public void Sound_MenuMusicToggle_UpdatesConfigAndCallsBack()
    {
        var config = new AppConfig { MainMenuMusicEnabled = true };
        bool received = true;
        using var screen = new MainMenuScreen(
            _saveStates, config, null,
            _ => { }, () => { },
            _ => { }, _ => { }, on => received = on);

        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);   // select Menu Music (index 2)
        screen.HandleKey(Keys.Return);

        Assert.That(config.MainMenuMusicEnabled, Is.False);
        Assert.That(received, Is.False);
    }

    [Test]
    public void Sound_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);   // select ← Back (index 3)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    [Test]
    public void Sound_Escape_ReturnsToMain()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }
}
