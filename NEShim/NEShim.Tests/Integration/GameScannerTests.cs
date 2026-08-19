using NEShim.Achievements;
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

    // ---- trustedDlcAppIds: anti-tamper cross-check against the shell manifest ----
    // A copied/leaked DLC folder's own config.json is player-editable — without this check,
    // editing its steamDlcAppId down to 0 would bypass the Steam ownership gate entirely (0
    // always means "show unconditionally"). games/multigame.json's trusted map is expected to
    // ship as part of the base app's own depot, so GameScanner treats it as authoritative.

    [Test]
    public void Scan_NoTrustedMap_TrustsGamesOwnDeclaredSteamDlcAppId()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 999 }, createRom: true);

        var result = GameScanner.Scan(_gamesRoot); // trustedDlcAppIds omitted entirely

        Assert.That(result[0].IsValid, Is.True);
        Assert.That(result[0].SteamDlcAppId, Is.EqualTo(999u));
    }

    [Test]
    public void Scan_GameIdAbsentFromTrustedMap_TrustsGamesOwnDeclaredSteamDlcAppId()
    {
        WriteGame("bundled-game", new AppConfig { WindowTitle = "Bundled", SteamDlcAppId = 0 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["some-other-dlc-game"] = 555 };

        var result = GameScanner.Scan(_gamesRoot, trustedMap);

        Assert.That(result[0].IsValid, Is.True);
    }

    [Test]
    public void Scan_ClaimedAppIdMatchesTrustedMap_IsValid()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 12345 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 12345 };

        var result = GameScanner.Scan(_gamesRoot, trustedMap);

        Assert.That(result[0].IsValid, Is.True);
    }

    [Test]
    public void Scan_ClaimedAppIdDowngradedToZero_TrustedMapExpectsNonZero_IsInvalid()
    {
        // The exact bypass this check exists to close: a copied DLC folder's config.json edited
        // to claim SteamDlcAppId 0 so it shows unconditionally without owning the real DLC.
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 0 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 12345 };

        var result = GameScanner.Scan(_gamesRoot, trustedMap);

        Assert.That(result[0].IsValid, Is.False);
    }

    [Test]
    public void Scan_ClaimedAppIdMismatchesTrustedMap_IsInvalid()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 111 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 222 };

        var result = GameScanner.Scan(_gamesRoot, trustedMap);

        Assert.That(result[0].IsValid, Is.False);
    }

    [Test]
    public void Scan_MismatchedAppId_WritesReasonToLog_WithoutLoggerEnabled()
    {
        string logPath = Path.Combine(Path.GetTempPath(), $"neshim_test_{Guid.NewGuid()}.log");
        Logger.Reset(logPath);
        try
        {
            WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 0 }, createRom: true);
            var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 999 };

            GameScanner.Scan(_gamesRoot, trustedMap);

            Assert.That(File.ReadAllText(logPath), Does.Contain("unsigned shell manifest expects"));
        }
        finally
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }

    [Test]
    public void Scan_MismatchedAppId_MixedWithValidGames_OnlyMismatchedEntryInvalid()
    {
        WriteGame("valid-game", new AppConfig { WindowTitle = "Valid", SteamDlcAppId = 0 }, createRom: true);
        WriteGame("tampered-dlc", new AppConfig { WindowTitle = "Tampered", SteamDlcAppId = 0 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["tampered-dlc"] = 777 };

        var result = GameScanner.Scan(_gamesRoot, trustedMap);

        Assert.That(result.Single(g => g.GameId == "valid-game").IsValid, Is.True);
        Assert.That(result.Single(g => g.GameId == "tampered-dlc").IsValid, Is.False);
    }

    // ---- Signed mode: fail-closed verification ----
    // A public key is deliberately never sourced from config (see GameScanner's/AppConfig's doc
    // comments) — these tests pass a test keypair explicitly, exactly how GameScanner.Scan's
    // publicKey parameter exists solely for. Production always resolves it from
    // DlcMapSigner.EmbeddedPublicKeyBase64 (null by default, so signing never activates unless a
    // publisher deliberately compiles a key in).

    private const string TestPrivateKeyBase64 =
        "MHcCAQEEIJX+aCzo2G6R5dUkmZWSRbUDpJMqj57dNvMZBNRhdjoqoAoGCCqGSM49AwEHoUQDQgAE" +
        "aAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3S" +
        "LZyvfmHJhw==";
    private const string TestPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG" +
        "1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3SLZyvfmHJhw==";

    [Test]
    public void Scan_SigningEnabled_ValidSignature_MatchingGame_IsValid()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 12345 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 12345 };
        string sig = DlcMapSigner.ComputeSig(trustedMap, TestPrivateKeyBase64);

        var result = GameScanner.Scan(_gamesRoot, trustedMap, sig, TestPublicKeyBase64);

        Assert.That(result[0].IsValid, Is.True);
    }

    [Test]
    public void Scan_SigningEnabled_ValidSignature_BundledGameListedWithZero_IsValid()
    {
        // Once signing is opted into, the map is the sole/complete source of truth — a bundled
        // (free) game must be explicitly listed with value 0, not merely absent.
        WriteGame("bundled", new AppConfig { WindowTitle = "Bundled", SteamDlcAppId = 0 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["bundled"] = 0 };
        string sig = DlcMapSigner.ComputeSig(trustedMap, TestPrivateKeyBase64);

        var result = GameScanner.Scan(_gamesRoot, trustedMap, sig, TestPublicKeyBase64);

        Assert.That(result[0].IsValid, Is.True);
    }

    [Test]
    public void Scan_SigningEnabled_ValidSignature_GameAbsentFromMap_IsInvalid()
    {
        // Unlike unsigned soft-check mode, absence from a SIGNED map is itself a rejection —
        // the map is required to be complete once signing is opted into.
        WriteGame("undeclared", new AppConfig { WindowTitle = "Undeclared", SteamDlcAppId = 0 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["some-other-game"] = 0 };
        string sig = DlcMapSigner.ComputeSig(trustedMap, TestPrivateKeyBase64);

        var result = GameScanner.Scan(_gamesRoot, trustedMap, sig, TestPublicKeyBase64);

        Assert.That(result[0].IsValid, Is.False);
    }

    [Test]
    public void Scan_SigningEnabled_MissingSignature_FailsClosed_AllGamesInvalid()
    {
        // The exact scenario the user flagged: a broken/missing signature must never fall back
        // to trusting the (now-unverified) map or each game's own claim.
        WriteGame("bundled", new AppConfig { WindowTitle = "Bundled", SteamDlcAppId = 0 }, createRom: true);
        WriteGame("dlc-game", new AppConfig { WindowTitle = "DLC", SteamDlcAppId = 999 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["bundled"] = 0, ["dlc-game"] = 999 };

        var result = GameScanner.Scan(_gamesRoot, trustedMap, dlcAppIdsSignature: "", TestPublicKeyBase64);

        Assert.That(result, Has.All.Matches<GameManifest>(g => !g.IsValid));
    }

    [Test]
    public void Scan_SigningEnabled_TamperedMapAfterSigning_FailsClosed_AllGamesInvalid()
    {
        WriteGame("bundled", new AppConfig { WindowTitle = "Bundled", SteamDlcAppId = 0 }, createRom: true);
        WriteGame("dlc-game", new AppConfig { WindowTitle = "DLC", SteamDlcAppId = 999 }, createRom: true);
        var originalMap = new Dictionary<string, uint> { ["bundled"] = 0, ["dlc-game"] = 999 };
        string sig = DlcMapSigner.ComputeSig(originalMap, TestPrivateKeyBase64);

        // Attacker downgrades dlc-game to 0 in both its own config AND the map, but the
        // signature (computed over the ORIGINAL map) no longer matches the tampered one.
        WriteGame("dlc-game", new AppConfig { WindowTitle = "DLC", SteamDlcAppId = 0 }, createRom: true);
        var tamperedMap = new Dictionary<string, uint> { ["bundled"] = 0, ["dlc-game"] = 0 };

        var result = GameScanner.Scan(_gamesRoot, tamperedMap, sig, TestPublicKeyBase64);

        Assert.That(result, Has.All.Matches<GameManifest>(g => !g.IsValid));
    }

    [Test]
    public void Scan_SigningEnabled_WrongPublicKey_FailsClosed_AllGamesInvalid()
    {
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 12345 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 12345 };
        string sig = DlcMapSigner.ComputeSig(trustedMap, TestPrivateKeyBase64);

        const string wrongPublicKey =
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAgCiqeoxm0UuLd9EiZt/ONVA6SybkplDzznY8s1f" +
            "YPCo1hbCAiaFWf3bJl33Sz2qXdYvH+UqQDaSQio5nP3SpQ==";

        var result = GameScanner.Scan(_gamesRoot, trustedMap, sig, wrongPublicKey);

        Assert.That(result, Has.All.Matches<GameManifest>(g => !g.IsValid));
    }

    [Test]
    public void Scan_NoPublicKeySupplied_DefaultsToEmbeddedConstant_WhichIsNull_SigningNeverActivates()
    {
        // Confirms the "code-only key" contract: when dlcAppIdsPublicKey is omitted entirely
        // (as every production call site does), Scan falls back to
        // DlcMapSigner.EmbeddedPublicKeyBase64 — null in this repo's checked-in source, so
        // signing never activates and the unsigned soft-check (or no-check) path applies,
        // regardless of how suspicious the data looks.
        WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 0 }, createRom: true);
        var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 999 }; // would fail signed mode

        // dlcAppIdsPublicKey intentionally omitted.
        var result = GameScanner.Scan(_gamesRoot, trustedMap);

        // Unsigned soft-check semantics apply: mismatch against a present map entry still
        // invalidates, but this proves signing itself never silently engaged without a key.
        Assert.That(DlcMapSigner.EmbeddedPublicKeyBase64, Is.Null);
        Assert.That(result[0].IsValid, Is.False); // rejected by the UNSIGNED soft-check, not fail-closed signing
    }

    [Test]
    public void Scan_SigningEnabled_MissingSignature_WritesLoudReasonToLog_WithoutLoggerEnabled()
    {
        string logPath = Path.Combine(Path.GetTempPath(), $"neshim_test_{Guid.NewGuid()}.log");
        Logger.Reset(logPath);
        try
        {
            WriteGame("kaaz", new AppConfig { WindowTitle = "KAAZ", SteamDlcAppId = 0 }, createRom: true);
            var trustedMap = new Dictionary<string, uint> { ["kaaz"] = 0 };

            GameScanner.Scan(_gamesRoot, trustedMap, dlcAppIdsSignature: "", TestPublicKeyBase64);

            Assert.That(File.ReadAllText(logPath), Does.Contain("failing CLOSED"));
        }
        finally
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }
}
