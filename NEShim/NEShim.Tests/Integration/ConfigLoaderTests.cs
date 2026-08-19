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
    public void LoadFrom_WhenFileDoesNotExist_OnNonSteamDeck_AudioFilterIsDefault()
    {
        // Tests run on Windows without the SteamDeck=1 env var, so IsSteamDeck is false.
        // Verifies the non-Steam-Deck first-run path leaves AudioFilter at "Default".
        var config = ConfigLoader.LoadFrom(_configPath);
        Assert.That(config.AudioFilter, Is.EqualTo("Default"));
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

    // ---- Deprecated field migration ----

    [Test]
    public void LoadFrom_SoundScrubberEnabled_PromotesToWarmAudioFilter()
    {
        var original = new AppConfig { SoundScrubberEnabled = true, AudioFilter = "Default" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.AudioFilter, Is.EqualTo("Warm"));
    }

    [Test]
    public void LoadFrom_SoundScrubberEnabled_DoesNotOverrideExplicitAudioFilter()
    {
        var original = new AppConfig { SoundScrubberEnabled = true, AudioFilter = "PseudoStereo" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.AudioFilter, Is.EqualTo("PseudoStereo"));
    }

    [Test]
    public void LoadFrom_SoundScrubberDisabled_LeavesAudioFilterDefault()
    {
        var original = new AppConfig { SoundScrubberEnabled = false, AudioFilter = "Default" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.AudioFilter, Is.EqualTo("Default"));
    }

    [Test]
    public void LoadFrom_GraphicsSmoothingEnabled_PromotesToBilinearVideoFilter()
    {
        var original = new AppConfig { GraphicsSmoothingEnabled = true, VideoFilter = "NearestNeighbour" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.VideoFilter, Is.EqualTo("Bilinear"));
    }

    [Test]
    public void LoadFrom_GraphicsSmoothingEnabled_DoesNotOverrideExplicitVideoFilter()
    {
        var original = new AppConfig { GraphicsSmoothingEnabled = true, VideoFilter = "CrtScanlines" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.VideoFilter, Is.EqualTo("CrtScanlines"));
    }

    [Test]
    public void LoadFrom_NearestNeighbour_MigratesTo_PixelPerfect()
    {
        var original = new AppConfig { GraphicsSmoothingEnabled = false, VideoFilter = "NearestNeighbour" };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.VideoFilter, Is.EqualTo("PixelPerfect"));
    }

    [Test]
    public void LoadFrom_OverscanMode_None_MigratesTo_Normal()
    {
        File.WriteAllText(_configPath, """{"overscanMode":"None"}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.OverscanMode, Is.EqualTo("Normal"));
    }

    [Test]
    public void LoadFrom_OverscanMode_NTSC_MigratesTo_Overscan()
    {
        File.WriteAllText(_configPath, """{"overscanMode":"NTSC"}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.OverscanMode, Is.EqualTo("Overscan"));
    }

    [Test]
    public void LoadFrom_OverscanMode_Auto_MigratesTo_Overscan()
    {
        File.WriteAllText(_configPath, """{"overscanMode":"Auto"}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.OverscanMode, Is.EqualTo("Overscan"));
    }

    [Test]
    public void LoadFrom_OverscanMode_Valid_NotMigrated()
    {
        File.WriteAllText(_configPath, """{"overscanMode":"Underscan"}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.OverscanMode, Is.EqualTo("Underscan"));
    }

    [Test]
    public void LoadFrom_StaleGamepadToggleWindowKey_MigratesToToggleWindowCarousel()
    {
        File.WriteAllText(_configPath,
            """{"gamepadHotkeyMappings":{"OpenMenu":"LeftShoulder","ToggleWindow":"Y"}}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.GamepadHotkeyMappings, Does.Not.ContainKey("ToggleWindow"));
        Assert.That(loaded.GamepadHotkeyMappings["ToggleWindowCarousel"], Is.EqualTo("Y"));
    }

    [Test]
    public void LoadFrom_StaleGamepadToggleWindowKey_DoesNotOverrideExistingToggleWindowCarousel()
    {
        File.WriteAllText(_configPath,
            """{"gamepadHotkeyMappings":{"ToggleWindow":"Y","ToggleWindowCarousel":"X"}}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);
        Assert.That(loaded.GamepadHotkeyMappings, Does.Not.ContainKey("ToggleWindow"));
        Assert.That(loaded.GamepadHotkeyMappings["ToggleWindowCarousel"], Is.EqualTo("X"));
    }

    // ---- Two-file layering ----

    [Test]
    public void Load_WithNoUserJson_BootstrapsUserJsonFromPublisherConfig()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", Volume = 80 };
        ConfigLoader.SaveTo(publisher, _configPath);
        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        ConfigLoader.Load(_configPath, userPath);

        Assert.That(File.Exists(userPath), Is.True);
        File.Delete(userPath);
    }

    [Test]
    public void Load_WithNoUserJson_ReturnsPublisherValues()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", Volume = 80 };
        ConfigLoader.SaveTo(publisher, _configPath);
        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Volume, Is.EqualTo(80));
        File.Delete(userPath);
    }

    [Test]
    public void Load_WithNoUserJson_BootstrappedUserJsonPreservesVolumeAfterPublisherConfigReset()
    {
        // Simulates: user had volume=42 in old config.json; Steam update resets config.json to defaults.
        // First run: no user.json → bootstrap user.json with volume=42.
        // Second run: config.json now has default volume=100; user.json overrides with 42.
        var originalPublisher = new AppConfig { WindowTitle = "TestGame", Volume = 42 };
        ConfigLoader.SaveTo(originalPublisher, _configPath);
        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        ConfigLoader.Load(_configPath, userPath); // bootstraps user.json with Volume=42

        var resetPublisher = new AppConfig { WindowTitle = "TestGame" }; // Volume = 100 (default)
        ConfigLoader.SaveTo(resetPublisher, _configPath);

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Volume, Is.EqualTo(42));
        File.Delete(userPath);
    }

    [Test]
    public void Load_UserJsonOverridesPublisherUserFields()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", Volume = 80 };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var userConfig  = new AppConfig { Volume = 42 };
        ConfigLoader.SaveUserTo(userConfig, userPath);

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Volume, Is.EqualTo(42));
    }

    [Test]
    public void Load_UserJsonDoesNotOverridePublisherOnlyFields()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", RomPath = "special.nes" };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath    = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var userAppConfig  = new AppConfig { Volume = 50 };
        ConfigLoader.SaveUserTo(userAppConfig, userPath);

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.RomPath, Is.EqualTo("special.nes"));
    }

    [Test]
    public void Load_PartialUserJson_OnlyOverridesFieldsPresent()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", Volume = 80, ShowFps = false };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(userPath, """{"volume":55}""");

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Volume,  Is.EqualTo(55));
        Assert.That(loaded.ShowFps, Is.False);
    }

    [Test]
    public void Load_UserJsonSetsShowFps_TrueOverridesPublisherFalse()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", ShowFps = false };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(userPath, """{"showFps":true}""");

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.ShowFps, Is.True);
    }

    [Test]
    public void Load_CorruptUserJson_FallsBackToPublisherValues()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", Volume = 70 };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(userPath, "this is not json {{{{");

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Volume, Is.EqualTo(70));
    }

    [Test]
    public void SaveUserTo_ThenLoad_RoundTripsVolume()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame" };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath   = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var savedConfig   = new AppConfig { Volume = 63 };
        ConfigLoader.SaveUserTo(savedConfig, userPath);

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Volume, Is.EqualTo(63));
    }

    [Test]
    public void SaveUserTo_DoesNotWritePublisherOnlyFields()
    {
        var config  = new AppConfig { WindowTitle = "TestGame", RomPath = "game.nes", Volume = 75 };
        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        ConfigLoader.SaveUserTo(config, userPath);

        string json = File.ReadAllText(userPath);

        Assert.That(json, Does.Not.Contain("RomPath"));
        Assert.That(json, Does.Not.Contain("WindowTitle"));
        Assert.That(json, Does.Contain("Volume"));
    }

    [Test]
    public void SaveUserTo_CreatesDirectoryIfMissing()
    {
        var config    = new AppConfig();
        string subDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nested");
        string userPath = Path.Combine(subDir, "user.json");

        ConfigLoader.SaveUserTo(config, userPath);

        Assert.That(File.Exists(userPath), Is.True);
        Directory.Delete(Path.GetDirectoryName(userPath)!, recursive: true);
    }

    [Test]
    public void SaveUserTo_ThenLoad_RoundTripsWindowMode()
    {
        // Exercises the exact mechanism NEShimApp uses to give the multi-game carousel its own
        // discrete, persisted window-mode preference (MultiGameMode.ShellUserConfigPath):
        // SaveUserTo on toggle, Load(publisherPath, userPath) on carousel (re-)entry.
        var publisher = new AppConfig { WindowTitle = "Shell", WindowMode = "Windowed" };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            ConfigLoader.SaveUserTo(new AppConfig { WindowMode = "Fullscreen" }, userPath);

            var loaded = ConfigLoader.Load(_configPath, userPath);

            Assert.That(loaded.WindowMode, Is.EqualTo("Fullscreen"));
        }
        finally
        {
            if (File.Exists(userPath)) File.Delete(userPath);
        }
    }

    [Test]
    public void Load_UserLanguage_OverridesPublisherLanguage()
    {
        var publisher = new AppConfig { WindowTitle = "TestGame", Language = "Auto" };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(userPath, """{"language":"french"}""");

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.Language, Is.EqualTo("french"));
    }

    // ---- PlayerCount clamping ----

    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(5, 4)]
    [TestCase(100, 4)]
    public void LoadFrom_PlayerCountOutOfRange_ClampsToValidRange(int rawValue, int expected)
    {
        File.WriteAllText(_configPath, $$"""{"playerCount":{{rawValue}}}""");

        var loaded = ConfigLoader.LoadFrom(_configPath);

        Assert.That(loaded.PlayerCount, Is.EqualTo(expected));
    }

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void LoadFrom_PlayerCountInRange_IsUnchanged(int value)
    {
        var original = new AppConfig { PlayerCount = value };
        ConfigLoader.SaveTo(original, _configPath);

        var loaded = ConfigLoader.LoadFrom(_configPath);

        Assert.That(loaded.PlayerCount, Is.EqualTo(value));
    }

    [Test]
    public void Load_HandEditedUserJsonPlayerCount_CannotOverridePublisherValue()
    {
        // PlayerCount is publisher-only — UserConfig has no PlayerCount property at all, so even
        // a hand-edited user.json containing "playerCount" must have zero effect. Also confirms
        // the clamp re-applies harmlessly on the user-overlay path (a no-op safety net, not
        // redundant work — see MigrateDeprecatedFields's doc comment).
        var publisher = new AppConfig { WindowTitle = "TestGame", PlayerCount = 3 };
        ConfigLoader.SaveTo(publisher, _configPath);

        string userPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(userPath, """{"playerCount":99}""");

        var loaded = ConfigLoader.Load(_configPath, userPath);

        Assert.That(loaded.PlayerCount, Is.EqualTo(3));
        File.Delete(userPath);
    }

    // ---- TryParseFrom ----

    [Test]
    public void TryParseFrom_ValidJson_ReturnsTrue()
    {
        ConfigLoader.SaveTo(new AppConfig { WindowTitle = "TestGame" }, _configPath);

        bool success = ConfigLoader.TryParseFrom(_configPath, out var config);

        Assert.That(success, Is.True);
        Assert.That(config.WindowTitle, Is.EqualTo("TestGame"));
    }

    [Test]
    public void TryParseFrom_MalformedJson_ReturnsFalse()
    {
        File.WriteAllText(_configPath, "this is not json {{{{");

        bool success = ConfigLoader.TryParseFrom(_configPath, out var config);

        Assert.That(success, Is.False);
        Assert.That(config, Is.Not.Null);
    }

    // ---- Multi-game (GameContext) isolation ----

    [Test]
    public void Load_WithGameContext_LoadsPublisherConfigFromGameRoot()
    {
        string gameRoot = Path.Combine(Path.GetTempPath(), $"game_{Guid.NewGuid():N}");
        Directory.CreateDirectory(gameRoot);
        ConfigLoader.SaveTo(new AppConfig { RomPath = "special-game.nes", WindowTitle = "GameRootTest" },
            Path.Combine(gameRoot, "config.json"));
        var ctx = new GameContext(gameRoot, "game-root-test");
        string userDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NEShim", "Games", "game-root-test");

        try
        {
            var loaded = ConfigLoader.Load(ctx);
            Assert.That(loaded.RomPath, Is.EqualTo("special-game.nes"));
        }
        finally
        {
            Directory.Delete(gameRoot, recursive: true);
            if (Directory.Exists(userDir)) Directory.Delete(userDir, recursive: true);
        }
    }

    [Test]
    public void Save_WithGameContext_WritesToGamesSubfolder_NotWindowTitleFolder()
    {
        string gameId = $"test-{Guid.NewGuid():N}";
        var ctx = new GameContext(Path.GetTempPath(), gameId);
        var config = new AppConfig { WindowTitle = "TestGame", Volume = 33 };

        string expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NEShim", "Games", gameId, "user.json");
        string singleGamePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), config.WindowTitle, "user.json");

        try
        {
            ConfigLoader.Save(config, ctx);
            Assert.That(File.Exists(expectedPath), Is.True);
            Assert.That(File.Exists(singleGamePath), Is.False);
        }
        finally
        {
            string gameDir = Path.GetDirectoryName(expectedPath)!;
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, recursive: true);
        }
    }

    [Test]
    public void Save_WithNullGameContext_UsesUnchangedSingleGameScheme()
    {
        // ctx defaults to null — behavior must be identical to calling Save(config) with no ctx.
        var config = new AppConfig { WindowTitle = $"SingleGameTest-{Guid.NewGuid():N}" };
        string expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), config.WindowTitle, "user.json");

        try
        {
            ConfigLoader.Save(config, ctx: null);
            Assert.That(File.Exists(expectedPath), Is.True);
        }
        finally
        {
            string dir = Path.GetDirectoryName(expectedPath)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void Save_ThenLoad_WithGameContext_RoundTripsUserOverlay()
    {
        string gameId = $"roundtrip-{Guid.NewGuid():N}";
        string gameRoot = Path.Combine(Path.GetTempPath(), $"game_{Guid.NewGuid():N}");
        Directory.CreateDirectory(gameRoot);
        ConfigLoader.SaveTo(new AppConfig { WindowTitle = "RoundTripGame" }, Path.Combine(gameRoot, "config.json"));
        var ctx = new GameContext(gameRoot, gameId);

        try
        {
            var toSave = new AppConfig { WindowTitle = "RoundTripGame", Volume = 42 };
            ConfigLoader.Save(toSave, ctx);

            var loaded = ConfigLoader.Load(ctx);

            Assert.That(loaded.Volume, Is.EqualTo(42));
        }
        finally
        {
            Directory.Delete(gameRoot, recursive: true);
            string userDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NEShim", "Games", gameId);
            if (Directory.Exists(userDir)) Directory.Delete(userDir, recursive: true);
        }
    }
}
