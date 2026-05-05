using System.IO;
using System.Text;
using NEShim.Localization;

namespace NEShim.Tests.Integration;

[TestFixture]
internal class LocalizationLoaderTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---- Load ----

    [Test]
    public void Load_WhenFileExists_ReturnsPopulatedData()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.json"),
            """{ "fontFamily": "Yu Gothic UI", "back": "戻る" }""",
            Encoding.UTF8);

        var data = LocalizationLoader.Load(_tempDir, "test");

        Assert.That(data.FontFamily, Is.EqualTo("Yu Gothic UI"));
        Assert.That(data.Back,       Is.EqualTo("戻る"));
    }

    [Test]
    public void Load_WhenFileExists_MissingKeysKeepDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "partial.json"),
            """{ "fontFamily": "Malgun Gothic" }""",
            Encoding.UTF8);

        var data = LocalizationLoader.Load(_tempDir, "partial");

        Assert.That(data.FontFamily,       Is.EqualTo("Malgun Gothic"));
        Assert.That(data.InGamePausedTitle, Is.EqualTo("PAUSED")); // untranslated → default
    }

    [Test]
    public void Load_WhenLanguageFileNotFound_FallsBackToEnglish()
    {
        File.WriteAllText(Path.Combine(_tempDir, "english.json"),
            """{ "mainMenuTitle": "MAIN MENU FALLBACK" }""",
            Encoding.UTF8);

        var data = LocalizationLoader.Load(_tempDir, "nonexistent");

        Assert.That(data.MainMenuTitle, Is.EqualTo("MAIN MENU FALLBACK"));
    }

    [Test]
    public void Load_WhenNoFilesExist_ReturnsDefaultInstance()
    {
        var data = LocalizationLoader.Load(_tempDir, "nonexistent");

        Assert.That(data.FontFamily,   Is.EqualTo("Segoe UI"));
        Assert.That(data.MainMenuTitle, Is.EqualTo("MAIN MENU"));
    }

    [Test]
    public void Load_IsCaseInsensitiveForJsonKeys()
    {
        File.WriteAllText(Path.Combine(_tempDir, "ci.json"),
            """{ "FONTFAMILY": "Test Font", "BACK": "Back Test" }""",
            Encoding.UTF8);

        var data = LocalizationLoader.Load(_tempDir, "ci");

        Assert.That(data.FontFamily, Is.EqualTo("Test Font"));
        Assert.That(data.Back,       Is.EqualTo("Back Test"));
    }

    // ---- LoadFrom ----

    [Test]
    public void LoadFrom_PopulatesAllSpecifiedKeys()
    {
        string path = Path.Combine(_tempDir, "full.json");
        File.WriteAllText(path,
            """
            {
              "fontFamily": "Segoe UI",
              "back": "← Back",
              "settingsTitle": "SETTINGS",
              "inGamePausedTitle": "PAUSED",
              "slotLabel": "Slot {0}",
              "slotNoSave": "  (no save)",
              "slotActive": "  ◀ active",
              "soundVolume": "◀  Volume: {0}  ▶"
            }
            """,
            Encoding.UTF8);

        var data = LocalizationLoader.LoadFrom(path);

        Assert.That(data.FontFamily,      Is.EqualTo("Segoe UI"));
        Assert.That(data.Back,            Is.EqualTo("← Back"));
        Assert.That(data.SettingsTitle,   Is.EqualTo("SETTINGS"));
        Assert.That(data.SlotLabel,       Is.EqualTo("Slot {0}"));
        Assert.That(data.SlotNoSave,      Is.EqualTo("  (no save)"));
        Assert.That(data.SlotActive,      Is.EqualTo("  ◀ active"));
        Assert.That(data.SoundVolume,     Is.EqualTo("◀  Volume: {0}  ▶"));
    }

    [Test]
    public void LoadFrom_EmptyJson_ReturnsDefaultInstance()
    {
        string path = Path.Combine(_tempDir, "empty.json");
        File.WriteAllText(path, "{}", Encoding.UTF8);

        var data = LocalizationLoader.LoadFrom(path);

        Assert.That(data.FontFamily, Is.EqualTo("Segoe UI"));
        Assert.That(data.Back,       Is.EqualTo("← Back"));
    }
}
