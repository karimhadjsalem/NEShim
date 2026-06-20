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
internal class InGameMenuTests
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

    private InGameMenu CreateMenu(
        Action?                                            onExitToDesktop            = null,
        Action?                                            onResetGame                = null,
        Action?                                            onReturnToMainMenu         = null,
        Action?                                            onConfigSaved              = null,
        Action<int>?                                       onVolumeChanged            = null,
        Action<AudioFilterMode>?                           onFilterChanged            = null,
        Action<NEShim.Rendering.VideoFilterMode>?          onVideoFilterChanged       = null,
        Action<NEShim.Rendering.VideoColorFilterMode>?     onVideoColorFilterChanged  = null,
        Action<NEShim.Rendering.OverscanMode>?             onOverscanModeChanged      = null)
    {
        return new InGameMenu(
            _saveStates,
            _config,
            new LocalizationData(),
            onExitToDesktop            ?? (() => { }),
            onResetGame                ?? (() => { }),
            onReturnToMainMenu         ?? (() => { }),
            _ => { },
            onConfigSaved              ?? (() => { }),
            onVolumeChanged            ?? (_ => { }),
            onFilterChanged            ?? (_ => { }),
            onVideoFilterChanged       ?? (_ => { }),
            onVideoColorFilterChanged  ?? (_ => { }),
            onOverscanModeChanged      ?? (_ => { }),
            _ => { });
    }

    // Helper: create an empty slot-state file so SlotExists returns true
    private void CreateSlotFile(int slot) =>
        File.WriteAllBytes(Path.Combine(_tempDir, $"slot{slot}.state"), Array.Empty<byte>());

    // ---- Open / Close ----

    [Test]
    public void Open_SetsIsOpenTrue_AndCurrentScreenIsRoot()
    {
        var menu = CreateMenu();
        menu.Open();
        Assert.That(menu.IsOpen,   Is.True);
        Assert.That(menu.Current,  Is.EqualTo(InGameMenu.Screen.Root));
    }

    [Test]
    public void Close_WhenOpen_SetsIsOpenFalse()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.Close();
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void Open_WhenAlreadyOpen_DoesNotResetSelection()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Down); // move cursor to index 1
        menu.Open();   // second open should be ignored
        Assert.That(menu.SelectedItem, Is.EqualTo(1));
    }

    // ---- Key handling ----

    [Test]
    public void HandleKey_WhenClosed_ReturnsFalse()
    {
        var menu     = CreateMenu();
        bool consumed = menu.HandleKey(Keys.Return);
        Assert.That(consumed, Is.False);
    }

    [Test]
    public void HandleKey_Escape_WhenRootScreen_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Escape);
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HandleKey_Escape_WhenSubScreen_ReturnsToRoot_WithoutClosing()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate into Save Slot Select (index 2)
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.SaveSlotSelect));

        menu.HandleKey(Keys.Escape);

        Assert.That(menu.Current,  Is.EqualTo(InGameMenu.Screen.Root));
        Assert.That(menu.IsOpen,   Is.True);
    }

    [Test]
    public void HandleKey_Down_IncreasesSelectedItem()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Down);
        Assert.That(menu.SelectedItem, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_Up_AtFirst_WrapsToLast()
    {
        var menu = CreateMenu();
        menu.Open();
        // Root has 8 items (indices 0–7). Up from 0 wraps to 7.
        menu.HandleKey(Keys.Up);
        Assert.That(menu.SelectedItem, Is.EqualTo(7));
    }

    [Test]
    public void HandleKey_Return_OnResume_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        // SelectedItem = 0 (Resume)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.IsOpen, Is.False);
    }

    // ---- Confirm screens ----

    [Test]
    public void HandleKey_Return_OnLoadGame_NavigatesToConfirmLoad_WithDefaultYes()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open();
        // Navigate to Load Game (index 4): 0→1→2→3→4 (enabled because slot exists)
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        Assert.That(menu.Current,      Is.EqualTo(InGameMenu.Screen.ConfirmLoad));
        Assert.That(menu.SelectedItem, Is.EqualTo(0)); // default is "Yes"
    }

    [Test]
    public void ConfirmLoad_Yes_LoadsAndClosesMenu()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);   // enter ConfirmLoad (selection at "Yes")
        menu.HandleKey(Keys.Return);   // confirm Yes

        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void ConfirmLoad_No_ReturnsToRoot_WithoutLoading()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);   // ConfirmLoad, at "Yes"
        menu.HandleKey(Keys.Down);     // move to "No"
        menu.HandleKey(Keys.Return);   // activate "No"

        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Root));
        Assert.That(menu.IsOpen,  Is.True);
    }

    [Test]
    public void HandleKey_Return_OnReturnToMainMenu_NavigatesToConfirmMainMenu_WithDefaultNo()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate to "Return to Main Menu" (index 6).
        // With no save, Load Game (index 4) is skipped: 0→1→2→3→5→6
        for (int i = 0; i < 5; i++)
            menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        Assert.That(menu.Current,      Is.EqualTo(InGameMenu.Screen.ConfirmMainMenu));
        Assert.That(menu.SelectedItem, Is.EqualTo(1)); // default is "No"
    }

    [Test]
    public void HandleKey_Return_OnExit_NavigatesToConfirmExit_WithDefaultNo()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate to "Exit" (index 7): 0→1→2→3→5→6→7
        for (int i = 0; i < 6; i++)
            menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        Assert.That(menu.Current,      Is.EqualTo(InGameMenu.Screen.ConfirmExit));
        Assert.That(menu.SelectedItem, Is.EqualTo(1)); // default is "No"
    }

    [Test]
    public void ConfirmMainMenu_Yes_ClosesMenuAndInvokesCallback()
    {
        bool callbackInvoked = false;
        var menu = CreateMenu(onReturnToMainMenu: () => callbackInvoked = true);
        menu.Open();
        for (int i = 0; i < 5; i++)
            menu.HandleKey(Keys.Down); // land on index 6
        menu.HandleKey(Keys.Return);   // enter ConfirmMainMenu (selection at "No")
        menu.HandleKey(Keys.Up);       // move to "Yes"
        menu.HandleKey(Keys.Return);   // confirm

        Assert.That(menu.IsOpen,        Is.False);
        Assert.That(callbackInvoked,    Is.True);
    }

    [Test]
    public void ConfirmMainMenu_No_ReturnsToRoot_WithoutCallback()
    {
        bool callbackInvoked = false;
        var menu = CreateMenu(onReturnToMainMenu: () => callbackInvoked = true);
        menu.Open();
        for (int i = 0; i < 5; i++)
            menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);   // ConfirmMainMenu, at "No"
        menu.HandleKey(Keys.Return);   // activate "No"

        Assert.That(menu.Current,     Is.EqualTo(InGameMenu.Screen.Root));
        Assert.That(menu.IsOpen,      Is.True);
        Assert.That(callbackInvoked,  Is.False);
    }

    [Test]
    public void ConfirmExit_Yes_ClosesMenuAndInvokesCallback()
    {
        bool exitInvoked = false;
        var menu = CreateMenu(onExitToDesktop: () => exitInvoked = true);
        menu.Open();
        for (int i = 0; i < 6; i++)
            menu.HandleKey(Keys.Down); // land on index 7 (Exit)
        menu.HandleKey(Keys.Return);   // ConfirmExit, at "No"
        menu.HandleKey(Keys.Up);       // move to "Yes"
        menu.HandleKey(Keys.Return);   // confirm

        Assert.That(menu.IsOpen,    Is.False);
        Assert.That(exitInvoked,    Is.True);
    }

    [Test]
    public void ConfirmExit_No_ReturnsToRoot_WithoutCallback()
    {
        bool exitInvoked = false;
        var menu = CreateMenu(onExitToDesktop: () => exitInvoked = true);
        menu.Open();
        for (int i = 0; i < 6; i++)
            menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);   // ConfirmExit, at "No"
        menu.HandleKey(Keys.Return);   // activate "No"

        Assert.That(menu.Current,    Is.EqualTo(InGameMenu.Screen.Root));
        Assert.That(menu.IsOpen,     Is.True);
        Assert.That(exitInvoked,     Is.False);
    }

    // ---- IsItemEnabled / disabled Load Game ----

    [Test]
    public void IsItemEnabled_LoadGame_ReturnsFalse_WhenActiveSlotEmpty()
    {
        var menu = CreateMenu();
        menu.Open();
        // Active slot is 0 by default; no save file exists
        Assert.That(menu.IsItemEnabled(4), Is.False);
    }

    [Test]
    public void IsItemEnabled_LoadGame_ReturnsTrue_WhenActiveSlotHasSave()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open();
        Assert.That(menu.IsItemEnabled(4), Is.True);
    }

    [Test]
    public void MoveCursor_SkipsDisabledLoadGame_LandsOnSettings()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate to Save Game (index 3)
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        Assert.That(menu.SelectedItem, Is.EqualTo(3));

        // One more Down should skip index 4 (disabled) and land on index 5 (Settings)
        menu.HandleKey(Keys.Down);
        Assert.That(menu.SelectedItem, Is.EqualTo(5));
    }

    // ---- Events ----

    [Test]
    public void Opened_EventFires_WhenMenuOpened()
    {
        var menu    = CreateMenu();
        bool fired  = false;
        menu.Opened += () => fired = true;
        menu.Open();
        Assert.That(fired, Is.True);
    }

    [Test]
    public void Closed_EventFires_WhenMenuClosed()
    {
        var menu    = CreateMenu();
        bool fired  = false;
        menu.Closed += () => fired = true;
        menu.Open();
        menu.Close();
        Assert.That(fired, Is.True);
    }

    // ---- Settings screen ----

    [Test]
    public void Settings_FpsToggle_TogglesShowFps()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate to Settings (index 5 with no save — skips disabled Load Game)
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));

        // Navigate to Video → Down×2 to FPS (index 3 in the 5-item GDI Video layout)
        menu.HandleKey(Keys.Down);   // skip Keyboard Controls (index 0)
        menu.HandleKey(Keys.Down);   // select Video (index 2)
        menu.HandleKey(Keys.Return); // enter Video screen
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));

        menu.HandleKey(Keys.Down);   // skip Video Filter (index 1)
        menu.HandleKey(Keys.Down);   // skip Overscan (index 2 in GDI mode)
        menu.HandleKey(Keys.Down);   // select FPS (index 3 in GDI mode)
        bool before = _config.ShowFps;
        menu.HandleKey(Keys.Return);
        Assert.That(_config.ShowFps, Is.EqualTo(!before));
    }

    [Test]
    public void Settings_FpsToggle_InvokesConfigSavedCallback()
    {
        bool saved = false;
        var menu   = CreateMenu(onConfigSaved: () => saved = true);
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        menu.HandleKey(Keys.Down);   // skip Keyboard Controls (index 0)
        menu.HandleKey(Keys.Down);   // select Video (index 2)
        menu.HandleKey(Keys.Return); // enter Video screen
        menu.HandleKey(Keys.Down);   // skip Video Filter (index 1)
        menu.HandleKey(Keys.Down);   // skip Overscan (index 2 in GDI mode)
        menu.HandleKey(Keys.Down);   // select FPS (index 3 in GDI mode)
        menu.HandleKey(Keys.Return); // toggle FPS
        Assert.That(saved, Is.True);
    }

    // ---- Save Slot Select screen ----

    [Test]
    public void SaveSlotSelect_Activate_UpdatesActiveSlot()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate to "Select Save Slot" (index 2)
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter SaveSlotSelect
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.SaveSlotSelect));

        // Move to slot 2 (index 2) and select
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        Assert.That(_saveStates.ActiveSlot, Is.EqualTo(2));
        Assert.That(_config.ActiveSlot,     Is.EqualTo(2));
        Assert.That(menu.Current,           Is.EqualTo(InGameMenu.Screen.Root));
    }

    // ---- Key rebind flow ----

    [Test]
    public void KeyBindings_EnterRebindMode_SetsRebindingAction()
    {
        var menu = CreateMenu();
        menu.Open();
        // Settings (index 5, skipping disabled Load Game at 4): 4 Downs then Return
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        menu.HandleKey(Keys.Return); // select Key Bindings (index 0)
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.KeyboardBindings));

        // Select first binding (Up → P1 Up)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.RebindingAction, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void KeyBindings_WhileRebinding_PressKey_UpdatesConfigAndClearsRebind()
    {
        bool saved = false;
        var menu   = CreateMenu(onConfigSaved: () => saved = true);
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Key Bindings
        menu.HandleKey(Keys.Return); // start rebind for P1 Up

        menu.HandleKey(Keys.T); // assign T to P1 Up
        Assert.That(menu.RebindingAction, Is.Null);
        Assert.That(_config.InputMappings["P1 Up"].Key, Is.EqualTo("T"));
        Assert.That(saved, Is.True);
    }

    [Test]
    public void KeyBindings_WhileRebinding_PressEscape_CancelsRebind()
    {
        string originalKey = _config.InputMappings["P1 Up"].Key!;
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Key Bindings
        menu.HandleKey(Keys.Return); // start rebind

        menu.HandleKey(Keys.Escape); // cancel
        Assert.That(menu.RebindingAction, Is.Null);
        Assert.That(_config.InputMappings["P1 Up"].Key, Is.EqualTo(originalKey));
    }

    // ---- GetTitle / GetCurrentItems ----

    [Test]
    public void GetTitle_ReturnsCorrectTitle_ForEachScreen()
    {
        var menu = CreateMenu();
        menu.Open();
        Assert.That(menu.GetTitle(), Is.EqualTo("PAUSED"));

        // Navigate to ConfirmMainMenu
        for (int i = 0; i < 5; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);
        Assert.That(menu.GetTitle(), Is.EqualTo("RETURN TO MAIN MENU?"));
    }

    [Test]
    public void GetCurrentItems_Root_ReturnsEightItems()
    {
        var menu = CreateMenu();
        menu.Open();
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(8));
    }

    [Test]
    public void GetCurrentItems_ConfirmMainMenu_ReturnsTwoItems()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 5; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter ConfirmMainMenu
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(2));
        Assert.That(menu.GetCurrentItems()[0], Does.StartWith("Yes"));
        Assert.That(menu.GetCurrentItems()[1], Does.StartWith("No"));
    }

    [Test]
    public void GetCurrentItems_SaveSlotSelect_ReturnsNineItems()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter SaveSlotSelect

        string[] items = menu.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(9)); // 8 slots + ← Back
        Assert.That(items[0], Does.Contain("1")); // "Slot 1..."
        Assert.That(items[8], Does.StartWith("←"));
    }

    // ---- Settings: Window Mode toggle (single item) ----

    [Test]
    public void Settings_WindowMode_IsSingleItem_ShowingCurrentMode()
    {
        _config.WindowMode = "Fullscreen";
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings

        string[] items = menu.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(6)); // Keyboard Controls, Gamepad Controls, Video, Sound, Language, ← Back
        Assert.That(items[2], Is.EqualTo("Video"));

        // Window Mode lives in the Video sub-screen
        menu.HandleKey(Keys.Down);   // skip Keyboard Controls (index 0)
        menu.HandleKey(Keys.Down);   // select Video (index 2)
        menu.HandleKey(Keys.Return); // enter Video
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));
        string[] videoItems = menu.GetCurrentItems();
        Assert.That(videoItems[0], Does.Contain("Fullscreen"));
    }

    [Test]
    public void Settings_WindowMode_Activate_TogglesMode()
    {
        var menu = CreateMenu(onConfigSaved: () => { }); // need window mode toggle
        // Wire a custom toggle capture
        bool receivedFullscreen = false;
        var menuWithToggle = new InGameMenu(
            _saveStates, _config,
            new LocalizationData(),
            () => { }, () => { }, () => { },
            fs => receivedFullscreen = fs,
            () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });

        menuWithToggle.Open();
        _config.WindowMode = "Fullscreen";
        for (int i = 0; i < 4; i++) menuWithToggle.HandleKey(Keys.Down);
        menuWithToggle.HandleKey(Keys.Return); // Settings
        menuWithToggle.HandleKey(Keys.Down);   // skip Keyboard Controls (index 0)
        menuWithToggle.HandleKey(Keys.Down);   // select Video (index 2)
        menuWithToggle.HandleKey(Keys.Return); // enter Video
        // Window Mode is index 0 in Video — already selected
        menuWithToggle.HandleKey(Keys.Return); // activate — should toggle to Windowed

        Assert.That(receivedFullscreen, Is.False); // was Fullscreen, toggled to Windowed
    }

    // ---- Sound screen ----

    // Helper: navigate Open → Settings → Sound
    private void OpenSoundScreen(InGameMenu menu)
    {
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // to Settings
        menu.HandleKey(Keys.Return); // enter Settings
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down); // to Sound (index 3)
        menu.HandleKey(Keys.Return); // enter Sound
    }

    [Test]
    public void Sound_NavigateTo_SetsCurrentScreen()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Sound));
    }

    [Test]
    public void Sound_GetCurrentItems_ReturnsThreeItems()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        // Volume + Audio Filter + Back
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(3));
    }

    [Test]
    public void Sound_GetTitle_ReturnsSound()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        Assert.That(menu.GetTitle(), Is.EqualTo("SOUND"));
    }

    [Test]
    public void Sound_VolumeItem_ShowsCurrentVolume()
    {
        _config.Volume = 75;
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        Assert.That(menu.GetCurrentItems()[0], Does.Contain("75"));
    }

    [Test]
    public void Sound_LeftKey_DecreasesVolume()
    {
        _config.Volume = 50;
        int received = -1;
        var menu = CreateMenu(onVolumeChanged: v => received = v);
        OpenSoundScreen(menu); // SelectedItem = 0 (Volume)
        menu.HandleKey(Keys.Left);
        Assert.That(_config.Volume, Is.EqualTo(45));
        Assert.That(received, Is.EqualTo(45));
    }

    [Test]
    public void Sound_RightKey_IncreasesVolume()
    {
        _config.Volume = 50;
        int received = -1;
        var menu = CreateMenu(onVolumeChanged: v => received = v);
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Right);
        Assert.That(_config.Volume, Is.EqualTo(55));
        Assert.That(received, Is.EqualTo(55));
    }

    [Test]
    public void Sound_Volume_ClampedAtZero()
    {
        _config.Volume = 0;
        int received = -1;
        var menu = CreateMenu(onVolumeChanged: v => received = v);
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Left); // cannot go below 0
        Assert.That(_config.Volume, Is.EqualTo(0));
        Assert.That(received, Is.EqualTo(-1)); // callback not called when no change
    }

    [Test]
    public void Sound_Volume_ClampedAt100()
    {
        _config.Volume = 100;
        int received = -1;
        var menu = CreateMenu(onVolumeChanged: v => received = v);
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Right); // cannot go above 100
        Assert.That(_config.Volume, Is.EqualTo(100));
        Assert.That(received, Is.EqualTo(-1));
    }

    [Test]
    public void Sound_FilterSelect_UpdatesConfig()
    {
        _config.AudioFilter = "Default";
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Down);   // index 1 = Audio Filter item
        menu.HandleKey(Keys.Return); // enter AudioFilter sub-screen
        menu.HandleKey(Keys.Down);   // index 1 = Warm
        menu.HandleKey(Keys.Return); // select Warm → returns to Sound
        Assert.That(_config.AudioFilter, Is.EqualTo("Warm"));
    }

    [Test]
    public void Sound_FilterSelect_InvokesCallback()
    {
        AudioFilterMode? received = null;
        var menu = CreateMenu(onFilterChanged: mode => received = mode);
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Down);   // index 1 = Audio Filter item
        menu.HandleKey(Keys.Return); // enter AudioFilter sub-screen
        menu.HandleKey(Keys.Down);   // index 1 = Warm
        menu.HandleKey(Keys.Return); // select Warm → callback fired
        Assert.That(received, Is.EqualTo(AudioFilterMode.Warm));
    }

    [Test]
    public void Sound_Back_ReturnsToSettings()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        for (int i = 0; i < 2; i++) menu.HandleKey(Keys.Down); // Back is at index 2
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    // ---- Audio Filter sub-screen ----

    private static void OpenAudioFilterScreen(InGameMenu menu)
    {
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // Settings
        menu.HandleKey(Keys.Return);
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down); // Sound (index 3)
        menu.HandleKey(Keys.Return);                             // enter Sound
        menu.HandleKey(Keys.Down);                              // Audio Filter item (index 1)
        menu.HandleKey(Keys.Return);                            // enter AudioFilter screen
    }

    [Test]
    public void AudioFilter_NavigateTo_SetsCurrentScreen()
    {
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.AudioFilter));
    }

    [Test]
    public void AudioFilter_GetTitle_ReturnsAudioFilter()
    {
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        Assert.That(menu.GetTitle(), Is.EqualTo("AUDIO FILTER"));
    }

    [Test]
    public void AudioFilter_GetCurrentItems_ReturnsEightItems()
    {
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        // 7 filter modes + Back
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(8));
    }

    [Test]
    public void AudioFilter_ActiveFilter_ShowsCheckmark()
    {
        _config.AudioFilter = "Warm";
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        string[] items = menu.GetCurrentItems();
        Assert.That(items[0], Does.StartWith("  ")); // Default — not active
        Assert.That(items[1], Does.StartWith("✓"));  // Warm — active
    }

    [Test]
    public void AudioFilter_SelectMode_UpdatesConfigAndReturnsToSound()
    {
        _config.AudioFilter = "Default";
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        menu.HandleKey(Keys.Down);   // index 1 = Warm
        menu.HandleKey(Keys.Return); // select Warm
        Assert.That(_config.AudioFilter, Is.EqualTo("Warm"));
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Sound));
    }

    [Test]
    public void AudioFilter_SelectMode_InvokesCallback()
    {
        AudioFilterMode? received = null;
        var menu = CreateMenu(onFilterChanged: mode => received = mode);
        OpenAudioFilterScreen(menu);
        menu.HandleKey(Keys.Down);   // Warm
        menu.HandleKey(Keys.Return);
        Assert.That(received, Is.EqualTo(AudioFilterMode.Warm));
    }

    [Test]
    public void AudioFilter_Back_ReturnsToSound()
    {
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        for (int i = 0; i < 7; i++) menu.HandleKey(Keys.Down); // Back is at index 7
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Sound));
    }

    [Test]
    public void AudioFilter_Escape_ReturnsToSound()
    {
        var menu = CreateMenu();
        OpenAudioFilterScreen(menu);
        menu.HandleKey(Keys.Escape);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Sound));
    }

    [Test]
    public void Sound_Escape_ReturnsToSettings()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Escape);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    // ---- Localization: audio filter display names and BindNone ----

    [Test]
    public void AudioFilter_GetTitle_UsesLocalizedTitle()
    {
        var loc = new LocalizationData { AudioFilterTitle = "FILT CUSTOM" };
        var menu = new InGameMenu(_saveStates, _config, loc,
            () => { }, () => { }, () => { }, _ => { }, () => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenAudioFilterScreen(menu);
        Assert.That(menu.GetTitle(), Is.EqualTo("FILT CUSTOM"));
    }

    [Test]
    public void AudioFilter_GetCurrentItems_UsesLocalizedDefaultName()
    {
        var loc = new LocalizationData { AudioFilterDefault = "TestDefault" };
        var menu = new InGameMenu(_saveStates, _config, loc,
            () => { }, () => { }, () => { }, _ => { }, () => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenAudioFilterScreen(menu);
        Assert.That(menu.GetCurrentItems()[0], Does.Contain("TestDefault"));
    }

    [Test]
    public void Sound_AudioFilterItem_UsesLocalizedLabel()
    {
        var loc = new LocalizationData { AudioFilterLabel = "TestLabel" };
        var menu = new InGameMenu(_saveStates, _config, loc,
            () => { }, () => { }, () => { }, _ => { }, () => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        OpenSoundScreen(menu);
        Assert.That(menu.GetCurrentItems()[1], Does.Contain("TestLabel"));
    }

    [Test]
    public void Video_FilterItem_ContainsCrtPhosphor_WhenActive()
    {
        _config.VideoFilter = "CrtPhosphor";
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        Assert.That(menu.GetCurrentItems()[1], Does.Contain("CRT Phosphor"));
    }

    [Test]
    public void KeyboardBindings_UnboundKey_ShowsBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, "DPadUp");
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Key Bindings (index 0)
        Assert.That(menu.GetCurrentItems()[0], Does.Contain("(none)"));
    }

    [Test]
    public void KeyboardBindings_UnboundKey_UsesLocalizedBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, "DPadUp");
        var loc = new LocalizationData { BindNone = "(unset)" };
        var menu = new InGameMenu(_saveStates, _config, loc,
            () => { }, () => { }, () => { }, _ => { }, () => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Key Bindings (index 0)
        Assert.That(menu.GetCurrentItems()[0], Does.Contain("(unset)"));
    }

    [Test]
    public void GamepadBindings_UnboundButton_ShowsBindNone()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("W", null);
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings
        Assert.That(menu.GetCurrentItems()[0], Does.Contain("(none)"));
    }

    // ---- Video screen ----

    private static void OpenVideoScreen(InGameMenu menu)
    {
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // to Settings (index 5, skipping disabled)
        menu.HandleKey(Keys.Return); // enter Settings
        menu.HandleKey(Keys.Down);   // skip Keyboard Controls (index 0)
        menu.HandleKey(Keys.Down);   // select Video (index 2)
        menu.HandleKey(Keys.Return); // enter Video
    }

    [Test]
    public void Video_NavigateTo_SetsCurrentScreen()
    {
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));
    }

    [Test]
    public void Video_GetCurrentItems_ReturnsFiveItemsInGdiMode()
    {
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        // GDI mode: Window Mode, Video Filter, Overscan, FPS Overlay, ← Back
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(5));
    }

    [Test]
    public void Video_GetCurrentItems_DoesNotContainColorEffect_InGdiMode()
    {
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        Assert.That(menu.GetCurrentItems().Any(i => i.Contains("Color Effect")), Is.False);
    }

    [Test]
    public void Video_GetTitle_ReturnsVideo()
    {
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        Assert.That(menu.GetTitle(), Is.EqualTo("VIDEO"));
    }

    [Test]
    public void Video_Back_ReturnsToSettings()
    {
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // ← Back (index 4 in GDI mode)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    [Test]
    public void Video_FilterSubMenu_SelectsFilterAndCallsBack()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        var menu = CreateMenu(onVideoFilterChanged: f => received = f);
        _config.VideoFilter = "PixelPerfect";
        OpenVideoScreen(menu);
        menu.HandleKey(Keys.Down);   // select Video Filter (index 1)
        menu.HandleKey(Keys.Return); // enter VideoFilter sub-menu
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.VideoFilter));
        // GDI mode: [0]=Bilinear, [1]=PixelPerfect(✓), [2]=Back
        // index 0 is already selected (SelectedItem resets to 0 on NavigateTo)
        menu.HandleKey(Keys.Return); // select Bilinear
        Assert.That(received, Is.EqualTo(NEShim.Rendering.VideoFilterMode.Bilinear));
        Assert.That(_config.VideoFilter, Is.EqualTo("Bilinear"));
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));
    }

    // ---- VideoFilter sub-menu ----

    private void OpenVideoFilterSubMenu(InGameMenu menu)
    {
        OpenVideoScreen(menu);
        menu.HandleKey(Keys.Down);   // Video Filter (index 1)
        menu.HandleKey(Keys.Return); // → VideoFilter sub-menu
    }

    [Test]
    public void VideoFilter_NavigateTo_SetsCurrentScreen()
    {
        var menu = CreateMenu();
        OpenVideoFilterSubMenu(menu);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.VideoFilter));
    }

    [Test]
    public void VideoFilter_GetTitle_ReturnsVideoFilterTitle()
    {
        var menu = CreateMenu();
        OpenVideoFilterSubMenu(menu);
        Assert.That(menu.GetTitle(), Is.EqualTo("VIDEO FILTER"));
    }

    [Test]
    public void VideoFilter_GetCurrentItems_ReturnsThreeItemsInGdiMode()
    {
        var menu = CreateMenu();
        OpenVideoFilterSubMenu(menu);
        // GDI mode: [Bilinear, PixelPerfect, Back]
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(3));
    }

    [Test]
    public void VideoFilter_CurrentFilter_HasCheckmark()
    {
        var menu = CreateMenu();
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(menu);
        var items = menu.GetCurrentItems();
        Assert.That(items[1], Does.StartWith("✓")); // PixelPerfect is at index 1 in GdiSupported
    }

    [Test]
    public void VideoFilter_SelectFilter_UpdatesConfig()
    {
        var menu = CreateMenu();
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(menu);
        menu.HandleKey(Keys.Return); // select Bilinear (index 0)
        Assert.That(_config.VideoFilter, Is.EqualTo("Bilinear"));
    }

    [Test]
    public void VideoFilter_SelectFilter_FiresCallback()
    {
        NEShim.Rendering.VideoFilterMode? received = null;
        var menu = CreateMenu(onVideoFilterChanged: m => received = m);
        _config.VideoFilter = "PixelPerfect";
        OpenVideoFilterSubMenu(menu);
        menu.HandleKey(Keys.Return); // select Bilinear (index 0)
        Assert.That(received, Is.EqualTo(NEShim.Rendering.VideoFilterMode.Bilinear));
    }

    [Test]
    public void VideoFilter_SelectFilter_NavigatesBackToVideo()
    {
        var menu = CreateMenu();
        OpenVideoFilterSubMenu(menu);
        menu.HandleKey(Keys.Return); // select any filter
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));
    }

    [Test]
    public void VideoFilter_Back_NavigatesBackToVideo()
    {
        var menu = CreateMenu();
        OpenVideoFilterSubMenu(menu);
        // Back is at last index — navigate to it
        var itemCount = menu.GetCurrentItems().Length;
        for (int i = 0; i < itemCount - 1; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Back
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));
    }

    [Test]
    public void Video_OverscanCycle_UpdatesConfigAndCallsBack()
    {
        NEShim.Rendering.OverscanMode? received = null;
        var menu = CreateMenu(onOverscanModeChanged: m => received = m);
        _config.OverscanMode = "Overscan";
        OpenVideoScreen(menu);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);   // select Overscan (index 2 in GDI mode)
        menu.HandleKey(Keys.Return);
        Assert.That(received, Is.EqualTo(NEShim.Rendering.OverscanMode.Normal));
        Assert.That(_config.OverscanMode, Is.EqualTo("Normal"));
    }

    // ---- Rollover ----

    [Test]
    public void HandleKey_Down_AtLast_WrapsToFirst()
    {
        var menu = CreateMenu();
        menu.Open();
        // Navigate to Exit (index 7): skips disabled Load Game (4)
        for (int i = 0; i < 6; i++) menu.HandleKey(Keys.Down);
        Assert.That(menu.SelectedItem, Is.EqualTo(7));

        menu.HandleKey(Keys.Down); // wraps to index 0
        Assert.That(menu.SelectedItem, Is.EqualTo(0));
    }

    // ---- Key binding uniqueness ----

    [Test]
    public void KeyBindings_AssignDuplicateKey_ClearsOldAction()
    {
        // P1 Up is bound to "W" by default; binding "W" to P1 Down should clear P1 Up
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Key Bindings (index 0)
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.KeyboardBindings));

        menu.HandleKey(Keys.Down);   // P1 Down (index 1)
        menu.HandleKey(Keys.Return); // start rebind
        menu.HandleKey(Keys.W);      // bind "W" — currently used by P1 Up

        Assert.That(_config.InputMappings["P1 Down"].Key, Is.EqualTo("W"));
        Assert.That(_config.InputMappings["P1 Up"].Key,   Is.Null); // cleared
    }

    // ---- Gamepad rebind: Start reserved, B bindable ----

    // Helper: navigate to GamepadBindings screen
    private void OpenGamepadBindings(InGameMenu menu)
    {
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // Settings
        menu.HandleKey(Keys.Return);
        menu.HandleKey(Keys.Down); // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.GamepadBindings));
        menu.HandleKey(Keys.Return); // start rebind for P1 Up (index 0)
        Assert.That(menu.IsGamepadRebinding, Is.True);
    }

    [Test]
    public void GamepadRebind_PressStart_CancelsRebindAndReturnsReservedMessage()
    {
        var menu = CreateMenu();
        OpenGamepadBindings(menu);

        string? toast = menu.HandleGamepadButtonPress("Start");

        Assert.That(toast, Is.EqualTo("Start is reserved for the menu"));
        Assert.That(menu.IsGamepadRebinding, Is.False); // rebind cancelled
    }

    [Test]
    public void GamepadRebind_PressB_BindsButton()
    {
        var menu = CreateMenu();
        OpenGamepadBindings(menu);

        string? toast = menu.HandleGamepadButtonPress("B");

        Assert.That(toast, Is.Null);
        Assert.That(menu.IsGamepadRebinding, Is.False);
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.EqualTo("B"));
    }

    [Test]
    public void GamepadRebind_PressBack_BindsButton()
    {
        var menu = CreateMenu();
        OpenGamepadBindings(menu);

        string? toast = menu.HandleGamepadButtonPress("Back");

        Assert.That(toast, Is.Null);
        Assert.That(menu.IsGamepadRebinding, Is.False);
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.EqualTo("Back"));
    }

    // ---- HandleGamepadNav ----
    // Root screen, 8 items — geometry mirrored by MenuRenderer (but nav uses selection only)

    [Test]
    public void HandleGamepadNav_WhenClosed_DoesNothing()
    {
        var menu = CreateMenu();
        menu.HandleGamepadNav(new MenuNavInput { Down = true });
        Assert.That(menu.SelectedItem, Is.EqualTo(0));
    }

    [Test]
    public void HandleGamepadNav_NoInputs_DoesNothing()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleGamepadNav(new MenuNavInput());
        Assert.That(menu.SelectedItem, Is.EqualTo(0));
    }

    [Test]
    public void HandleGamepadNav_Down_MovesSelection()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleGamepadNav(new MenuNavInput { Down = true });
        Assert.That(menu.SelectedItem, Is.EqualTo(1));
    }

    [Test]
    public void HandleGamepadNav_Up_AtFirst_WrapsToLast()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleGamepadNav(new MenuNavInput { Up = true });
        Assert.That(menu.SelectedItem, Is.EqualTo(7));
    }

    [Test]
    public void HandleGamepadNav_Confirm_OnResume_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleGamepadNav(new MenuNavInput { Confirm = true });
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HandleGamepadNav_Back_OnRoot_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleGamepadNav(new MenuNavInput { Back = true });
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HandleGamepadNav_Back_OnSubScreen_NavigatesUp()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // → SaveSlotSelect
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.SaveSlotSelect));

        menu.HandleGamepadNav(new MenuNavInput { Back = true });
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Root));
        Assert.That(menu.IsOpen,  Is.True);
    }

    [Test]
    public void HandleGamepadNav_DuringKeyRebinding_Ignores()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // KeyboardBindings
        menu.HandleKey(Keys.Return); // start rebind
        Assert.That(menu.RebindingAction, Is.Not.Null);

        int itemBefore = menu.SelectedItem;
        menu.HandleGamepadNav(new MenuNavInput { Down = true });
        Assert.That(menu.SelectedItem, Is.EqualTo(itemBefore));
    }

    [Test]
    public void HandleGamepadNav_Left_OnSoundVolume_DecreasesVolume()
    {
        _config.Volume = 60;
        var menu = CreateMenu();
        menu.Open();
        // Navigate to Sound screen, select Volume (index 0)
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // Settings
        menu.HandleKey(Keys.Return);
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down); // Sound (index 3)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Sound));
        Assert.That(menu.SelectedItem, Is.EqualTo(0)); // Volume

        menu.HandleGamepadNav(new MenuNavInput { Left = true });
        Assert.That(_config.Volume, Is.EqualTo(55));
    }

    [Test]
    public void HandleGamepadNav_Right_OnSoundVolume_IncreasesVolume()
    {
        _config.Volume = 60;
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return);

        menu.HandleGamepadNav(new MenuNavInput { Right = true });
        Assert.That(_config.Volume, Is.EqualTo(65));
    }

    // ---- HandleKey Z / Space ----

    [Test]
    public void HandleKey_Z_ActsAsConfirm_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Z); // Resume (index 0) → Close()
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HandleKey_Space_ActsAsConfirm_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Space);
        Assert.That(menu.IsOpen, Is.False);
    }

    // ---- HandleKey_Escape during GamepadRebinding ----

    [Test]
    public void HandleKey_Escape_DuringGamepadRebinding_CancelsRebind()
    {
        var menu = CreateMenu();
        OpenGamepadBindings(menu);
        Assert.That(menu.IsGamepadRebinding, Is.True);

        menu.HandleKey(Keys.Escape);
        Assert.That(menu.IsGamepadRebinding, Is.False);
    }

    // ---- Root actions: Reset Game (1) and Save Game (3) ----

    [Test]
    public void HandleKey_Return_OnResetGame_InvokesCallbackAndClosesMenu()
    {
        bool reset = false;
        var menu = CreateMenu(onResetGame: () => reset = true);
        menu.Open();
        menu.HandleKey(Keys.Down); // index 1 (Reset Game)
        menu.HandleKey(Keys.Return);
        Assert.That(reset,       Is.True);
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HandleKey_Return_OnSaveGame_SavesAndClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down); // index 3 (Save Game)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.IsOpen, Is.False);
    }

    // ---- HandleGamepadButtonPress when not rebinding ----

    [Test]
    public void HandleGamepadButtonPress_WhenNotRebinding_ReturnsNull()
    {
        var menu = CreateMenu();
        menu.Open();
        string? result = menu.HandleGamepadButtonPress("A");
        Assert.That(result, Is.Null);
    }

    // ---- Close when already closed ----

    [Test]
    public void Close_WhenAlreadyClosed_DoesNothing()
    {
        var menu = CreateMenu();
        Assert.That(() => menu.Close(), Throws.Nothing);
        Assert.That(menu.IsOpen, Is.False);
    }

    // ---- GetTitle remaining screens ----

    [Test]
    public void GetTitle_SaveSlotSelect_ContainsSelectSlot()
    {
        var menu = CreateMenu();
        menu.Open();
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // SaveSlotSelect
        Assert.That(menu.GetTitle(), Does.Contain("SELECT SLOT"));
    }

    [Test]
    public void GetTitle_Settings_ReturnsSettings()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        Assert.That(menu.GetTitle(), Is.EqualTo("SETTINGS"));
    }

    [Test]
    public void GetTitle_KeyboardBindings_ReturnsKeyboardControls()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // KeyboardBindings (index 0)
        Assert.That(menu.GetTitle(), Is.EqualTo("KEYBOARD CONTROLS"));
    }

    [Test]
    public void GetTitle_KeyboardBindings_DuringRebind_ShowsActionName()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // KeyboardBindings
        menu.HandleKey(Keys.Return); // start rebind for P1 Up (index 0)
        Assert.That(menu.GetTitle(), Is.EqualTo("PRESS KEY FOR  UP"));
    }

    [Test]
    public void GetTitle_GamepadBindings_ReturnsGamepadControls()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings
        Assert.That(menu.GetTitle(), Is.EqualTo("GAMEPAD CONTROLS"));
    }

    [Test]
    public void GetTitle_GamepadBindings_DuringRebind_ShowsActionName()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings
        menu.HandleKey(Keys.Return); // start rebind for P1 Up (index 0)
        Assert.That(menu.GetTitle(), Is.EqualTo("PRESS BUTTON FOR  UP"));
    }

    [Test]
    public void GetTitle_ConfirmLoad_ReturnsLoadGame()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // Load Game (enabled at index 4)
        menu.HandleKey(Keys.Return); // ConfirmLoad
        Assert.That(menu.GetTitle(), Is.EqualTo("LOAD GAME?"));
    }

    [Test]
    public void GetTitle_ConfirmExit_ReturnsExitToDesktop()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 6; i++) menu.HandleKey(Keys.Down); // Exit (index 7)
        menu.HandleKey(Keys.Return); // ConfirmExit
        Assert.That(menu.GetTitle(), Is.EqualTo("EXIT TO DESKTOP?"));
    }

    // ---- GetCurrentItems remaining screens ----

    [Test]
    public void GetCurrentItems_ConfirmLoad_ReturnsTwoItems()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // ConfirmLoad
        string[] items = menu.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0], Does.Contain("Yes"));
        Assert.That(items[1], Does.Contain("No"));
    }

    [Test]
    public void GetCurrentItems_ConfirmExit_ReturnsTwoItems()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 6; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // ConfirmExit
        string[] items = menu.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0], Does.Contain("Yes"));
        Assert.That(items[1], Does.Contain("No"));
    }

    [Test]
    public void GetCurrentItems_KeyboardBindings_ReturnsNineItems()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // KeyboardBindings (index 0)
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(9));
    }

    [Test]
    public void GetCurrentItems_GamepadBindings_ReturnsNineItems()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(9));
    }

    // ---- Navigation: Settings Back, KeyboardBindings Back, GamepadBindings Back ----

    [Test]
    public void Settings_Back_ReturnsToRoot()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        for (int i = 0; i < 5; i++) menu.HandleKey(Keys.Down); // Back (index 5)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Root));
    }

    [Test]
    public void KeyboardBindings_Back_ReturnsToSettings()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // KeyboardBindings (index 0)
        for (int i = 0; i < 8; i++) menu.HandleKey(Keys.Down); // Back (index 8)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    [Test]
    public void GamepadBindings_Back_ReturnsToSettings()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings
        for (int i = 0; i < 8; i++) menu.HandleKey(Keys.Down); // Back (index 8)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    // ---- HandleGamepadNav during gamepad rebinding ----

    [Test]
    public void HandleGamepadNav_DuringGamepadRebinding_Ignores()
    {
        var menu = CreateMenu();
        OpenGamepadBindings(menu);
        Assert.That(menu.IsGamepadRebinding, Is.True);

        int itemBefore = menu.SelectedItem;
        menu.HandleGamepadNav(new MenuNavInput { Down = true });
        Assert.That(menu.SelectedItem, Is.EqualTo(itemBefore));
    }

    // ---- OverrideStartBindingProtection ----

    [Test]
    public void HandleGamepadButtonPress_StartPressed_OverrideEnabled_BindsStart()
    {
        _config.OverrideStartBindingProtection = true;
        var menu = CreateMenu();
        OpenGamepadBindings(menu);

        string? toast = menu.HandleGamepadButtonPress("Start");

        Assert.That(toast, Is.Null);
        Assert.That(menu.IsGamepadRebinding, Is.False);
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.EqualTo("Start"));
    }

    [Test]
    public void HandleGamepadButtonPress_OpenMenuAction_OverrideEnabled_UpdatesHotkeyMappings()
    {
        _config.OverrideStartBindingProtection = true;
        var menu = CreateMenu();
        menu.Open();

        // Navigate to GamepadBindings
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // Settings (index 5)
        menu.HandleKey(Keys.Return);
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings

        // The OpenMenu entry is at index 8 (after the 8 NES buttons)
        for (int i = 0; i < 8; i++) menu.HandleKey(Keys.Down); // index 8 = OpenMenu
        menu.HandleKey(Keys.Return); // start rebinding OpenMenu
        Assert.That(menu.GamepadRebindingAction, Is.EqualTo("OpenMenu"));

        string? toast = menu.HandleGamepadButtonPress("RightShoulder");

        Assert.That(toast, Is.Null);
        Assert.That(menu.IsGamepadRebinding, Is.False);
        Assert.That(_config.GamepadHotkeyMappings["OpenMenu"], Is.EqualTo("RightShoulder"));
    }

    [Test]
    public void GetCurrentItems_GamepadBindings_OverrideEnabled_ReturnsTenItems()
    {
        _config.OverrideStartBindingProtection = true;
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Down);   // Gamepad Controls (index 1)
        menu.HandleKey(Keys.Return); // GamepadBindings
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(10)); // 8 NES + OpenMenu + Back
    }

    // ---- ActiveNesButton ----

    [Test]
    public void ActiveNesButton_KeyboardBindings_FirstItem_ReturnsP1Up()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Keyboard Controls (index 0)
        // SelectedItem is 0 = P1 Up
        Assert.That(menu.ActiveNesButton, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void ActiveNesButton_KeyboardBindings_BackEntry_ReturnsNull()
    {
        var menu = CreateMenu();
        menu.Open();
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Keyboard Controls (index 0)
        // Navigate to the last item (Back, configKey = "")
        for (int i = 0; i < 8; i++) menu.HandleKey(Keys.Down);
        Assert.That(menu.ActiveNesButton, Is.Null);
    }

    [Test]
    public void ActiveNesButton_NonBindingScreen_ReturnsNull()
    {
        var menu = CreateMenu();
        menu.Open();
        Assert.That(menu.ActiveNesButton, Is.Null); // Root screen
    }
}
