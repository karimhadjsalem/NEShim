using NEShim.Config;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for GameScanner — these cross the file system boundary and are
/// intentionally separate from unit tests per the project testing guidelines.
/// </summary>
[TestFixture]
internal class GameScannerTests
{
    private string _gamesRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _gamesRoot = Path.Combine(Path.GetTempPath(), $"games_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_gamesRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_gamesRoot)) Directory.Delete(_gamesRoot, recursive: true);
    }

    // createRom controls whether the config's RomPath is backed by a real file — needed for a
    // folder to scan as IsValid: true under the new ROM-existence check.
    private void WriteGame(string gameId, AppConfig config, bool createRom = false)
    {
        string dir = Path.Combine(_gamesRoot, gameId);
        Directory.CreateDirectory(dir);
        ConfigLoader.SaveTo(config, Path.Combine(dir, "config.json"));
        if (createRom)
        {
            string romPath = Path.IsPathRooted(config.RomPath) ? config.RomPath : Path.Combine(dir, config.RomPath);
            File.WriteAllBytes(romPath, Array.Empty<byte>());
        }
    }

    [Test]
    public void Scan_WhenGamesRootDoesNotExist_ReturnsEmpty()
    {
        var result = GameScanner.Scan(Path.Combine(_gamesRoot, "does-not-exist"));
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Scan_EmptyGamesRoot_ReturnsEmpty()
    {
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Scan_FolderWithoutConfigJson_IncludedAsInvalidWithFolderNameTitle()
    {
        Directory.CreateDirectory(Path.Combine(_gamesRoot, "not-a-game"));
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].IsValid, Is.False);
        Assert.That(result[0].DisplayTitle, Is.EqualTo("not-a-game"));
    }

    [Test]
    public void Scan_ValidGameFolder_ReturnsOneEntry()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ" });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public void Scan_ValidGameFolder_GameIdMatchesFolderName()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ" });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result[0].GameId, Is.EqualTo("kaaz"));
    }

    [Test]
    public void Scan_GameDisplayTitleSet_UsesGameDisplayTitleOverWindowTitle()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", GameDisplayTitle = "Kaaz Deluxe" });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result[0].DisplayTitle, Is.EqualTo("Kaaz Deluxe"));
    }

    [Test]
    public void Scan_GameDisplayTitleEmpty_FallsBackToWindowTitle()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ" });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result[0].DisplayTitle, Is.EqualTo("KAAZ"));
    }

    [Test]
    public void Scan_CapturesSteamDlcAppId()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 123456 });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result[0].SteamDlcAppId, Is.EqualTo(123456u));
    }

    [Test]
    public void Scan_MultipleGames_ReturnsAll()
    {
        WriteGame("game-a", new AppConfig { WindowTitle = "Alpha" });
        WriteGame("game-b", new AppConfig { WindowTitle = "Beta" });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void Scan_MultipleGames_SortedByDisplayTitleCaseInsensitive()
    {
        WriteGame("game-b", new AppConfig { WindowTitle = "beta" });
        WriteGame("game-a", new AppConfig { WindowTitle = "Alpha" });
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result.Select(g => g.DisplayTitle), Is.EqualTo(new[] { "Alpha", "beta" }));
    }

    [Test]
    public void Scan_MixOfConfiguredAndUnconfiguredFolders_BothReturned_WithCorrectValidity()
    {
        WriteGame("valid-game", new AppConfig { WindowTitle = "Valid" }, createRom: true);
        Directory.CreateDirectory(Path.Combine(_gamesRoot, "invalid-empty"));

        var result = GameScanner.Scan(_gamesRoot);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.Single(g => g.GameId == "valid-game").IsValid, Is.True);
        Assert.That(result.Single(g => g.GameId == "invalid-empty").IsValid, Is.False);
    }

    [Test]
    public void Scan_FolderWithMalformedConfigJson_IncludedAsInvalidWithFolderNameTitle()
    {
        string dir = Path.Combine(_gamesRoot, "corrupt-game");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "this is not json {{{{");

        var result = GameScanner.Scan(_gamesRoot);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].IsValid, Is.False);
        Assert.That(result[0].DisplayTitle, Is.EqualTo("corrupt-game"));
    }

    [Test]
    public void Scan_MixOfValidAndMalformedFolders_BothReturned_WithCorrectValidity()
    {
        WriteGame("valid-game", new AppConfig { WindowTitle = "Valid" }, createRom: true);
        string corruptDir = Path.Combine(_gamesRoot, "corrupt-game");
        Directory.CreateDirectory(corruptDir);
        File.WriteAllText(Path.Combine(corruptDir, "config.json"), "this is not json {{{{");

        var result = GameScanner.Scan(_gamesRoot);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.Single(g => g.GameId == "valid-game").IsValid, Is.True);
        Assert.That(result.Single(g => g.GameId == "corrupt-game").IsValid, Is.False);
    }

    [Test]
    public void Scan_ValidConfigButRomPathMissing_IncludedAsInvalidWithRealTitle()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", RomPath = "missing.nes" }, createRom: false);

        var result = GameScanner.Scan(_gamesRoot);

        Assert.That(result[0].IsValid, Is.False);
        Assert.That(result[0].DisplayTitle, Is.EqualTo("KAAZ"));
    }

    [Test]
    public void Scan_FullyValidConfig_IsValidTrue()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", RomPath = "game.nes" }, createRom: true);

        var result = GameScanner.Scan(_gamesRoot);

        Assert.That(result[0].IsValid, Is.True);
    }

    [Test]
    public void Scan_ValidConfig_PassesThroughThumbnailPathAndDescription()
    {
        WriteGame("kaaz", new AppConfig
        {
            WindowTitle = "KAAZ",
            ThumbnailPath = "art/box.png",
            GameDescription = "A great game.",
        }, createRom: true);

        var result = GameScanner.Scan(_gamesRoot);

        Assert.That(result[0].ThumbnailPath, Is.EqualTo("art/box.png"));
        Assert.That(result[0].Description, Is.EqualTo("A great game."));
    }

    // ---- Invalid-entry diagnostics always reach the log, even with EnableLogging off ----
    // The carousel only ever shows a generic "Game Error" note to players (see
    // LocalizationData.CarouselUnavailable) — the specific reason must still be recoverable from
    // neshim.log, so GameScanner uses Logger.LogAlways rather than the EnableLogging-gated Log.

    [Test]
    public void Scan_MissingConfigJson_WritesReasonToLog_WithoutLoggerEnabled()
    {
        string logPath = Path.Combine(Path.GetTempPath(), $"neshim_test_{Guid.NewGuid()}.log");
        Logger.Reset(logPath);
        try
        {
            Directory.CreateDirectory(Path.Combine(_gamesRoot, "not-a-game"));

            GameScanner.Scan(_gamesRoot);

            Assert.That(File.ReadAllText(logPath), Does.Contain("no config.json"));
        }
        finally
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }

    [Test]
    public void Scan_CorruptConfigJson_WritesReasonToLog_WithoutLoggerEnabled()
    {
        string logPath = Path.Combine(Path.GetTempPath(), $"neshim_test_{Guid.NewGuid()}.log");
        Logger.Reset(logPath);
        try
        {
            string dir = Path.Combine(_gamesRoot, "corrupt-game");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "config.json"), "this is not json {{{{");

            GameScanner.Scan(_gamesRoot);

            Assert.That(File.ReadAllText(logPath), Does.Contain("failed to parse"));
        }
        finally
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }

    [Test]
    public void Scan_MissingRomFile_WritesReasonToLog_WithoutLoggerEnabled()
    {
        string logPath = Path.Combine(Path.GetTempPath(), $"neshim_test_{Guid.NewGuid()}.log");
        Logger.Reset(logPath);
        try
        {
            WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", RomPath = "missing.nes" }, createRom: false);

            GameScanner.Scan(_gamesRoot);

            Assert.That(File.ReadAllText(logPath), Does.Contain("ROM file not found"));
        }
        finally
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }
}
