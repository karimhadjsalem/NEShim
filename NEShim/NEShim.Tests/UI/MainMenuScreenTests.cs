using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BizHawk.Emulation.Common;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Input;
using NSubstitute;
using NEShim.Saves;
using NEShim.Localization;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class MainMenuScreenTests
{
    private string           _tempDir      = null!;
    private IStatable        _mockStatable = null!;
    private SaveStateManager _saveStates   = null!;
    private AppConfig        _config       = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir      = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _mockStatable = Substitute.For<IStatable>();
        _saveStates   = new SaveStateManager(_mockStatable, _tempDir);
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
        Action<int>?                                       onVolumeChanged            = null,
        Action<AudioFilterMode>?                           onFilterChanged            = null,
        Action<bool>?                                      onMenuMusicToggled         = null,
        Action<NEShim.Rendering.VideoFilterMode>?          onVideoFilterChanged       = null,
        Action<NEShim.Rendering.VideoColorFilterMode>?     onVideoColorFilterChanged  = null,
        Action<NEShim.Rendering.OverscanMode>?             onOverscanModeChanged      = null) =>
        new(_saveStates, _config, new LocalizationData(), null,
            _ => { },
            () => { },
            onVolumeChanged            ?? (_ => { }),
            onFilterChanged            ?? (_ => { }),
            onMenuMusicToggled         ?? (_ => { }),
            onVideoFilterChanged       ?? (_ => { }),
            onVideoColorFilterChanged  ?? (_ => { }),
            onOverscanModeChanged      ?? (_ => { }),
            _ => { });

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
    public void HandleKey_Escape_OnMainScreen_DoesNothing()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.ExitChosen += () => fired = true;
        screen.HandleKey(Keys.Escape);
        Assert.That(fired, Is.False);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
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
    public void Settings_GetCurrentItems_HasFiveItems_WithVideoSubmenuAndBack()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down); // Settings
        screen.HandleKey(Keys.Return);
        string[] items = screen.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(6)); // Video, Sound, Keyboard Controls, Gamepad Controls, Language, ← Back
        Assert.That(items[0], Is.EqualTo("Video"));
        Assert.That(items[5], Does.StartWith("←"));
    }

    // ---- Sound screen ----

    // Helper: navigate to Settings → Sound
    private static void OpenSoundScreen(MainMenuScreen screen)
    {
        screen.HandleKey(Keys.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(Keys.Return); // enter Settings
        screen.HandleKey(Keys.Down);   // to Sound (index 1)
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
        // Volume + Audio Filter + Menu Music + Back
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
            _saveStates, config, new LocalizationData(), null,
            _ => { }, () => { },
            v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });

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
            _saveStates, config, new LocalizationData(), null,
            _ => { }, () => { },
            v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });

        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Right);
        Assert.That(config.Volume, Is.EqualTo(65));
        Assert.That(received, Is.EqualTo(65));
    }

    [Test]
    public void Sound_FilterSelect_UpdatesConfigAndCallsBack()
    {
        var config = new AppConfig { AudioFilter = "Default" };
        AudioFilterMode? received = null;
        using var screen = new MainMenuScreen(
            _saveStates, config, new LocalizationData(), null,
            _ => { }, () => { },
            _ => { }, mode => received = mode, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });

        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Down);   // index 1 = Audio Filter item
        screen.HandleKey(Keys.Return); // enter AudioFilter sub-screen
        screen.HandleKey(Keys.Down);   // index 1 = Warm
        screen.HandleKey(Keys.Return); // select Warm → returns to Sound

        Assert.That(config.AudioFilter, Is.EqualTo("Warm"));
        Assert.That(received, Is.EqualTo(AudioFilterMode.Warm));
    }

    [Test]
    public void Sound_MenuMusicToggle_UpdatesConfigAndCallsBack()
    {
        var config = new AppConfig { MainMenuMusicEnabled = true };
        bool received = true;
        using var screen = new MainMenuScreen(
            _saveStates, config, new LocalizationData(), null,
            _ => { }, () => { },
            _ => { }, _ => { }, on => received = on, _ => { }, _ => { }, _ => { }, _ => { });

        OpenSoundScreen(screen);
        for (int i = 0; i < 2; i++) screen.HandleKey(Keys.Down); // Music is at index 2
        screen.HandleKey(Keys.Return);

        Assert.That(config.MainMenuMusicEnabled, Is.False);
        Assert.That(received, Is.False);
    }

    [Test]
    public void Sound_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        for (int i = 0; i < 3; i++) screen.HandleKey(Keys.Down); // Back is at index 3
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Audio Filter sub-screen ----

    private static void OpenAudioFilterScreen(MainMenuScreen screen)
    {
        screen.HandleKey(Keys.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(Keys.Return); // enter Settings
        screen.HandleKey(Keys.Down);   // Sound (index 1)
        screen.HandleKey(Keys.Return);                             // enter Sound
        screen.HandleKey(Keys.Down);                              // Audio Filter item (index 1)
        screen.HandleKey(Keys.Return);                            // enter AudioFilter screen
    }

    [Test]
    public void AudioFilter_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.AudioFilter));
    }

    [Test]
    public void AudioFilter_GetTitle_ReturnsAudioFilter()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("AUDIO FILTER"));
    }

    [Test]
    public void AudioFilter_GetCurrentItems_ReturnsEightItems()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        // 7 filter modes + Back
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(8));
    }

    [Test]
    public void AudioFilter_ActiveFilter_ShowsCheckmark()
    {
        _config.AudioFilter = "Warm";
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        string[] items = screen.GetCurrentItems();
        Assert.That(items[0], Does.StartWith("  ")); // Default — not active
        Assert.That(items[1], Does.StartWith("✓"));  // Warm — active
    }

    [Test]
    public void AudioFilter_SelectMode_UpdatesConfigAndReturnsToSound()
    {
        _config.AudioFilter = "Default";
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        screen.HandleKey(Keys.Down);   // index 1 = Warm
        screen.HandleKey(Keys.Return); // select Warm
        Assert.That(_config.AudioFilter, Is.EqualTo("Warm"));
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void AudioFilter_SelectMode_InvokesCallback()
    {
        AudioFilterMode? received = null;
        using var screen = new MainMenuScreen(
            _saveStates, _config, new LocalizationData(), null,
            _ => { }, () => { },
            _ => { }, mode => received = mode, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenAudioFilterScreen(screen);
        screen.HandleKey(Keys.Down);   // Warm
        screen.HandleKey(Keys.Return);
        Assert.That(received, Is.EqualTo(AudioFilterMode.Warm));
    }

    [Test]
    public void AudioFilter_Back_ReturnsToSound()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        for (int i = 0; i < 7; i++) screen.HandleKey(Keys.Down); // Back is at index 7
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void AudioFilter_Escape_ReturnsToSound()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        screen.HandleKey(Keys.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void Sound_Escape_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Localization: audio filter display names and BindNone ----

    [Test]
    public void AudioFilter_GetTitle_UsesLocalizedTitle()
    {
        var loc = new LocalizationData { AudioFilterTitle = "FILT CUSTOM" };
        using var screen = new MainMenuScreen(_saveStates, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenAudioFilterScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("FILT CUSTOM"));
    }

    [Test]
    public void AudioFilter_GetCurrentItems_UsesLocalizedDefaultName()
    {
        var loc = new LocalizationData { AudioFilterDefault = "TestDefault" };
        using var screen = new MainMenuScreen(_saveStates, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenAudioFilterScreen(screen);
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("TestDefault"));
    }

    [Test]
    public void Sound_AudioFilterItem_UsesLocalizedLabel()
    {
        var loc = new LocalizationData { AudioFilterLabel = "TestLabel" };
        using var screen = new MainMenuScreen(_saveStates, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenSoundScreen(screen);
        Assert.That(screen.GetCurrentItems()[1], Does.Contain("TestLabel"));
    }

    [Test]
    public void Video_FilterItem_ContainsCrtPhosphor_WhenActive()
    {
        _config.VideoFilter = "CrtPhosphor";
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        Assert.That(screen.GetCurrentItems()[1], Does.Contain("CRT Phosphor"));
    }

    [Test]
    public void KeyboardBindings_UnboundKey_ShowsBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, "DPadUp");
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);   // Settings
        screen.HandleKey(Keys.Return); // enter Settings
        screen.HandleKey(Keys.Down);   // skip Video (index 0)
        screen.HandleKey(Keys.Down);   // skip Sound (index 1)
        screen.HandleKey(Keys.Return); // Keyboard Controls (index 2)
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("(none)"));
    }

    [Test]
    public void KeyboardBindings_UnboundKey_UsesLocalizedBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, "DPadUp");
        var loc = new LocalizationData { BindNone = "(unset)" };
        using var screen = new MainMenuScreen(_saveStates, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        screen.HandleKey(Keys.Down);   // Settings
        screen.HandleKey(Keys.Return); // enter Settings
        screen.HandleKey(Keys.Down);   // skip Video (index 0)
        screen.HandleKey(Keys.Down);   // skip Sound (index 1)
        screen.HandleKey(Keys.Return); // Keyboard Controls (index 2)
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("(unset)"));
    }

    [Test]
    public void GamepadBindings_UnboundButton_ShowsBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("W", null);
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);   // Settings
        screen.HandleKey(Keys.Return); // enter Settings
        screen.HandleKey(Keys.Down);   // skip Sound (index 1)
        screen.HandleKey(Keys.Down);   // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);   // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return); // GamepadBindings
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("(none)"));
    }

    // ---- Video screen ----

    private static void OpenVideoScreen(MainMenuScreen screen)
    {
        screen.HandleKey(Keys.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(Keys.Return); // enter Settings
        screen.HandleKey(Keys.Return); // enter Video (index 0)
    }

    [Test]
    public void Video_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    [Test]
    public void Video_GetCurrentItems_ReturnsFiveItemsInGdiMode()
    {
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        // GDI mode: Window Mode, Video Filter, Overscan, FPS Overlay, ← Back
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(5));
    }

    [Test]
    public void Video_GetCurrentItems_DoesNotContainColorEffect_InGdiMode()
    {
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        Assert.That(screen.GetCurrentItems().Any(i => i.Contains("Color Effect")), Is.False);
    }

    [Test]
    public void Video_GetTitle_ReturnsVideo()
    {
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("VIDEO"));
    }

    [Test]
    public void Video_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);   // ← Back (index 4 in GDI mode)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    [Test]
    public void Video_FilterSubMenu_SelectsFilterAndCallsBack()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        using var screen = CreateScreen(onVideoFilterChanged: mode => received = mode);
        OpenVideoScreen(screen);
        screen.HandleKey(Keys.Down);   // Video Filter (index 1)
        screen.HandleKey(Keys.Return); // → VideoFilter sub-menu
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.VideoFilter));
        screen.HandleKey(Keys.Return); // select first filter (index 0)
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
        Assert.That(received, Is.Not.Null);
    }

    // ---- VideoFilter sub-menu ----

    private static void OpenVideoFilterSubMenu(MainMenuScreen screen)
    {
        OpenVideoScreen(screen);
        screen.HandleKey(Keys.Down);   // Video Filter (index 1)
        screen.HandleKey(Keys.Return); // → VideoFilter sub-menu
    }

    [Test]
    public void VideoFilter_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.VideoFilter));
    }

    [Test]
    public void VideoFilter_GetTitle_ReturnsVideoFilterTitle()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("VIDEO FILTER"));
    }

    [Test]
    public void VideoFilter_GetCurrentItems_ReturnsThreeItemsInGdiMode()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        // GDI mode: [Bilinear, PixelPerfect, Back]
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(3));
    }

    [Test]
    public void VideoFilter_CurrentFilter_HasCheckmark()
    {
        using var screen = CreateScreen();
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(screen);
        var items = screen.GetCurrentItems();
        Assert.That(items[1], Does.StartWith("✓")); // PixelPerfect is at index 1 in GdiSupported
    }

    [Test]
    public void VideoFilter_SelectFilter_UpdatesConfig()
    {
        using var screen = CreateScreen();
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(Keys.Return); // select Bilinear (index 0)
        Assert.That(_config.VideoFilter, Is.EqualTo("Bilinear"));
    }

    [Test]
    public void VideoFilter_SelectFilter_FiresCallback()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        using var screen = CreateScreen(onVideoFilterChanged: m => received = m);
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(Keys.Return); // select Bilinear (index 0)
        Assert.That(received, Is.EqualTo(NEShim.Rendering.VideoFilterMode.Bilinear));
    }

    [Test]
    public void VideoFilter_SelectFilter_NavigatesBackToVideo()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(Keys.Return); // select any filter
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    [Test]
    public void VideoFilter_Back_NavigatesBackToVideo()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        var itemCount = screen.GetCurrentItems().Length;
        for (int i = 0; i < itemCount - 1; i++) screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return); // Back
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    [Test]
    public void Video_OverscanCycle_UpdatesConfigAndCallsBack()
    {
        NEShim.Rendering.OverscanMode? received = null;
        _config.OverscanMode = "Overscan";
        using var screen = CreateScreen(onOverscanModeChanged: mode => received = mode);
        OpenVideoScreen(screen);
        screen.HandleKey(Keys.Down);   // Video Filter (index 1)
        screen.HandleKey(Keys.Down);   // Overscan (index 2 in GDI mode)
        screen.HandleKey(Keys.Return);
        // Overscan cycle: Overscan → Normal → Underscan
        Assert.That(_config.OverscanMode, Is.EqualTo("Normal"));
        Assert.That(received, Is.EqualTo(NEShim.Rendering.OverscanMode.Normal));
    }

    // ---- Rollover ----

    [Test]
    public void HandleKey_Down_AtLast_WrapsToFirst_OnMainScreen()
    {
        using var screen = CreateScreen();
        // Main: 4 items but Resume (index 1) disabled. Enabled: New Game(0), Settings(2), Exit(3).
        screen.HandleKey(Keys.Down); // 0 → 2 (skips disabled Resume)
        screen.HandleKey(Keys.Down); // 2 → 3
        Assert.That(screen.SelectedIndex, Is.EqualTo(3));

        screen.HandleKey(Keys.Down); // 3 → wraps to 0
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    // ---- Key binding uniqueness ----

    [Test]
    public void KeyBindings_AssignDuplicateKey_ClearsOldAction()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);   // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);   // skip Video (index 0)
        screen.HandleKey(Keys.Down);   // skip Sound (index 1)
        screen.HandleKey(Keys.Return); // Keyboard Controls (index 2)
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.KeyboardBindings));

        screen.HandleKey(Keys.Down);   // P1 Down (index 1)
        screen.HandleKey(Keys.Return); // start rebind
        screen.HandleKey(Keys.W);      // bind "W" — already used by P1 Up

        Assert.That(_config.InputMappings["P1 Down"].Key, Is.EqualTo("W"));
        Assert.That(_config.InputMappings["P1 Up"].Key,   Is.Null); // cleared
    }

    // ---- HandleGamepadNav ----
    // Main screen: New Game(0), Resume(1 disabled), Settings(2), Exit(3)

    [Test]
    public void HandleGamepadNav_WhenNotVisible_DoesNothing()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Return); // New Game → IsVisible = false
        screen.HandleGamepadNav(new MenuNavInput { Down = true });
        // Should do nothing (IsVisible = false)
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void HandleGamepadNav_NoInputs_DoesNothing()
    {
        using var screen = CreateScreen();
        screen.HandleGamepadNav(new MenuNavInput());
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    [Test]
    public void HandleGamepadNav_Down_MovesSelection()
    {
        using var screen = CreateScreen();
        screen.HandleGamepadNav(new MenuNavInput { Down = true });
        // Skips disabled Resume (index 1) → lands on Settings (index 2)
        Assert.That(screen.SelectedIndex, Is.EqualTo(2));
    }

    [Test]
    public void HandleGamepadNav_Up_AtFirst_WrapsToLast()
    {
        using var screen = CreateScreen();
        screen.HandleGamepadNav(new MenuNavInput { Up = true });
        Assert.That(screen.SelectedIndex, Is.EqualTo(3)); // Exit (last enabled)
    }

    [Test]
    public void HandleGamepadNav_Confirm_OnNewGame_FiresEvent()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.NewGameChosen += () => fired = true;
        screen.HandleGamepadNav(new MenuNavInput { Confirm = true });
        Assert.That(fired, Is.True);
    }

    [Test]
    public void HandleGamepadNav_Back_OnSubScreen_NavigatesUp()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));

        screen.HandleGamepadNav(new MenuNavInput { Back = true });
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    [Test]
    public void HandleGamepadNav_DuringRebinding_Ignores()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Video (index 0)
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Return);  // KeyboardBindings (index 2)
        screen.HandleKey(Keys.Return);  // start rebinding "P1 Up"
        Assert.That(screen.RebindingAction, Is.Not.Null);

        int indexBefore = screen.SelectedIndex;
        screen.HandleGamepadNav(new MenuNavInput { Down = true });
        Assert.That(screen.SelectedIndex, Is.EqualTo(indexBefore));
    }

    [Test]
    public void HandleGamepadNav_Left_OnSoundVolume_DecreasesVolume()
    {
        _config.Volume = 50;
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        Assert.That(screen.SelectedIndex, Is.EqualTo(0)); // Volume selected

        screen.HandleGamepadNav(new MenuNavInput { Left = true });
        Assert.That(_config.Volume, Is.EqualTo(45));
    }

    [Test]
    public void HandleGamepadNav_Right_OnSoundVolume_IncreasesVolume()
    {
        _config.Volume = 50;
        using var screen = CreateScreen();
        OpenSoundScreen(screen);

        screen.HandleGamepadNav(new MenuNavInput { Right = true });
        Assert.That(_config.Volume, Is.EqualTo(55));
    }

    // ---- HandleKey Z / Space (alternate confirm keys) ----

    [Test]
    public void HandleKey_Z_ActsAsConfirm()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.NewGameChosen += () => fired = true;
        screen.HandleKey(Keys.Z);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void HandleKey_Space_ActsAsConfirm()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.NewGameChosen += () => fired = true;
        screen.HandleKey(Keys.Space);
        Assert.That(fired, Is.True);
    }

    // ---- Exit item ----

    [Test]
    public void HandleKey_Return_OnExit_FiresExitChosenEvent()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.ExitChosen += () => fired = true;
        // Main: New Game(0), Resume(1 disabled), Settings(2), Exit(3)
        screen.HandleKey(Keys.Down); // 0 → 2 (skips disabled)
        screen.HandleKey(Keys.Down); // 2 → 3
        screen.HandleKey(Keys.Return);
        Assert.That(fired,            Is.True);
        Assert.That(screen.IsVisible, Is.False);
    }

    // ---- Escape during rebinding ----

    [Test]
    public void HandleKey_Escape_DuringKeyRebinding_CancelsRebind()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Video (index 0)
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Return);  // KeyboardBindings (index 2)
        screen.HandleKey(Keys.Return);  // start rebinding P1 Up
        Assert.That(screen.RebindingAction, Is.Not.Null);

        screen.HandleKey(Keys.Escape);
        Assert.That(screen.RebindingAction, Is.Null);
    }

    [Test]
    public void HandleKey_Escape_DuringGamepadRebinding_CancelsRebind()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Return);  // start rebinding P1 Up
        Assert.That(screen.GamepadRebindingAction, Is.Not.Null);

        screen.HandleKey(Keys.Escape);
        Assert.That(screen.GamepadRebindingAction, Is.Null);
    }

    // ---- HandleGamepadButtonPress ----

    [Test]
    public void HandleGamepadButtonPress_WhenNotRebinding_ReturnsNull()
    {
        using var screen = CreateScreen();
        Assert.That(screen.HandleGamepadButtonPress("A"), Is.Null);
    }

    [Test]
    public void HandleGamepadButtonPress_StartButton_ReturnsReservedMessage()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Return);  // start rebinding P1 Up
        Assert.That(screen.GamepadRebindingAction, Is.Not.Null);

        string? msg = screen.HandleGamepadButtonPress("Start");
        Assert.That(msg, Is.EqualTo("Start is reserved for the menu"));
        Assert.That(screen.GamepadRebindingAction, Is.Null);
    }

    [Test]
    public void HandleGamepadButtonPress_NormalButton_SetsBindingAndReturnsNull()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Return);  // start rebinding P1 Up

        string? msg = screen.HandleGamepadButtonPress("X");
        Assert.That(msg, Is.Null);
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.EqualTo("X"));
        Assert.That(screen.GamepadRebindingAction, Is.Null);
    }

    // ---- GetTitle ----

    [Test]
    public void GetTitle_MainScreen_ReturnsMainMenu()
    {
        using var screen = CreateScreen();
        Assert.That(screen.GetTitle(), Is.EqualTo("MAIN MENU"));
    }

    [Test]
    public void GetTitle_SettingsScreen_ReturnsSettings()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        Assert.That(screen.GetTitle(), Is.EqualTo("SETTINGS"));
    }

    [Test]
    public void GetTitle_KeyboardBindings_ReturnsKeyboardControls()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);   // skip Video (index 0)
        screen.HandleKey(Keys.Down);   // skip Sound (index 1)
        screen.HandleKey(Keys.Return); // KeyboardBindings (index 2)
        Assert.That(screen.GetTitle(), Is.EqualTo("KEYBOARD CONTROLS"));
    }

    [Test]
    public void GetTitle_KeyboardBindings_DuringRebind_ContainsActionLabel()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);   // skip Video (index 0)
        screen.HandleKey(Keys.Down);   // skip Sound (index 1)
        screen.HandleKey(Keys.Return); // KeyboardBindings (index 2)
        screen.HandleKey(Keys.Return); // start rebinding P1 Up
        Assert.That(screen.GetTitle(), Does.Contain("UP"));
    }

    [Test]
    public void GetTitle_GamepadBindings_ReturnsGamepadControls()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.GetTitle(), Is.EqualTo("GAMEPAD CONTROLS"));
    }

    [Test]
    public void GetTitle_GamepadBindings_DuringRebind_ContainsActionLabel()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Return);  // start rebinding P1 Up
        Assert.That(screen.GetTitle(), Does.Contain("UP"));
    }

    [Test]
    public void GetTitle_ResumeSlots_ReturnsLoadGame()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Resume (enabled)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.GetTitle(), Is.EqualTo("LOAD GAME"));
    }

    // ---- ResumeSlots ----

    [Test]
    public void ResumeSlots_Back_ReturnsToMain()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Resume
        screen.HandleKey(Keys.Return);  // → ResumeSlots
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.ResumeSlots));

        // Last item in the list is "← Back"
        string[] items = screen.GetCurrentItems();
        for (int i = 0; i < items.Length - 1; i++) screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    [Test]
    public void ResumeSlots_LoadSlot_FiresResumeChosenAndHidesScreen()
    {
        CreateSlotFile(0);
        using var screen = CreateScreen();
        bool fired = false;
        screen.ResumeChosen += () => fired = true;

        screen.HandleKey(Keys.Down);    // Resume (enabled)
        screen.HandleKey(Keys.Return);  // → ResumeSlots, first item = slot 0
        screen.HandleKey(Keys.Return);  // activate slot 0 → load → ResumeChosen

        Assert.That(fired,            Is.True);
        Assert.That(screen.IsVisible, Is.False);
    }

    // ---- GamepadBindings ----

    [Test]
    public void GamepadBindings_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.GamepadBindings));
    }

    [Test]
    public void GamepadBindings_GetCurrentItems_HasNineItems()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(9)); // 8 actions + Back
    }

    [Test]
    public void GamepadBindings_SelectAction_SetsGamepadRebindingAction()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Return);  // index 0 → P1 Up
        Assert.That(screen.GamepadRebindingAction, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void GamepadBindings_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        for (int i = 0; i < 8; i++) screen.HandleKey(Keys.Down); // navigate to Back (index 8)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    [Test]
    public void GamepadBindings_AssignDuplicateButton_ClearsOldAction()
    {
        _config.InputMappings["P1 Up"].GamepadButton   = "A";
        _config.InputMappings["P1 Down"].GamepadButton = "B";

        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // P1 Down (index 1)
        screen.HandleKey(Keys.Return);  // start rebinding P1 Down

        screen.HandleGamepadButtonPress("A"); // "A" was P1 Up → clear P1 Up, assign to P1 Down
        Assert.That(_config.InputMappings["P1 Down"].GamepadButton, Is.EqualTo("A"));
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton,   Is.Null);
    }

    // ---- Video: WindowMode and FPS ----

    [Test]
    public void Video_WindowMode_CallsWindowModeCallback()
    {
        bool received = false;
        _config.WindowMode = "Windowed";
        using var screen = new MainMenuScreen(
            _saveStates, _config, new LocalizationData(), null,
            fs => received = fs, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenVideoScreen(screen);
        screen.HandleKey(Keys.Return); // Window Mode (index 0, already selected)
        Assert.That(received, Is.True); // Windowed → Fullscreen (toggled to true)
    }

    [Test]
    public void Video_FpsToggle_UpdatesConfig()
    {
        bool initial = _config.ShowFps;
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Down);   // FPS Overlay (index 3 in GDI mode)
        screen.HandleKey(Keys.Return);
        Assert.That(_config.ShowFps, Is.EqualTo(!initial));
    }

    // ---- Settings Back ----

    [Test]
    public void Settings_Back_ReturnsToMain()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        for (int i = 0; i < 5; i++) screen.HandleKey(Keys.Down); // to Back (index 5)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    // ---- KeyboardBindings Back ----

    [Test]
    public void KeyboardBindings_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Video (index 0)
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Return);  // KeyboardBindings (index 2)
        for (int i = 0; i < 8; i++) screen.HandleKey(Keys.Down); // to Back (index 8)
        screen.HandleKey(Keys.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Volume at boundaries ----

    [Test]
    public void Sound_VolumeLeft_AtZero_DoesNotGoNegative()
    {
        _config.Volume = 0;
        int received = 999;
        using var screen = new MainMenuScreen(
            _saveStates, _config, new LocalizationData(), null,
            _ => { }, () => { }, v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Left); // already at 0 — no change
        Assert.That(_config.Volume, Is.EqualTo(0));
        Assert.That(received,       Is.EqualTo(999)); // callback not invoked
    }

    [Test]
    public void Sound_VolumeRight_AtMax_DoesNotExceed100()
    {
        _config.Volume = 100;
        int received = 999;
        using var screen = new MainMenuScreen(
            _saveStates, _config, new LocalizationData(), null,
            _ => { }, () => { }, v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenSoundScreen(screen);
        screen.HandleKey(Keys.Right); // already at 100 — no change
        Assert.That(_config.Volume, Is.EqualTo(100));
        Assert.That(received,       Is.EqualTo(999));
    }

    // ---- ResolveAssetPath ----

    [Test]
    public void ResolveAssetPath_RootedPath_Exists_ReturnsPath()
    {
        string f = Path.GetTempFileName();
        try
        {
            string? result = MainMenuScreen.ResolveAssetPath(f);
            Assert.That(result, Is.EqualTo(f));
        }
        finally { File.Delete(f); }
    }

    [Test]
    public void ResolveAssetPath_RootedPath_NotExist_ReturnsNull()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        Assert.That(MainMenuScreen.ResolveAssetPath(nonExistent), Is.Null);
    }

    [Test]
    public void ResolveAssetPath_RelativePath_NotFound_ReturnsNull()
    {
        Assert.That(MainMenuScreen.ResolveAssetPath("this_does_not_exist_xyz.png"), Is.Null);
    }

    // ---- OverrideStartBindingProtection ----

    [Test]
    public void HandleGamepadButtonPress_StartPressed_OverrideEnabled_BindsStart()
    {
        _config.OverrideStartBindingProtection = true;
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Return);  // start rebinding P1 Up (index 0)
        Assert.That(screen.GamepadRebindingAction, Is.Not.Null);

        string? msg = screen.HandleGamepadButtonPress("Start");
        Assert.That(msg, Is.Null);
        Assert.That(screen.GamepadRebindingAction, Is.Null);
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.EqualTo("Start"));
    }

    [Test]
    public void HandleGamepadButtonPress_OpenMenuAction_OverrideEnabled_UpdatesHotkeyMappings()
    {
        _config.OverrideStartBindingProtection = true;
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);  // GamepadBindings

        // OpenMenu entry is at index 8 (after the 8 NES button entries)
        for (int i = 0; i < 8; i++) screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);  // start rebinding OpenMenu
        Assert.That(screen.GamepadRebindingAction, Is.EqualTo("OpenMenu"));

        string? msg = screen.HandleGamepadButtonPress("Y");
        Assert.That(msg, Is.Null);
        Assert.That(screen.GamepadRebindingAction, Is.Null);
        Assert.That(_config.GamepadHotkeyMappings["OpenMenu"], Is.EqualTo("Y"));
    }

    [Test]
    public void GetCurrentItems_GamepadBindings_OverrideEnabled_ReturnsTenItems()
    {
        _config.OverrideStartBindingProtection = true;
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(Keys.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(Keys.Return);  // GamepadBindings
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(10)); // 8 NES + OpenMenu + Back
    }

    // ---- ActiveNesButton ----

    [Test]
    public void ActiveNesButton_KeyboardBindings_FirstItem_ReturnsP1Up()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);    // Settings
        screen.HandleKey(Keys.Return);
        screen.HandleKey(Keys.Down);    // skip Video (index 0)
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Return);  // Keyboard Controls (index 2)
        // SelectedIndex is 0 = P1 Up
        Assert.That(screen.ActiveNesButton, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void ActiveNesButton_KeyboardBindings_BackEntry_ReturnsNull()
    {
        using var screen = CreateScreen();
        screen.HandleKey(Keys.Down);
        screen.HandleKey(Keys.Return);  // Settings
        screen.HandleKey(Keys.Down);    // skip Video (index 0)
        screen.HandleKey(Keys.Down);    // skip Sound (index 1)
        screen.HandleKey(Keys.Return);  // Keyboard Controls (index 2)
        // Navigate to the last item (Back, configKey = "")
        for (int i = 0; i < 8; i++) screen.HandleKey(Keys.Down);
        Assert.That(screen.ActiveNesButton, Is.Null);
    }

    [Test]
    public void ActiveNesButton_NonBindingScreen_ReturnsNull()
    {
        using var screen = CreateScreen();
        Assert.That(screen.ActiveNesButton, Is.Null); // Main screen
    }
}
