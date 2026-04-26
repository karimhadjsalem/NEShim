using System.IO;
using System.Text.Json;
using NEShim.Config;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for ConfigLoader — these cross the file system boundary and are
/// intentionally separate from unit tests per the project testing guidelines.
/// </summary>
[TestFixture]
internal class ConfigLoaderTests
{
    private string _configPath = null!;

    [SetUp]
    public void SetUp()
    {
        _configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_configPath)) File.Delete(_configPath);
    }

    // ---- Missing file ----

    [Test]
    public void LoadFrom_WhenFileDoesNotExist_WritesDefaultsToDisk()
    {
        ConfigLoader.LoadFrom(_configPath);
        Assert.That(File.Exists(_configPath), Is.True);
    }

    [Test]
    public void LoadFrom_WhenFileDoesNotExist_ReturnsDefaultRomPath()
    {
        var config = ConfigLoader.LoadFrom(_configPath);
        Assert.That(config.RomPath, Is.EqualTo(new AppConfig().RomPath));
    }

    [Test]
    public void LoadFrom_WhenFileDoesNotExist_ReturnsDefaultWindowMode()
    {
        var config = ConfigLoader.LoadFrom(_configPath);
        Assert.That(config.WindowMode, Is.EqualTo(new AppConfig().WindowMode));
    }

    // ---- Existing file ----

    [Test]
    public void LoadFrom_ExistingFile_DeserializesWindowTitle()
    {
        var original = new AppConfig { WindowTitle = "My Test Game" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.WindowTitle, Is.EqualTo("My Test Game"));
    }

    [Test]
    public void LoadFrom_ExistingFile_DeserializesVolume()
    {
        var original = new AppConfig { Volume = 42 };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.Volume, Is.EqualTo(42));
    }

    [Test]
    public void LoadFrom_ExistingFile_DeserializesRegion()
    {
        var original = new AppConfig { Region = "PAL" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.Region, Is.EqualTo("PAL"));
    }

    // ---- Partial / malformed JSON ----

    [Test]
    public void LoadFrom_PartialJson_MissingFieldsUseDefaults()
    {
        File.WriteAllText(_configPath, """{"windowTitle":"Partial"}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.WindowTitle, Is.EqualTo("Partial"));
        Assert.That(loaded.Volume,      Is.EqualTo(new AppConfig().Volume));
    }

    [Test]
    public void LoadFrom_CorruptJson_ReturnsDefaultRomPath()
    {
        File.WriteAllText(_configPath, "this is not json {{{{");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.RomPath, Is.EqualTo(new AppConfig().RomPath));
    }

    [Test]
    public void LoadFrom_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(_configPath, "");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.RomPath, Is.EqualTo(new AppConfig().RomPath));
    }

    [Test]
    public void LoadFrom_NullLiteralJson_ReturnsDefaults()
    {
        File.WriteAllText(_configPath, "null");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.RomPath, Is.EqualTo(new AppConfig().RomPath));
    }

    // ---- Round-trip ----

    [Test]
    public void SaveTo_ThenLoadFrom_RoundTripsWindowTitle()
    {
        ConfigLoader.SaveTo(new AppConfig { WindowTitle = "Round Trip" }, _configPath);
        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.WindowTitle, Is.EqualTo("Round Trip"));
    }

    [Test]
    public void SaveTo_ThenLoadFrom_RoundTripsMultipleFields()
    {
        var original = new AppConfig
        {
            Volume        = 55,
            EnableLogging = true,
            ShowFps       = true,
            Region        = "NTSC",
        };
        ConfigLoader.SaveTo(original, _configPath);
        var loaded = ConfigLoader.LoadFrom(_configPath);

        Assert.That(loaded.Volume,        Is.EqualTo(55));
        Assert.That(loaded.EnableLogging, Is.True);
        Assert.That(loaded.ShowFps,       Is.True);
        Assert.That(loaded.Region,        Is.EqualTo("NTSC"));
    }

    [Test]
    public void SaveTo_WritesValidJson()
    {
        ConfigLoader.SaveTo(new AppConfig { Volume = 75 }, _configPath);

        string json = File.ReadAllText(_configPath);
        Assert.That(() => JsonDocument.Parse(json), Throws.Nothing);
    }

    [Test]
    public void SaveTo_WritesIndentedJson()
    {
        ConfigLoader.SaveTo(new AppConfig(), _configPath);

        string json = File.ReadAllText(_configPath);
        Assert.That(json, Does.Contain("\n"));
    }

    [Test]
    public void LoadFrom_CalledTwice_ReturnsSameValues()
    {
        ConfigLoader.SaveTo(new AppConfig { Volume = 30 }, _configPath);

        var first  = ConfigLoader.LoadFrom(_configPath);
        var second = ConfigLoader.LoadFrom(_configPath);

        Assert.That(second.Volume, Is.EqualTo(first.Volume));
    }
}
