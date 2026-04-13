using System.IO;
using System.Windows.Forms;
using BizHawk.Emulation.Common;
using Moq;
using NEShim.Config;
using NEShim.Saves;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class InGameMenuTests
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

    private InGameMenu CreateMenu(
        Action?       onExitToDesktop    = null,
        Action?       onResetGame        = null,
        Action?       onReturnToMainMenu = null,
        Action?       onConfigSaved      = null,
        Action<int>?  onVolumeChanged    = null,
        Action<bool>? onScrubberToggled  = null)
    {
        return new InGameMenu(
            _saveStates,
            _config,
            onExitToDesktop    ?? (() => { }),
            onResetGame        ?? (() => { }),
            onReturnToMainMenu ?? (() => { }),
            _ => { },
            onConfigSaved      ?? (() => { }),
            onVolumeChanged    ?? (_ => { }),
            onScrubberToggled  ?? (_ => { }));
    }

    private static int[] EmptyFrame() => new int[256 * 240];

    // Helper: create an empty slot-state file so SlotExists returns true
    private void CreateSlotFile(int slot) =>
        File.WriteAllBytes(Path.Combine(_tempDir, $"slot{slot}.state"), Array.Empty<byte>());

    // ---- Open / Close ----

    [Test]
    public void Open_SetsIsOpenTrue_AndCurrentScreenIsRoot()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        Assert.That(menu.IsOpen,   Is.True);
        Assert.That(menu.Current,  Is.EqualTo(InGameMenu.Screen.Root));
    }

    [Test]
    public void Close_WhenOpen_SetsIsOpenFalse()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        menu.Close();
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void Open_WhenAlreadyOpen_DoesNotResetSelection()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        menu.HandleKey(Keys.Down); // move cursor to index 1
        menu.Open(EmptyFrame());   // second open should be ignored
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
        menu.Open(EmptyFrame());
        menu.HandleKey(Keys.Escape);
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HandleKey_Escape_WhenSubScreen_ReturnsToRoot_WithoutClosing()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
        menu.HandleKey(Keys.Down);
        Assert.That(menu.SelectedItem, Is.EqualTo(1));
    }

    [Test]
    public void HandleKey_Up_AtFirst_WrapsToLast()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        // Root has 8 items (indices 0–7). Up from 0 wraps to 7.
        menu.HandleKey(Keys.Up);
        Assert.That(menu.SelectedItem, Is.EqualTo(7));
    }

    [Test]
    public void HandleKey_Return_OnResume_ClosesMenu()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        // SelectedItem = 0 (Resume)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.IsOpen, Is.False);
    }

    // ---- Confirm screens ----

    [Test]
    public void HandleKey_Return_OnReturnToMainMenu_NavigatesToConfirmMainMenu_WithDefaultNo()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
        // Active slot is 0 by default; no save file exists
        Assert.That(menu.IsItemEnabled(4), Is.False);
    }

    [Test]
    public void IsItemEnabled_LoadGame_ReturnsTrue_WhenActiveSlotHasSave()
    {
        CreateSlotFile(0);
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        Assert.That(menu.IsItemEnabled(4), Is.True);
    }

    [Test]
    public void MoveCursor_SkipsDisabledLoadGame_LandsOnSettings()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
        Assert.That(fired, Is.True);
    }

    [Test]
    public void Closed_EventFires_WhenMenuClosed()
    {
        var menu    = CreateMenu();
        bool fired  = false;
        menu.Closed += () => fired = true;
        menu.Open(EmptyFrame());
        menu.Close();
        Assert.That(fired, Is.True);
    }

    // ---- Settings screen ----

    [Test]
    public void Settings_FpsToggle_TogglesShowFps()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        // Navigate to Settings (index 5 with no save — skips disabled Load Game)
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));

        // Navigate: Down to Video (index 1) → enter Video → Down to FPS (index 1 in Video) → toggle
        menu.HandleKey(Keys.Down);   // select Video (index 1)
        menu.HandleKey(Keys.Return); // enter Video screen
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));

        menu.HandleKey(Keys.Down);   // select FPS (index 1 in Video)
        bool before = _config.Developer.ShowFps;
        menu.HandleKey(Keys.Return);
        Assert.That(_config.Developer.ShowFps, Is.EqualTo(!before));
    }

    [Test]
    public void Settings_FpsToggle_InvokesConfigSavedCallback()
    {
        bool saved = false;
        var menu   = CreateMenu(onConfigSaved: () => saved = true);
        menu.Open(EmptyFrame());
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        menu.HandleKey(Keys.Down);   // select Video (index 1)
        menu.HandleKey(Keys.Return); // enter Video screen
        menu.HandleKey(Keys.Down);   // select FPS (index 1 in Video)
        menu.HandleKey(Keys.Return); // toggle FPS
        Assert.That(saved, Is.True);
    }

    // ---- Save Slot Select screen ----

    [Test]
    public void SaveSlotSelect_Activate_UpdatesActiveSlot()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
        // Settings (index 5, skipping disabled Load Game at 4): 4 Downs then Return
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings
        menu.HandleKey(Keys.Return); // select Key Bindings (index 0)
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.KeyBindings));

        // Select first binding (Up → P1 Up)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.RebindingAction, Is.EqualTo("P1 Up"));
    }

    [Test]
    public void KeyBindings_WhileRebinding_PressKey_UpdatesConfigAndClearsRebind()
    {
        bool saved = false;
        var menu   = CreateMenu(onConfigSaved: () => saved = true);
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(8));
    }

    [Test]
    public void GetCurrentItems_ConfirmMainMenu_ReturnsTwoItems()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        for (int i = 0; i < 5; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter ConfirmMainMenu
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(2));
        Assert.That(menu.GetCurrentItems()[0], Does.StartWith("Yes"));
        Assert.That(menu.GetCurrentItems()[1], Does.StartWith("No"));
    }

    [Test]
    public void GetCurrentItems_SaveSlotSelect_ReturnsEightSlotLabels()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter SaveSlotSelect

        string[] items = menu.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(8)); // 8 slots
        Assert.That(items[0], Does.Contain("1")); // "Slot 1..."
    }

    // ---- Settings: Window Mode toggle (single item) ----

    [Test]
    public void Settings_WindowMode_IsSingleItem_ShowingCurrentMode()
    {
        _config.WindowMode = "Fullscreen";
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // enter Settings

        string[] items = menu.GetCurrentItems();
        Assert.That(items.Length, Is.EqualTo(3)); // Key Bindings, Video, Sound
        Assert.That(items[1], Is.EqualTo("Video"));

        // Window Mode lives in the Video sub-screen
        menu.HandleKey(Keys.Down);   // select Video
        menu.HandleKey(Keys.Return); // enter Video
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Video));
        string[] videoItems = menu.GetCurrentItems();
        Assert.That(videoItems[0], Does.Contain("Fullscreen"));
    }

    [Test]
    public void Settings_WindowMode_Activate_TogglesMode()
    {
        bool toggledTo = false;
        var menu = CreateMenu(onConfigSaved: () => { }); // need window mode toggle
        // Wire a custom toggle capture
        bool receivedFullscreen = false;
        var menuWithToggle = new InGameMenu(
            _saveStates, _config,
            () => { }, () => { }, () => { },
            fs => receivedFullscreen = fs,
            () => { }, _ => { }, _ => { });

        menuWithToggle.Open(new int[256 * 240]);
        _config.WindowMode = "Fullscreen";
        for (int i = 0; i < 4; i++) menuWithToggle.HandleKey(Keys.Down);
        menuWithToggle.HandleKey(Keys.Return); // Settings
        menuWithToggle.HandleKey(Keys.Down);   // select Video (index 1)
        menuWithToggle.HandleKey(Keys.Return); // enter Video
        // Window Mode is index 0 in Video — already selected
        menuWithToggle.HandleKey(Keys.Return); // activate — should toggle to Windowed

        Assert.That(receivedFullscreen, Is.False); // was Fullscreen, toggled to Windowed
    }

    // ---- Sound screen ----

    // Helper: navigate Open → Settings → Sound
    private void OpenSoundScreen(InGameMenu menu)
    {
        menu.Open(new int[256 * 240]);
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // to Settings
        menu.HandleKey(Keys.Return); // enter Settings
        for (int i = 0; i < 2; i++) menu.HandleKey(Keys.Down); // to Sound (index 2)
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
    public void Sound_ScrubberToggle_UpdatesConfig()
    {
        _config.SoundScrubberEnabled = false;
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Down); // select Scrubber (index 1)
        menu.HandleKey(Keys.Return);
        Assert.That(_config.SoundScrubberEnabled, Is.True);
    }

    [Test]
    public void Sound_ScrubberToggle_InvokesCallback()
    {
        bool received = false;
        var menu = CreateMenu(onScrubberToggled: on => received = on);
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Down); // select Scrubber
        menu.HandleKey(Keys.Return);
        Assert.That(received, Is.True);
    }

    [Test]
    public void Sound_Back_ReturnsToSettings()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down); // select Back (index 2)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    [Test]
    public void Sound_Escape_ReturnsToRoot()
    {
        var menu = CreateMenu();
        OpenSoundScreen(menu);
        menu.HandleKey(Keys.Escape);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Root));
    }

    // ---- Video screen ----

    private static void OpenVideoScreen(InGameMenu menu)
    {
        menu.Open(new int[256 * 240]);
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down); // to Settings (index 5, skipping disabled)
        menu.HandleKey(Keys.Return); // enter Settings
        menu.HandleKey(Keys.Down);   // select Video (index 1)
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
    public void Video_GetCurrentItems_ReturnsThreeItems()
    {
        var menu = CreateMenu();
        OpenVideoScreen(menu);
        Assert.That(menu.GetCurrentItems().Length, Is.EqualTo(3));
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
        menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Down);   // ← Back (index 2)
        menu.HandleKey(Keys.Return);
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.Settings));
    }

    // ---- Rollover ----

    [Test]
    public void HandleKey_Down_AtLast_WrapsToFirst()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
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
        menu.Open(EmptyFrame());
        for (int i = 0; i < 4; i++) menu.HandleKey(Keys.Down);
        menu.HandleKey(Keys.Return); // Settings
        menu.HandleKey(Keys.Return); // Key Bindings (index 0)
        Assert.That(menu.Current, Is.EqualTo(InGameMenu.Screen.KeyBindings));

        menu.HandleKey(Keys.Down);   // P1 Down (index 1)
        menu.HandleKey(Keys.Return); // start rebind
        menu.HandleKey(Keys.W);      // bind "W" — currently used by P1 Up

        Assert.That(_config.InputMappings["P1 Down"].Key, Is.EqualTo("W"));
        Assert.That(_config.InputMappings["P1 Up"].Key,   Is.Null); // cleared
    }
}
