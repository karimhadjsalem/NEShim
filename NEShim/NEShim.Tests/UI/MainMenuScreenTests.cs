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
    private MainMenuScreen CreateScreen() =>
        new(_saveStates, _config, null, _ => { }, () => { });

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
}
