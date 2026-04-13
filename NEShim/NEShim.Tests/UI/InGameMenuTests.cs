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
        Action? onExitToDesktop     = null,
        Action? onResetGame         = null,
        Action? onReturnToMainMenu  = null,
        Action? onConfigSaved       = null)
    {
        return new InGameMenu(
            _saveStates,
            _config,
            onExitToDesktop    ?? (() => { }),
            onResetGame        ?? (() => { }),
            onReturnToMainMenu ?? (() => { }),
            _ => { },
            onConfigSaved      ?? (() => { }));
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
    public void HandleKey_Up_CannotMoveAboveZero()
    {
        var menu = CreateMenu();
        menu.Open(EmptyFrame());
        menu.HandleKey(Keys.Up);
        Assert.That(menu.SelectedItem, Is.EqualTo(0));
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

        // Move down to FPS toggle (index 3)
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down);

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
        for (int i = 0; i < 3; i++) menu.HandleKey(Keys.Down);
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
}
