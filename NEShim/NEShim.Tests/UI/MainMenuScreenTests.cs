using System.Drawing;
using System.IO;
using SDL3;
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
    private ISaveManager _saves  = null!;
    private AppConfig    _config = null!;

    [SetUp]
    public void SetUp()
    {
        _saves  = Substitute.For<ISaveManager>();
        _saves.SlotCount.Returns(8);
        _config = new AppConfig();
    }

    [TearDown]
    public void TearDown()
    {
        NEShim.Platform.PlatformDetector.SetD3D11Active(false);
    }

    // No background image path — avoids any file I/O in the constructor
    private MainMenuScreen CreateScreen(
        Action<int>?                                       onVolumeChanged                  = null,
        Action<AudioFilterMode>?                           onFilterChanged                  = null,
        Action<bool>?                                      onMenuMusicToggled               = null,
        Action<NEShim.Rendering.VideoFilterMode>?          onVideoFilterChanged             = null,
        Action<NEShim.Rendering.VideoFilterMode?>?         onVideoFilterOverlayChanged      = null,
        Action<NEShim.Rendering.VideoColorFilterMode>?     onVideoColorFilterChanged        = null,
        Action<NEShim.Rendering.OverscanMode>?             onOverscanModeChanged            = null,
        Action<IntPtr>?                                    onSurfaceDisposing               = null) =>
        new(_saves, _config, new LocalizationData(), null,
            _ => { },
            () => { },
            onVolumeChanged                  ?? (_ => { }),
            onFilterChanged                  ?? (_ => { }),
            onMenuMusicToggled               ?? (_ => { }),
            onVideoFilterChanged             ?? (_ => { }),
            onVideoFilterOverlayChanged      ?? (_ => { }),
            onVideoColorFilterChanged        ?? (_ => { }),
            _ => { },
            onOverscanModeChanged            ?? (_ => { }),
            _ => { }, (_, _, _, _) => { }, (_, _, _) => { },
            bgImage: default,
            onSurfaceDisposing: onSurfaceDisposing);

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
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        Assert.That(screen.CanResume, Is.True);
    }

    [Test]
    public void CanResume_ReturnsTrue_WhenAutoSaveExists()
    {
        _saves.HasAutoSave.Returns(true);
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
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        Assert.That(screen.IsItemEnabled(1), Is.True);
    }

    // ---- Show ----

    [Test]
    public void Show_SetsIsVisible_AndResetsToMainScreen()
    {
        using var screen = CreateScreen();
        // Navigate away from Main
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return); // enter Settings (Down skips disabled Resume → lands on Settings)
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
        _saves.SlotExists(2).Returns(true);

        screen.Show();
        Assert.That(screen.CanResume, Is.True);
    }

    // ---- Navigation ----

    [Test]
    public void HandleKey_WhenNotVisible_ReturnsFalse()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Return); // select New Game → sets IsVisible = false
        bool consumed = screen.HandleKey(SDL.Keycode.Down);
        Assert.That(consumed, Is.False);
    }

    [Test]
    public void HandleKey_Down_SkipsDisabledResume_LandsOnSettings()
    {
        using var screen = CreateScreen();
        // From index 0 (New Game), Down should skip index 1 (Resume, disabled) → land on 2 (Settings)
        screen.HandleKey(SDL.Keycode.Down);
        Assert.That(screen.SelectedIndex, Is.EqualTo(2));
    }

    [Test]
    public void HandleKey_Down_DoesNotSkipResume_WhenSaveExists()
    {
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        Assert.That(screen.SelectedIndex, Is.EqualTo(1)); // Resume is enabled
    }

    [Test]
    public void HandleKey_Escape_OnMainScreen_DoesNothing()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.ExitChosen += () => fired = true;
        screen.HandleKey(SDL.Keycode.Escape);
        Assert.That(fired, Is.False);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    [Test]
    public void HandleKey_Escape_OnSettingsScreen_ReturnsToMain()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);   // skip to Settings (index 2)
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));

        screen.HandleKey(SDL.Keycode.Escape);
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
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(fired,            Is.True);
        Assert.That(screen.IsVisible, Is.False);
    }

    [Test]
    public void HandleKey_Return_OnResume_NavigatesToResumeSlots_WhenSaveExists()
    {
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);   // SelectedIndex → 1 (Resume, now enabled)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.ResumeSlots));
    }

    [Test]
    public void HandleKey_Return_OnSettings_NavigatesToSettingsScreen()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);   // skip to Settings
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Settings: Window Mode single toggle ----

    [Test]
    public void Settings_GetCurrentItems_HasFiveItems_WithVideoSubmenuAndBack()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down); // Settings
        screen.HandleKey(SDL.Keycode.Return);
        string[] items = screen.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(7)); // Video, Sound, Keyboard Controls, Gamepad Controls, D-Pad/Stick toggle, Language, ← Back
        Assert.That(items[0], Is.EqualTo("Video"));
        Assert.That(items[6], Does.StartWith("←"));
    }

    // ---- Sound screen ----

    // Helper: navigate to Settings → Sound
    private static void OpenSoundScreen(MainMenuScreen screen)
    {
        screen.HandleKey(SDL.Keycode.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Down);   // to Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return); // enter Sound
    }

    [Test]
    public void Sound_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void Sound_GetCurrentItems_ReturnsFiveItems()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        // Volume + Audio Filter + EQ + Menu Music + Back
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(5));
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
            _saves, config, new LocalizationData(), null,
            _ => { }, () => { },
            v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });

        OpenSoundScreen(screen);          // SelectedIndex = 0 (Volume)
        screen.HandleKey(SDL.Keycode.Left);
        Assert.That(config.Volume, Is.EqualTo(55));
        Assert.That(received, Is.EqualTo(55));
    }

    [Test]
    public void Sound_VolumeRight_IncreasesVolume()
    {
        var config = new AppConfig { Volume = 60 };
        int received = -1;
        using var screen = new MainMenuScreen(
            _saves, config, new LocalizationData(), null,
            _ => { }, () => { },
            v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });

        OpenSoundScreen(screen);
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(config.Volume, Is.EqualTo(65));
        Assert.That(received, Is.EqualTo(65));
    }

    [Test]
    public void Sound_FilterSelect_UpdatesConfigAndCallsBack()
    {
        var config = new AppConfig { AudioFilter = "Default" };
        AudioFilterMode? received = null;
        using var screen = new MainMenuScreen(
            _saves, config, new LocalizationData(), null,
            _ => { }, () => { },
            _ => { }, mode => received = mode, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });

        OpenSoundScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);   // index 1 = Audio Filter item
        screen.HandleKey(SDL.Keycode.Return); // enter AudioFilter sub-screen
        screen.HandleKey(SDL.Keycode.Down);   // index 1 = Warm
        screen.HandleKey(SDL.Keycode.Return); // select Warm → returns to Sound

        Assert.That(config.AudioFilter, Is.EqualTo("Warm"));
        Assert.That(received, Is.EqualTo(AudioFilterMode.Warm));
    }

    [Test]
    public void Sound_MenuMusicToggle_UpdatesConfigAndCallsBack()
    {
        var config = new AppConfig { MainMenuMusicEnabled = true };
        bool received = true;
        using var screen = new MainMenuScreen(
            _saves, config, new LocalizationData(), null,
            _ => { }, () => { },
            _ => { }, _ => { }, on => received = on, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });

        OpenSoundScreen(screen);
        for (int i = 0; i < 3; i++) screen.HandleKey(SDL.Keycode.Down); // Music is at index 3
        screen.HandleKey(SDL.Keycode.Return);

        Assert.That(config.MainMenuMusicEnabled, Is.False);
        Assert.That(received, Is.False);
    }

    [Test]
    public void Sound_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        for (int i = 0; i < 4; i++) screen.HandleKey(SDL.Keycode.Down); // Back is at index 4
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Audio Filter sub-screen ----

    private static void OpenAudioFilterScreen(MainMenuScreen screen)
    {
        screen.HandleKey(SDL.Keycode.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Down);   // Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return);                             // enter Sound
        screen.HandleKey(SDL.Keycode.Down);                              // Audio Filter item (index 1)
        screen.HandleKey(SDL.Keycode.Return);                            // enter AudioFilter screen
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
        // 8 filter modes + Back
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(9));
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
        screen.HandleKey(SDL.Keycode.Down);   // index 1 = Warm
        screen.HandleKey(SDL.Keycode.Return); // select Warm
        Assert.That(_config.AudioFilter, Is.EqualTo("Warm"));
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void AudioFilter_SelectMode_InvokesCallback()
    {
        AudioFilterMode? received = null;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { },
            _ => { }, mode => received = mode, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        OpenAudioFilterScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);   // Warm
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(received, Is.EqualTo(AudioFilterMode.Warm));
    }

    [Test]
    public void AudioFilter_Back_ReturnsToSound()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        for (int i = 0; i < 7; i++) screen.HandleKey(SDL.Keycode.Down); // Back is at index 7
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void AudioFilter_Escape_ReturnsToSound()
    {
        using var screen = CreateScreen();
        OpenAudioFilterScreen(screen);
        screen.HandleKey(SDL.Keycode.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void Sound_Escape_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        screen.HandleKey(SDL.Keycode.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Localization: audio filter display names and BindNone ----

    [Test]
    public void AudioFilter_GetTitle_UsesLocalizedTitle()
    {
        var loc = new LocalizationData { AudioFilterTitle = "FILT CUSTOM" };
        using var screen = new MainMenuScreen(_saves, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        OpenAudioFilterScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("FILT CUSTOM"));
    }

    [Test]
    public void AudioFilter_GetCurrentItems_UsesLocalizedDefaultName()
    {
        var loc = new LocalizationData { AudioFilterDefault = "TestDefault" };
        using var screen = new MainMenuScreen(_saves, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        OpenAudioFilterScreen(screen);
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("TestDefault"));
    }

    [Test]
    public void Sound_AudioFilterItem_UsesLocalizedLabel()
    {
        var loc = new LocalizationData { AudioFilterLabel = "TestLabel" };
        using var screen = new MainMenuScreen(_saves, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
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
        screen.HandleKey(SDL.Keycode.Down);   // Settings
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return); // Keyboard Controls (index 2)
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("(none)"));
    }

    [Test]
    public void KeyboardBindings_UnboundKey_UsesLocalizedBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, "DPadUp");
        var loc = new LocalizationData { BindNone = "(unset)" };
        using var screen = new MainMenuScreen(_saves, _config, loc, null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        screen.HandleKey(SDL.Keycode.Down);   // Settings
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return); // Keyboard Controls (index 2)
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("(unset)"));
    }

    [Test]
    public void GamepadBindings_UnboundButton_ShowsBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("W", null);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);   // Settings
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);   // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);   // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return); // GamepadBindings
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("(none)"));
    }

    [Test]
    public void GamepadBindings_DPadUpBinding_ShowsLocalizedText_NotRawIdentifier()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("W", "DPadUp");
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);   // Settings
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);   // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);   // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return); // GamepadBindings

        Assert.That(screen.GetCurrentItems()[0], Does.Contain("D-Pad Up"));
        Assert.That(screen.GetCurrentItems()[0], Does.Not.Contain("DPadUp"));
    }

    // ---- Video screen ----

    private static void OpenVideoScreen(MainMenuScreen screen)
    {
        screen.HandleKey(SDL.Keycode.Down);   // Settings (index 2, Resume disabled)
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        screen.HandleKey(SDL.Keycode.Return); // enter Video (index 0)
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
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);   // ← Back (index 4 in GDI mode)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    [Test]
    public void Video_FilterSubMenu_SelectsFilterAndCallsBack()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        using var screen = CreateScreen(onVideoFilterChanged: mode => received = mode);
        OpenVideoScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);   // Video Filter (index 1)
        screen.HandleKey(SDL.Keycode.Return); // → VideoFilter sub-menu
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.VideoFilter));
        screen.HandleKey(SDL.Keycode.Return); // select first filter (index 0)
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
        Assert.That(received, Is.Not.Null);
    }

    // ---- VideoFilter sub-menu ----

    private static void OpenVideoFilterSubMenu(MainMenuScreen screen)
    {
        OpenVideoScreen(screen);
        if (NEShim.Platform.PlatformDetector.IsD3D11Active) screen.HandleKey(SDL.Keycode.Down); // skip Presets(0) → Window(1)
        screen.HandleKey(SDL.Keycode.Down);   // Video Filter (index 1 GDI / index 2 D3D11)
        screen.HandleKey(SDL.Keycode.Return); // → VideoFilter sub-menu
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
    public void VideoFilter_GetCurrentItems_ReturnsEightItems()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        // All D3D11Supported filters available on both D3D11 and SDL_GPU: 7 filters + Back = 8
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(8));
    }

    [Test]
    public void VideoFilter_CurrentFilter_HasCheckmark()
    {
        using var screen = CreateScreen();
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(screen);
        var items = screen.GetCurrentItems();
        Assert.That(items[0], Does.StartWith("✓")); // PixelPerfect is at index 0 in GdiSupported
    }

    [Test]
    public void VideoFilter_SelectFilter_UpdatesConfig()
    {
        using var screen = CreateScreen();
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(SDL.Keycode.Down);   // move to Bilinear (index 1)
        screen.HandleKey(SDL.Keycode.Return); // select Bilinear
        Assert.That(_config.VideoFilter, Is.EqualTo("Bilinear"));
    }

    [Test]
    public void VideoFilter_SelectFilter_FiresCallback()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        using var screen = CreateScreen(onVideoFilterChanged: m => received = m);
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(SDL.Keycode.Down);   // move to Bilinear (index 1)
        screen.HandleKey(SDL.Keycode.Return); // select Bilinear
        Assert.That(received, Is.EqualTo(NEShim.Rendering.VideoFilterMode.Bilinear));
    }

    [Test]
    public void VideoFilter_SelectFilter_NavigatesBackToVideo()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(SDL.Keycode.Return); // select any filter
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    [Test]
    public void VideoFilter_Back_NavigatesBackToVideo()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        var itemCount = screen.GetCurrentItems().Length;
        for (int i = 0; i < itemCount - 1; i++) screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return); // Back
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    // ---- VideoFilter: overlay conflict (D3D11 mode) ----
    // D3D11Supported order: PixelPerfect(0), Bilinear(1), CrtScanlines(2), CrtPhosphor(3), CrtScreen(4), NtscComposite(5), Xbr(6), Back(7)

    [Test]
    public void VideoFilter_D3D11_SelectFilter_WhenOverlayMatches_ClearsOverlay()
    {
        using var screen = CreateScreen();
        NEShim.Platform.PlatformDetector.SetD3D11Active(true);
        _config.VideoFilterOverlay = "CrtScanlines";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(SDL.Keycode.Down);   // Bilinear (1)
        screen.HandleKey(SDL.Keycode.Down);   // CrtScanlines (2)
        screen.HandleKey(SDL.Keycode.Return); // select CrtScanlines — matches overlay
        Assert.That(_config.VideoFilterOverlay, Is.EqualTo("None"));
    }

    [Test]
    public void VideoFilter_D3D11_SelectFilter_WhenOverlayMatches_FiresOverlayCallback()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        bool callbackFired = false;
        using var screen = CreateScreen(onVideoFilterOverlayChanged: m => { callbackFired = true; received = m; });
        NEShim.Platform.PlatformDetector.SetD3D11Active(true);
        _config.VideoFilterOverlay = "CrtScanlines";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return); // select CrtScanlines
        Assert.That(callbackFired, Is.True);
        Assert.That(received, Is.Null);
    }

    [Test]
    public void VideoFilter_D3D11_SelectFilter_WhenOverlayDiffers_KeepsOverlay()
    {
        using var screen = CreateScreen();
        NEShim.Platform.PlatformDetector.SetD3D11Active(true);
        _config.VideoFilterOverlay = "CrtPhosphor";
        OpenVideoFilterSubMenu(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return); // select CrtScanlines — overlay is CrtPhosphor (different)
        Assert.That(_config.VideoFilterOverlay, Is.EqualTo("CrtPhosphor"));
    }

    // ---- VideoOverlay (overlay entry absent from filter submenu) ----

    [Test]
    public void VideoFilter_GetCurrentItems_ReturnsEightItems_OverlayEntryAbsent()
    {
        using var screen = CreateScreen();
        OpenVideoFilterSubMenu(screen);
        // 7 structural filters + Back; the overlay filter selector lives in its own submenu
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(8));
    }

    [Test]
    public void Video_OverscanCycle_UpdatesConfigAndCallsBack()
    {
        NEShim.Rendering.OverscanMode? received = null;
        _config.OverscanMode = "Overscan";
        using var screen = CreateScreen(onOverscanModeChanged: mode => received = mode);
        OpenVideoScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);   // Video Filter (index 1)
        screen.HandleKey(SDL.Keycode.Down);   // Overscan (index 2 in GDI mode)
        screen.HandleKey(SDL.Keycode.Return);
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
        screen.HandleKey(SDL.Keycode.Down); // 0 → 2 (skips disabled Resume)
        screen.HandleKey(SDL.Keycode.Down); // 2 → 3
        Assert.That(screen.SelectedIndex, Is.EqualTo(3));

        screen.HandleKey(SDL.Keycode.Down); // 3 → wraps to 0
        Assert.That(screen.SelectedIndex, Is.EqualTo(0));
    }

    // ---- Key binding uniqueness ----

    [Test]
    public void KeyBindings_AssignDuplicateKey_ClearsOldAction()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);   // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return); // Keyboard Controls (index 2)
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.KeyboardBindings));

        screen.HandleKey(SDL.Keycode.Down);   // P1 Down (index 1)
        screen.HandleKey(SDL.Keycode.Return); // start rebind
        screen.HandleKey(SDL.Keycode.W);      // bind "W" — already used by P1 Up

        Assert.That(_config.InputMappings["P1 Down"].Key, Is.EqualTo("W"));
        Assert.That(_config.InputMappings["P1 Up"].Key,   Is.Null); // cleared
    }

    // ---- HandleGamepadNav ----
    // Main screen: New Game(0), Resume(1 disabled), Settings(2), Exit(3)

    [Test]
    public void HandleGamepadNav_WhenNotVisible_DoesNothing()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Return); // New Game → IsVisible = false
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
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));

        screen.HandleGamepadNav(new MenuNavInput { Back = true });
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    [Test]
    public void HandleGamepadNav_DuringRebinding_Ignores()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return);  // KeyboardBindings (index 2)
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding "P1 Up"
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
        screen.HandleKey(SDL.Keycode.Z);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void HandleKey_Space_ActsAsConfirm()
    {
        using var screen = CreateScreen();
        bool fired = false;
        screen.NewGameChosen += () => fired = true;
        screen.HandleKey(SDL.Keycode.Space);
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
        screen.HandleKey(SDL.Keycode.Down); // 0 → 2 (skips disabled)
        screen.HandleKey(SDL.Keycode.Down); // 2 → 3
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(fired,            Is.True);
        Assert.That(screen.IsVisible, Is.False);
    }

    // ---- Escape during rebinding ----

    [Test]
    public void HandleKey_Escape_DuringKeyRebinding_CancelsRebind()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return);  // KeyboardBindings (index 2)
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Up
        Assert.That(screen.RebindingAction, Is.Not.Null);

        screen.HandleKey(SDL.Keycode.Escape);
        Assert.That(screen.RebindingAction, Is.Null);
    }

    [Test]
    public void HandleKey_Escape_DuringGamepadRebinding_CancelsRebind()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Up
        Assert.That(screen.GamepadRebindingAction, Is.Not.Null);

        screen.HandleKey(SDL.Keycode.Escape);
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
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Up
        Assert.That(screen.GamepadRebindingAction, Is.Not.Null);

        string? msg = screen.HandleGamepadButtonPress("Start");
        Assert.That(msg, Is.EqualTo("Start is reserved for the menu"));
        Assert.That(screen.GamepadRebindingAction, Is.Null);
    }

    [Test]
    public void HandleGamepadButtonPress_NormalButton_SetsBindingAndReturnsNull()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Up

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
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.GetTitle(), Is.EqualTo("SETTINGS"));
    }

    [Test]
    public void GetTitle_KeyboardBindings_ReturnsKeyboardControls()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return); // KeyboardBindings (index 2)
        Assert.That(screen.GetTitle(), Is.EqualTo("KEYBOARD CONTROLS"));
    }

    [Test]
    public void GetTitle_KeyboardBindings_DuringRebind_ContainsActionLabel()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);   // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);   // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return); // KeyboardBindings (index 2)
        screen.HandleKey(SDL.Keycode.Return); // start rebinding P1 Up
        Assert.That(screen.GetTitle(), Does.Contain("UP"));
    }

    [Test]
    public void GetTitle_GamepadBindings_ReturnsGamepadControls()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.GetTitle(), Is.EqualTo("GAMEPAD CONTROLS"));
    }

    [Test]
    public void GetTitle_GamepadBindings_DuringRebind_ContainsActionLabel()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Up
        Assert.That(screen.GetTitle(), Does.Contain("UP"));
    }

    [Test]
    public void GetTitle_ResumeSlots_ReturnsLoadGame()
    {
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Resume (enabled)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.GetTitle(), Is.EqualTo("LOAD GAME"));
    }

    // ---- ResumeSlots ----

    [Test]
    public void ResumeSlots_Back_ReturnsToMain()
    {
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Resume
        screen.HandleKey(SDL.Keycode.Return);  // → ResumeSlots
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.ResumeSlots));

        // Last item in the list is "← Back"
        string[] items = screen.GetCurrentItems();
        for (int i = 0; i < items.Length - 1; i++) screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    [Test]
    public void ResumeSlots_LoadSlot_FiresResumeChosenAndHidesScreen()
    {
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        bool fired = false;
        screen.ResumeChosen += () => fired = true;

        screen.HandleKey(SDL.Keycode.Down);    // Resume (enabled)
        screen.HandleKey(SDL.Keycode.Return);  // → ResumeSlots, first item = slot 0
        screen.HandleKey(SDL.Keycode.Return);  // activate slot 0 → load → ResumeChosen

        Assert.That(fired,            Is.True);
        Assert.That(screen.IsVisible, Is.False);
    }

    [Test]
    public void ResumeSlots_WhenAutoSaveSelected_CallsAutoLoad()
    {
        _saves.HasAutoSave.Returns(true);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Resume (enabled — autosave exists)
        screen.HandleKey(SDL.Keycode.Return);  // → ResumeSlots; autosave is index 0
        screen.HandleKey(SDL.Keycode.Return);  // activate autosave
        _saves.Received(1).AutoLoad();
    }

    [Test]
    public void ResumeSlots_WhenSlotSelected_CallsLoadSlot()
    {
        _saves.SlotExists(2).Returns(true);
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Resume (enabled — slot 2 exists)
        screen.HandleKey(SDL.Keycode.Return);  // → ResumeSlots; slot 2 is the only slot (index 0)
        screen.HandleKey(SDL.Keycode.Return);  // activate slot 2
        _saves.Received(1).LoadSlot(2);
    }

    // ---- GamepadBindings ----

    [Test]
    public void GamepadBindings_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.GamepadBindings));
    }

    [Test]
    public void GamepadBindings_GetCurrentItems_HasNineItems()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(9)); // 8 actions + Back
    }

    [Test]
    public void GamepadBindings_SelectAction_SetsGamepadRebindingAction()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Return);  // index 0 → P1 Up
        Assert.That(screen.GamepadRebindingAction, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void GamepadBindings_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        for (int i = 0; i < 8; i++) screen.HandleKey(SDL.Keycode.Down); // navigate to Back (index 8)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    [Test]
    public void GamepadBindings_AssignDuplicateButton_ClearsOldAction()
    {
        _config.InputMappings["P1 Up"].GamepadButton   = "A";
        _config.InputMappings["P1 Down"].GamepadButton = "B";

        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // P1 Down (index 1)
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Down

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
            _saves, _config, new LocalizationData(), null,
            fs => received = fs, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        OpenVideoScreen(screen);
        screen.HandleKey(SDL.Keycode.Return); // Window Mode (index 0, already selected)
        Assert.That(received, Is.True); // Windowed → Fullscreen (toggled to true)
    }

    [Test]
    public void Video_FpsToggle_UpdatesConfig()
    {
        bool initial = _config.ShowFps;
        using var screen = CreateScreen();
        OpenVideoScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);   // FPS Overlay (index 3 in GDI mode)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.ShowFps, Is.EqualTo(!initial));
    }

    // ---- Settings D-Pad/Stick toggle ----

    [Test]
    public void Settings_DpadStickToggle_ShowsLinkedLabel_WhenEnabled()
    {
        _config.GamepadDpadStickInterchangeable = true;
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down); // Settings
        screen.HandleKey(SDL.Keycode.Return);

        Assert.That(screen.GetCurrentItems()[4], Is.EqualTo("D-Pad / Analog Stick: Linked"));
    }

    [Test]
    public void Settings_DpadStickToggle_ShowsSeparateLabel_WhenDisabled()
    {
        _config.GamepadDpadStickInterchangeable = false;
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down); // Settings
        screen.HandleKey(SDL.Keycode.Return);

        Assert.That(screen.GetCurrentItems()[4], Is.EqualTo("D-Pad / Analog Stick: Separate"));
    }

    [Test]
    public void Settings_DpadStickToggle_UpdatesConfig()
    {
        bool initial = _config.GamepadDpadStickInterchangeable;
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down); // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down); // D-Pad/Stick toggle (index 4)
        screen.HandleKey(SDL.Keycode.Return);

        Assert.That(_config.GamepadDpadStickInterchangeable, Is.EqualTo(!initial));
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    [Test]
    public void Settings_LanguageItem_ExplicitLanguageCode_ShowsNativeName()
    {
        // Covers CurrentLanguageName's non-Auto branch (LanguageRegistry.FindByCode lookup) —
        // every other Settings test leaves _config.Language at its "Auto" default.
        _config.Language = "french";
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down); // Settings
        screen.HandleKey(SDL.Keycode.Return);

        Assert.That(screen.GetCurrentItems()[5], Does.Contain("Français"));
    }

    [Test]
    public void Settings_LanguageItem_UnknownLanguageCode_FallsBackToRawCode()
    {
        // Covers CurrentLanguageName's "?? code" fallback when FindByCode returns null.
        _config.Language = "not-a-real-language";
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down); // Settings
        screen.HandleKey(SDL.Keycode.Return);

        Assert.That(screen.GetCurrentItems()[5], Does.Contain("not-a-real-language"));
    }

    // ---- Settings Back ----

    [Test]
    public void Settings_Back_ReturnsToMain()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        for (int i = 0; i < 6; i++) screen.HandleKey(SDL.Keycode.Down); // to Back (index 6)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Main));
    }

    // ---- KeyboardBindings Back ----

    [Test]
    public void KeyboardBindings_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return);  // KeyboardBindings (index 2)
        for (int i = 0; i < 8; i++) screen.HandleKey(SDL.Keycode.Down); // to Back (index 8)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- Volume at boundaries ----

    [Test]
    public void Sound_VolumeLeft_AtZero_DoesNotGoNegative()
    {
        _config.Volume = 0;
        int received = 999;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        OpenSoundScreen(screen);
        screen.HandleKey(SDL.Keycode.Left); // already at 0 — no change
        Assert.That(_config.Volume, Is.EqualTo(0));
        Assert.That(received,       Is.EqualTo(999)); // callback not invoked
    }

    [Test]
    public void Sound_VolumeRight_AtMax_DoesNotExceed100()
    {
        _config.Volume = 100;
        int received = 999;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, v => received = v, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });
        OpenSoundScreen(screen);
        screen.HandleKey(SDL.Keycode.Right); // already at 100 — no change
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
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding P1 Up (index 0)
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
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);  // GamepadBindings

        // OpenMenu entry is at index 8 (after the 8 NES button entries)
        for (int i = 0; i < 8; i++) screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);  // start rebinding OpenMenu
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
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Down);    // skip Keyboard Controls (index 2)
        screen.HandleKey(SDL.Keycode.Down);    // Gamepad Controls (index 3)
        screen.HandleKey(SDL.Keycode.Return);  // GamepadBindings
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(10)); // 8 NES + OpenMenu + Back
    }

    // ---- ActiveNesButton ----

    [Test]
    public void ActiveNesButton_KeyboardBindings_FirstItem_ReturnsP1Up()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);    // Settings
        screen.HandleKey(SDL.Keycode.Return);
        screen.HandleKey(SDL.Keycode.Down);    // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return);  // Keyboard Controls (index 2)
        // SelectedIndex is 0 = P1 Up
        Assert.That(screen.ActiveNesButton, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void ActiveNesButton_KeyboardBindings_BackEntry_ReturnsNull()
    {
        using var screen = CreateScreen();
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);  // Settings
        screen.HandleKey(SDL.Keycode.Down);    // skip Video (index 0)
        screen.HandleKey(SDL.Keycode.Down);    // skip Sound (index 1)
        screen.HandleKey(SDL.Keycode.Return);  // Keyboard Controls (index 2)
        // Navigate to the last item (Back, configKey = "")
        for (int i = 0; i < 8; i++) screen.HandleKey(SDL.Keycode.Down);
        Assert.That(screen.ActiveNesButton, Is.Null);
    }

    [Test]
    public void ActiveNesButton_NonBindingScreen_ReturnsNull()
    {
        using var screen = CreateScreen();
        Assert.That(screen.ActiveNesButton, Is.Null); // Main screen
    }

    // ---- Video screen Overlay cycle (D3D11 mode, index 3) ----

    // In D3D11 mode the Video screen is: Presets(0), Window(1), Filter(2), Overlay(3), Motion(4), Picture(5), Overscan(6), FPS(7), Back(8).
    private static void OpenVideoScreenD3D11(MainMenuScreen screen)
    {
        NEShim.Platform.PlatformDetector.SetD3D11Active(true);
        OpenVideoScreen(screen);
    }

    [Test]
    public void Video_D3D11_ItemCount_IsNine()
    {
        using var screen = CreateScreen();
        OpenVideoScreenD3D11(screen);
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(9));
    }

    [Test]
    public void Video_D3D11_OverlayItem_ShowsCurrentValue()
    {
        using var screen = CreateScreen();
        _config.VideoFilterOverlay = "None";
        OpenVideoScreenD3D11(screen);
        Assert.That(screen.GetCurrentItems()[3], Does.Contain("None"));
    }

    [Test]
    public void Video_D3D11_OverlayCycle_FromNone_SetsCrtScanlines()
    {
        using var screen = CreateScreen();
        _config.VideoFilterOverlay = "None";
        OpenVideoScreenD3D11(screen);
        screen.HandleKey(SDL.Keycode.Down); // Window (1)
        screen.HandleKey(SDL.Keycode.Down); // Filter (2)
        screen.HandleKey(SDL.Keycode.Down); // Overlay (3)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.VideoFilterOverlay, Is.EqualTo("CrtScanlines"));
    }

    [Test]
    public void Video_D3D11_OverlayCycle_FromNone_FiresCallback()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        using var screen = CreateScreen(onVideoFilterOverlayChanged: m => received = m);
        _config.VideoFilterOverlay = "None";
        OpenVideoScreenD3D11(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(received, Is.EqualTo(NEShim.Rendering.VideoFilterMode.CrtScanlines));
    }

    [Test]
    public void Video_D3D11_OverlayCycle_SkipsPrimaryFilter()
    {
        using var screen = CreateScreen();
        _config.VideoFilter        = "CrtScanlines";
        _config.VideoFilterOverlay = "None";
        OpenVideoScreenD3D11(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return); // None → skip CrtScanlines (primary) → CrtPhosphor
        Assert.That(_config.VideoFilterOverlay, Is.EqualTo("CrtPhosphor"));
    }

    [Test]
    public void Video_D3D11_OverlayCycle_CyclesBackToNone()
    {
        using var screen = CreateScreen();
        _config.VideoFilterOverlay = "CrtScreen";
        OpenVideoScreenD3D11(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return); // CrtScreen → None
        Assert.That(_config.VideoFilterOverlay, Is.EqualTo("None"));
    }

    // ---- VideoFilter sub-menu D3D11 item count ----

    [Test]
    public void VideoFilter_D3D11_GetCurrentItems_ReturnsEightItems()
    {
        using var screen = CreateScreen();
        NEShim.Platform.PlatformDetector.SetD3D11Active(true);
        OpenVideoFilterSubMenu(screen);
        // D3D11Supported = 7 filters + Back = 8 items (no Overlay → entry)
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(8));
    }

    // ---- VideoPicture sub-screen (D3D11 mode, Picture is index 5 in Video) ----

    private static void OpenVideoPictureScreen(MainMenuScreen screen)
    {
        OpenVideoScreenD3D11(screen);
        for (int i = 0; i < 5; i++) screen.HandleKey(SDL.Keycode.Down); // to Picture (index 5)
        screen.HandleKey(SDL.Keycode.Return);
    }

    [Test]
    public void VideoPicture_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.VideoPicture));
    }

    [Test]
    public void VideoPicture_GetCurrentItems_ReturnsSevenItems()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(7));
    }

    [Test]
    public void VideoPicture_GetTitle_ReturnsPictureTitle()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("PICTURE"));
    }

    [Test]
    public void VideoPicture_BrightnessSlider_Default_ReturnsSliderDataAtMidpoint()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        var slider = screen.GetCurrentSliderData(1);
        Assert.That(slider.HasValue, Is.True);
        Assert.That(slider!.Value.ValueText, Is.EqualTo("0"));
        Assert.That(slider.Value.Fill01, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void VideoPicture_RightOnBrightness_IncreasesConfigBrightness()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // to Brightness (index 1)
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.VideoBrightness, Is.EqualTo(1));
    }

    [Test]
    public void VideoPicture_LeftOnBrightness_DecreasesConfigBrightness()
    {
        using var screen = CreateScreen();
        _config.VideoBrightness = 10;
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // to Brightness
        screen.HandleKey(SDL.Keycode.Left);
        Assert.That(_config.VideoBrightness, Is.EqualTo(9));
    }

    [Test]
    public void VideoPicture_RightOnContrast_IncreasesConfigContrast()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Brightness
        screen.HandleKey(SDL.Keycode.Down); // Contrast (index 2)
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.VideoContrast, Is.EqualTo(1));
    }

    [Test]
    public void VideoPicture_RightOnSaturation_IncreasesConfigSaturation()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Brightness
        screen.HandleKey(SDL.Keycode.Down); // Contrast
        screen.HandleKey(SDL.Keycode.Down); // Saturation (index 3)
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.VideoSaturation, Is.EqualTo(1));
    }

    [Test]
    public void VideoPicture_RightOnHue_IncreasesConfigHue()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Brightness
        screen.HandleKey(SDL.Keycode.Down); // Contrast
        screen.HandleKey(SDL.Keycode.Down); // Saturation
        screen.HandleKey(SDL.Keycode.Down); // Hue (index 4)
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.VideoHue, Is.EqualTo(1));
    }

    [Test]
    public void VideoPicture_LeftOnHue_DecreasesConfigHue()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Brightness
        screen.HandleKey(SDL.Keycode.Down); // Contrast
        screen.HandleKey(SDL.Keycode.Down); // Saturation
        screen.HandleKey(SDL.Keycode.Down); // Hue (index 4)
        screen.HandleKey(SDL.Keycode.Left);
        Assert.That(_config.VideoHue, Is.EqualTo(-1));
    }

    [Test]
    public void VideoPicture_HueAt100_RightDoesNotExceed()
    {
        using var screen = CreateScreen();
        _config.VideoHue = 100;
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Brightness
        screen.HandleKey(SDL.Keycode.Down); // Contrast
        screen.HandleKey(SDL.Keycode.Down); // Saturation
        screen.HandleKey(SDL.Keycode.Down); // Hue (index 4)
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.VideoHue, Is.EqualTo(100));
    }

    [Test]
    public void VideoPicture_HueAtMinus100_LeftDoesNotExceed()
    {
        using var screen = CreateScreen();
        _config.VideoHue = -100;
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Brightness
        screen.HandleKey(SDL.Keycode.Down); // Contrast
        screen.HandleKey(SDL.Keycode.Down); // Saturation
        screen.HandleKey(SDL.Keycode.Down); // Hue (index 4)
        screen.HandleKey(SDL.Keycode.Left);
        Assert.That(_config.VideoHue, Is.EqualTo(-100));
    }

    [Test]
    public void VideoPicture_BrightnessAt100_RightDoesNotExceed()
    {
        using var screen = CreateScreen();
        _config.VideoBrightness = 100;
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.VideoBrightness, Is.EqualTo(100));
    }

    [Test]
    public void VideoPicture_BrightnessAtMinus100_LeftDoesNotExceed()
    {
        using var screen = CreateScreen();
        _config.VideoBrightness = -100;
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Left);
        Assert.That(_config.VideoBrightness, Is.EqualTo(-100));
    }

    [Test]
    public void VideoPicture_ColorPreset_Activate_CyclesColorFilter()
    {
        using var screen = CreateScreen();
        _config.VideoColorFilter = "None";
        OpenVideoPictureScreen(screen);
        screen.HandleKey(SDL.Keycode.Return); // activate ColorPreset (index 0)
        Assert.That(_config.VideoColorFilter, Is.Not.EqualTo("None"));
    }

    [Test]
    public void VideoPicture_Reset_SetsAllValuesToZeroAndColorToNone()
    {
        using var screen = CreateScreen();
        _config.VideoBrightness  = 50;
        _config.VideoContrast    = -30;
        _config.VideoSaturation  = 25;
        _config.VideoHue         = 75;
        _config.VideoColorFilter = "Warm";
        OpenVideoPictureScreen(screen);
        for (int i = 0; i < 5; i++) screen.HandleKey(SDL.Keycode.Down); // to Reset (index 5)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.VideoBrightness,  Is.EqualTo(0));
        Assert.That(_config.VideoContrast,    Is.EqualTo(0));
        Assert.That(_config.VideoSaturation,  Is.EqualTo(0));
        Assert.That(_config.VideoHue,         Is.EqualTo(0));
        Assert.That(_config.VideoColorFilter, Is.EqualTo("None"));
    }

    [Test]
    public void VideoPicture_Back_NavigatesToVideoScreen()
    {
        using var screen = CreateScreen();
        OpenVideoPictureScreen(screen);
        for (int i = 0; i < 6; i++) screen.HandleKey(SDL.Keycode.Down); // to Back (index 6)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    // ---- VideoFilter D3D11: Xbr filter ----

    [Test]
    public void VideoFilter_D3D11_SelectXbr_UpdatesConfig()
    {
        using var screen = CreateScreen();
        NEShim.Platform.PlatformDetector.SetD3D11Active(true);
        OpenVideoFilterSubMenu(screen);
        for (int i = 0; i < 6; i++) screen.HandleKey(SDL.Keycode.Down); // to Xbr (index 6)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.VideoFilter, Is.EqualTo("Xbr"));
    }

    // ---- Audio EQ sub-screen ----

    private static void OpenAudioEqScreen(MainMenuScreen screen)
    {
        OpenSoundScreen(screen);
        screen.HandleKey(SDL.Keycode.Down);   // Audio Filter item (index 1)
        screen.HandleKey(SDL.Keycode.Down);   // EQ item (index 2)
        screen.HandleKey(SDL.Keycode.Return); // enter AudioEq screen
    }

    [Test]
    public void AudioEq_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.AudioEq));
    }

    [Test]
    public void AudioEq_GetTitle_ReturnsAudioEqTitle()
    {
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("AUDIO EQ"));
    }

    [Test]
    public void AudioEq_GetCurrentItems_ReturnsFiveItems()
    {
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(5));
    }

    [Test]
    public void AudioEq_BassItem_ShowsCurrentValue()
    {
        _config.AudioEqBass = 9;
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        var slider = screen.GetCurrentSliderData(0);
        Assert.That(slider.HasValue, Is.True);
        Assert.That(slider!.Value.ValueText, Is.EqualTo("+9"));
    }

    [Test]
    public void AudioEq_RightKey_OnBass_IncreasesGain()
    {
        _config.AudioEqBass = 0;
        (int bass, int mid, int treble) received = default;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            (_, _, _, _) => { }, (b, m, t) => received = (b, m, t));
        OpenAudioEqScreen(screen); // SelectedItem = 0 (Bass)
        screen.HandleKey(SDL.Keycode.Right);
        Assert.That(_config.AudioEqBass, Is.EqualTo(1));
        Assert.That(received.bass, Is.EqualTo(1));
    }

    [Test]
    public void AudioEq_LeftKey_OnTreble_DecreasesGain()
    {
        _config.AudioEqTreble = 6;
        (int bass, int mid, int treble) received = default;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            (_, _, _, _) => { }, (b, m, t) => received = (b, m, t));
        OpenAudioEqScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // Mid
        screen.HandleKey(SDL.Keycode.Down); // Treble (index 2)
        screen.HandleKey(SDL.Keycode.Left);
        Assert.That(_config.AudioEqTreble, Is.EqualTo(5));
        Assert.That(received.treble, Is.EqualTo(5));
    }

    [Test]
    public void AudioEq_Reset_SetsAllGainsToZero()
    {
        _config.AudioEqBass   = 4;
        _config.AudioEqMid    = -2;
        _config.AudioEqTreble = 8;
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        for (int i = 0; i < 3; i++) screen.HandleKey(SDL.Keycode.Down); // to Reset (index 3)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.AudioEqBass,   Is.EqualTo(0));
        Assert.That(_config.AudioEqMid,    Is.EqualTo(0));
        Assert.That(_config.AudioEqTreble, Is.EqualTo(0));
    }

    [Test]
    public void AudioEq_Back_ReturnsToSound()
    {
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        for (int i = 0; i < 4; i++) screen.HandleKey(SDL.Keycode.Down); // to Back (index 4)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void AudioEq_Escape_ReturnsToSound()
    {
        using var screen = CreateScreen();
        OpenAudioEqScreen(screen);
        screen.HandleKey(SDL.Keycode.Escape);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Sound));
    }

    [Test]
    public void Sound_EqItem_ShowsFlat_WhenAllZero()
    {
        _config.AudioEqBass = _config.AudioEqMid = _config.AudioEqTreble = 0;
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        Assert.That(screen.GetCurrentItems()[2], Does.Contain("Flat"));
    }

    [Test]
    public void Sound_EqItem_ShowsCustom_WhenAnyNonZero()
    {
        _config.AudioEqTreble = -6;
        using var screen = CreateScreen();
        OpenSoundScreen(screen);
        Assert.That(screen.GetCurrentItems()[2], Does.Contain("Custom"));
    }

    // ---- Language sub-screen ----

    private static void OpenLanguageScreen(MainMenuScreen screen)
    {
        screen.HandleKey(SDL.Keycode.Down);   // Settings (index 2)
        screen.HandleKey(SDL.Keycode.Return); // enter Settings
        for (int i = 0; i < 5; i++) screen.HandleKey(SDL.Keycode.Down); // Language (index 5)
        screen.HandleKey(SDL.Keycode.Return); // enter Language
    }

    [Test]
    public void Language_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenLanguageScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Language));
    }

    [Test]
    public void Language_GetTitle_ReturnsLanguageTitle()
    {
        using var screen = CreateScreen();
        OpenLanguageScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("LANGUAGE"));
    }

    [Test]
    public void Language_GetCurrentItems_HasAutoAtIndex0()
    {
        using var screen = CreateScreen();
        OpenLanguageScreen(screen);
        Assert.That(screen.GetCurrentItems()[0], Does.Contain("Auto"));
    }

    [Test]
    public void Language_SelectAuto_SetsConfigLanguageToAuto()
    {
        _config.Language = "english";
        bool callbackFired = false;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            lang => { callbackFired = true; },
            (_, _, _, _) => { }, (_, _, _) => { });
        OpenLanguageScreen(screen);
        screen.HandleKey(SDL.Keycode.Return); // select Auto (index 0)
        Assert.That(_config.Language, Is.EqualTo("Auto"));
        Assert.That(callbackFired, Is.True);
    }

    [Test]
    public void Language_SelectSpecificLanguage_UpdatesConfig()
    {
        _config.Language = "Auto";
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            _ => { },
            (_, _, _, _) => { }, (_, _, _) => { });
        OpenLanguageScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // first language (index 1)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.Language, Is.Not.EqualTo("Auto"));
    }

    [Test]
    public void Language_Auto_HasCheckmark_WhenConfigIsAuto()
    {
        _config.Language = "Auto";
        using var screen = CreateScreen();
        OpenLanguageScreen(screen);
        Assert.That(screen.GetCurrentItems()[0], Does.StartWith("✓"));
    }

    [Test]
    public void Language_Back_ReturnsToSettings()
    {
        using var screen = CreateScreen();
        OpenLanguageScreen(screen);
        int backIndex = screen.GetCurrentItems().Length - 1;
        for (int i = 0; i < backIndex; i++) screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Settings));
    }

    // ---- VideoMotionEffect sub-screen (D3D11 only) ----

    private static void OpenVideoMotionEffectScreen(MainMenuScreen screen)
    {
        OpenVideoScreenD3D11(screen);
        for (int i = 0; i < 4; i++) screen.HandleKey(SDL.Keycode.Down); // MotionEffect (index 4)
        screen.HandleKey(SDL.Keycode.Return);
    }

    [Test]
    public void VideoMotionEffect_D3D11_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenVideoMotionEffectScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.VideoMotionEffect));
    }

    [Test]
    public void VideoMotionEffect_D3D11_GetTitle_ReturnsMotionEffectTitle()
    {
        using var screen = CreateScreen();
        OpenVideoMotionEffectScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("MOTION EFFECT"));
    }

    [Test]
    public void VideoMotionEffect_D3D11_GetCurrentItems_ContainsAllModes()
    {
        using var screen = CreateScreen();
        OpenVideoMotionEffectScreen(screen);
        var allModes = NEShim.Rendering.VideoMotionEffectModeParser.AllModes;
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(allModes.Length + 1));
    }

    [Test]
    public void VideoMotionEffect_D3D11_SelectMode_UpdatesConfig()
    {
        _config.VideoMotionEffect = "None";
        using var screen = CreateScreen();
        OpenVideoMotionEffectScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // CrtJitter (index 1)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.VideoMotionEffect, Is.EqualTo("CrtJitter"));
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    [Test]
    public void VideoMotionEffect_D3D11_SelectMode_FiresCallback()
    {
        NEShim.Rendering.VideoMotionEffectMode? received = null;
        using var screen = new MainMenuScreen(
            _saves, _config, new LocalizationData(), null,
            _ => { }, () => { }, _ => { }, _ => { }, _ => { },
            _ => { }, _ => { }, _ => { }, m => received = m, _ => { }, _ => { },
            (_, _, _, _) => { }, (_, _, _) => { });
        OpenVideoMotionEffectScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // CrtJitter
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(received, Is.EqualTo(NEShim.Rendering.VideoMotionEffectMode.CrtJitter));
    }

    [Test]
    public void VideoMotionEffect_D3D11_Back_ReturnsToVideo()
    {
        using var screen = CreateScreen();
        OpenVideoMotionEffectScreen(screen);
        int backIndex = screen.GetCurrentItems().Length - 1;
        for (int i = 0; i < backIndex; i++) screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    // ---- VideoPresets sub-screen (D3D11 only) ----

    private static void OpenVideoPresetsScreen(MainMenuScreen screen)
    {
        OpenVideoScreenD3D11(screen);
        // Presets is at index 0 — already selected
        screen.HandleKey(SDL.Keycode.Return);
    }

    [Test]
    public void VideoPresets_D3D11_NavigateTo_SetsCurrentScreen()
    {
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.VideoPresets));
    }

    [Test]
    public void VideoPresets_D3D11_GetTitle_ReturnsPresetsTitle()
    {
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        Assert.That(screen.GetTitle(), Is.EqualTo("VIDEO PRESETS"));
    }

    [Test]
    public void VideoPresets_D3D11_GetCurrentItems_HasNonePlusAllPresetsAndBack()
    {
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(7));
    }

    [Test]
    public void VideoPresets_D3D11_None_HasCheckmark_WhenNoActivePreset()
    {
        _config.VideoPreset = "None";
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        Assert.That(screen.GetCurrentItems()[0], Does.StartWith("✓"));
    }

    [Test]
    public void VideoPresets_D3D11_SelectPreset_AppliesAllFields()
    {
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        screen.HandleKey(SDL.Keycode.Down); // NoFilters (index 1)
        screen.HandleKey(SDL.Keycode.Down); // LivingRoom (index 2)
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(_config.VideoPreset, Is.EqualTo("LivingRoom"));
        Assert.That(_config.VideoFilter, Is.EqualTo("CrtScreen"));
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    [Test]
    public void VideoPresets_D3D11_SelectNone_ClearsPreset()
    {
        _config.VideoPreset = "Sharp";
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        screen.HandleKey(SDL.Keycode.Return); // None (index 0)
        Assert.That(_config.VideoPreset, Is.EqualTo("None"));
    }

    [Test]
    public void VideoPresets_D3D11_ActivePreset_HasCheckmark()
    {
        _config.VideoPreset = "Sharp";
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        // None(0), NoFilters(1), LivingRoom(2), Arcade(3), Sharp(4)
        Assert.That(screen.GetCurrentItems()[4], Does.StartWith("✓"));
        Assert.That(screen.GetCurrentItems()[0], Does.Not.StartWith("✓"));
    }

    [Test]
    public void VideoPresets_D3D11_Back_ReturnsToVideo()
    {
        using var screen = CreateScreen();
        OpenVideoPresetsScreen(screen);
        int backIndex = screen.GetCurrentItems().Length - 1;
        for (int i = 0; i < backIndex; i++) screen.HandleKey(SDL.Keycode.Down);
        screen.HandleKey(SDL.Keycode.Return);
        Assert.That(screen.CurrentScreen, Is.EqualTo(MainMenuScreen.Screen.Video));
    }

    // ---- Main handler GetItems / GetTitle (exercises MainHandler.GetItems body) ----

    [Test]
    public void Main_GetCurrentItems_ReturnsFourItems()
    {
        using var screen = CreateScreen();
        Assert.That(screen.GetCurrentItems().Length, Is.EqualTo(4));
    }

    [Test]
    public void Main_GetCurrentItems_WhenNoSave_ResumeLabelHasNoSaveSuffix()
    {
        using var screen = CreateScreen();
        string[] items = screen.GetCurrentItems();
        // CanResume = false → label appends SlotNoSave
        Assert.That(items[1], Does.Contain(new NEShim.Localization.LocalizationData().SlotNoSave));
    }

    [Test]
    public void Main_GetCurrentItems_WhenSaveExists_ResumeLabelHasNoSuffix()
    {
        _saves.SlotExists(0).Returns(true);
        using var screen = CreateScreen();
        string[] items = screen.GetCurrentItems();
        // CanResume = true → label is just the resume string, no SlotNoSave suffix
        Assert.That(items[1], Does.Not.Contain(new NEShim.Localization.LocalizationData().SlotNoSave));
    }

    [Test]
    public void Main_GetTitle_ContainsMenu()
    {
        using var screen = CreateScreen();
        Assert.That(screen.GetTitle(), Does.Contain("MENU"));
    }

    // ---- onSurfaceDisposing callback ----
    // CreateScreen never loads a background image, so Background and _scaledBackground both
    // stay IntPtr.Zero — the only state safely testable without a real SDL surface reaching the
    // native SDL.DestroySurface call inside Dispose() (a boundary-crossing concern, not a unit
    // test one).

    [Test]
    public void Dispose_WithNoBackgroundLoaded_DoesNotInvokeCallback()
    {
        bool invoked = false;
        var screen = CreateScreen(onSurfaceDisposing: _ => invoked = true);
        screen.Dispose();
        Assert.That(invoked, Is.False);
    }
}
