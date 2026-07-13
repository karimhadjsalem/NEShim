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

    private void WriteGame(string gameId, AppConfig config)
    {
        string dir = Path.Combine(_gamesRoot, gameId);
        Directory.CreateDirectory(dir);
        ConfigLoader.SaveTo(config, Path.Combine(dir, "config.json"));
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
    public void Scan_SubfolderWithoutConfigJson_IsSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_gamesRoot, "not-a-game"));
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result, Is.Empty);
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
    public void Scan_MixOfValidAndInvalidFolders_OnlyReturnsValid()
    {
        WriteGame("valid-game", new AppConfig { WindowTitle = "Valid" });
        Directory.CreateDirectory(Path.Combine(_gamesRoot, "invalid-empty"));
        var result = GameScanner.Scan(_gamesRoot);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].GameId, Is.EqualTo("valid-game"));
    }
}
