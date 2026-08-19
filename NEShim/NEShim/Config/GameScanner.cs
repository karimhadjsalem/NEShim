using NEShim.Achievements;

namespace NEShim.Config;

/// <summary>
/// Discovers installed games under <c>games/</c> for the carousel. Independent of mode
/// *detection* (<see cref="MultiGameMode.IsActive"/>, which checks for <c>games/multigame.json</c>
/// only) — <c>games/</c> can legitimately exist with zero valid subfolders (e.g. DLC still
/// downloading); <see cref="Scan"/> simply returns an empty list in that case.
///
/// Every subfolder produces a <see cref="GameManifest"/> — none are ever skipped. A folder is
/// classified invalid (<see cref="GameManifest.IsValid"/> false) when its <c>config.json</c> is
/// missing or fails to parse, when it parses but its configured <c>RomPath</c> does not resolve
/// to a file that exists on disk, or when its claimed <c>SteamDlcAppId</c> fails the DLC trust
/// check below. A missing thumbnail image is deliberately NOT a validation failure — that's an
/// art-asset gap, not a structural configuration problem. Players only ever see a generic
/// "Game Error" note for an invalid entry (see LocalizationData.CarouselUnavailable) — the
/// specific reason is written to neshim.log unconditionally (Logger.LogAlways) rather than
/// carried on the manifest for display.
///
/// DLC trust check (see <see cref="AppConfig.GameDlcAppIds"/>/<see cref="AppConfig.GameDlcAppIdsSignature"/>
/// and <see cref="DlcMapSigner.EmbeddedPublicKeyBase64"/>): a real 2-way branch in
/// <see cref="IsDlcClaimTrusted"/> on whether a public key is compiled in (<c>signingEnabled</c>),
/// which in turn covers three conceptually-distinct trust levels — the "signing disabled" branch
/// alone spans two of them, distinguished only by whether <c>trustedDlcAppIds</c> happens to be
/// null, not by separate code paths:
///   - No trust data at all (<c>signingEnabled == false</c>, map null): each game's own claimed
///     SteamDlcAppId is trusted outright (today's baseline; correct for a single-deploy build
///     with no DLC-gated games).
///   - Unsigned gameDlcAppIds map (<c>signingEnabled == false</c>, map present): a soft cross-check
///     — a gameId listed in the map must match, but a gameId absent from it is still trusted as
///     declared by its own config.json. Raises the bar slightly but is not tamper-proof
///     (games/multigame.json is just as locally-editable as any per-game config.json).
///   - Signed map (<c>signingEnabled == true</c> — a public key IS compiled into the binary, see
///     DlcMapSigner.EmbeddedPublicKeyBase64, deliberately code-only, never config-driven): fails
///     CLOSED, not open, on any problem. A missing or invalid signature marks every scanned game
///     invalid — never silently falls back to the unsigned behavior, since a corrupted/missing
///     signature must not be indistinguishable from "protection intentionally disabled". Once the
///     signature verifies, the map becomes the sole, complete source of truth: every game must
///     appear in it (bundled games too, with value 0) with a matching SteamDlcAppId, or it's rejected.
/// </summary>
internal static class GameScanner
{
    /// <param name="trustedDlcAppIds">
    /// Optional gameId -> expected SteamDlcAppId map, sourced from the shell manifest
    /// (<see cref="AppConfig.GameDlcAppIds"/>). See the class doc comment for how its effect
    /// depends on whether a public key is compiled in.
    /// </param>
    /// <param name="dlcAppIdsSignature">ECDSA-P256 signature (base64) over <paramref name="trustedDlcAppIds"/> — <see cref="AppConfig.GameDlcAppIdsSignature"/>.</param>
    /// <param name="dlcAppIdsPublicKey">
    /// Verifying public key (base64). Defaults to <see cref="DlcMapSigner.EmbeddedPublicKeyBase64"/>
    /// (the compile-time constant — production callers should omit this parameter entirely so
    /// they always get that value; there is no config.json equivalent by design). Overridable
    /// only so tests can inject a test keypair without a real compiled key.
    /// </param>
    internal static IReadOnlyList<GameManifest> Scan(
        string gamesRoot,
        IReadOnlyDictionary<string, uint>? trustedDlcAppIds = null,
        string dlcAppIdsSignature = "",
        string? dlcAppIdsPublicKey = null)
    {
        if (!Directory.Exists(gamesRoot)) return Array.Empty<GameManifest>();

        dlcAppIdsPublicKey ??= DlcMapSigner.EmbeddedPublicKeyBase64 ?? "";
        bool signingEnabled = !string.IsNullOrEmpty(dlcAppIdsPublicKey);
        bool signatureValid = signingEnabled && trustedDlcAppIds is not null
            && DlcMapSigner.Verify(trustedDlcAppIds, dlcAppIdsSignature, dlcAppIdsPublicKey);

        if (signingEnabled && !signatureValid)
            Logger.LogAlways("[GameScanner] gameDlcAppIdsPublicKey is set but gameDlcAppIdsSignature is missing " +
                              "or does not verify — failing CLOSED: every game in this scan will be listed as " +
                              "invalid until this is fixed. This is intentional: a broken signature must never " +
                              "silently fall back to trusting each game's own unsigned claim.");

        var results = Directory.GetDirectories(gamesRoot)
            .Select(dir => ScanGame(dir, gamesRoot, trustedDlcAppIds, signingEnabled, signatureValid))
            .ToList();

        return results.OrderBy(g => g.DisplayTitle, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static GameManifest ScanGame(string dir, string gamesRoot,
        IReadOnlyDictionary<string, uint>? trustedDlcAppIds, bool signingEnabled, bool signatureValid)
    {
        string gameId = Path.GetFileName(dir);
        string configPath = Path.Combine(dir, "config.json");

        if (!File.Exists(configPath))
        {
            Logger.LogAlways($"[GameScanner] '{dir}' has no config.json — listing as invalid.");
            return new GameManifest(gameId, gameId, SteamDlcAppId: 0, ThumbnailPath: "", IsValid: false);
        }

        if (!ConfigLoader.TryParseFrom(configPath, out var cfg))
        {
            Logger.LogAlways($"[GameScanner] '{dir}' config.json failed to parse — listing as invalid.");
            return new GameManifest(gameId, gameId, SteamDlcAppId: 0, ThumbnailPath: "", IsValid: false);
        }

        string title = string.IsNullOrWhiteSpace(cfg.GameDisplayTitle) ? cfg.WindowTitle : cfg.GameDisplayTitle;

        if (!IsDlcClaimTrusted(gameId, cfg.SteamDlcAppId, trustedDlcAppIds, signingEnabled, signatureValid))
        {
            LogDlcTrustFailure(dir, gameId, cfg.SteamDlcAppId, trustedDlcAppIds, signingEnabled, signatureValid);
            return new GameManifest(gameId, title, cfg.SteamDlcAppId, cfg.ThumbnailPath,
                Description: cfg.GameDescription, IsValid: false);
        }

        var ctx = GameContext.ForGame(gamesRoot, gameId);
        string romAbsolute = GameContext.ResolvePath(cfg.RomPath, ctx);
        bool romExists = File.Exists(romAbsolute);
        if (!romExists)
            Logger.LogAlways($"[GameScanner] '{dir}' ROM file not found at '{romAbsolute}' — listing as invalid.");

        return new GameManifest(gameId, title, cfg.SteamDlcAppId, cfg.ThumbnailPath,
            Description: cfg.GameDescription,
            IsValid: romExists);
    }

    /// <summary>
    /// Pure and I/O-free: the DLC trust-tier decision described in this class's doc comment,
    /// isolated from directory scanning so it's directly unit-testable. Signed mode fails
    /// closed — a bad signature makes every claim untrusted regardless of its own value, and
    /// once verified, the map is authoritative AND complete (a gameId absent from it is
    /// rejected, not trusted through). Unsigned mode is a soft cross-check — a gameId listed in
    /// the map must match, but one absent from it is trusted as declared.
    /// </summary>
    internal static bool IsDlcClaimTrusted(string gameId, uint claimedAppId,
        IReadOnlyDictionary<string, uint>? trustedDlcAppIds, bool signingEnabled, bool signatureValid)
    {
        if (signingEnabled)
            return signatureValid
                && trustedDlcAppIds is not null
                && trustedDlcAppIds.TryGetValue(gameId, out uint expectedAppId)
                && expectedAppId == claimedAppId;

        return trustedDlcAppIds is null
            || !trustedDlcAppIds.TryGetValue(gameId, out uint softExpectedAppId)
            || softExpectedAppId == claimedAppId;
    }

    private static void LogDlcTrustFailure(string dir, string gameId, uint claimedAppId,
        IReadOnlyDictionary<string, uint>? trustedDlcAppIds, bool signingEnabled, bool signatureValid)
    {
        if (signingEnabled)
        {
            // A missing/invalid signature already logged one loud warning before the scan loop
            // started — every game fails for that same reason, so logging it again per-game
            // here would just be noise.
            if (signatureValid)
                Logger.LogAlways($"[GameScanner] '{dir}' (gameId '{gameId}') is missing from the signed " +
                                  $"gameDlcAppIds map, or its claimed steamDlcAppId doesn't match — listing as invalid.");
            return;
        }

        if (trustedDlcAppIds is not null && trustedDlcAppIds.TryGetValue(gameId, out uint softExpectedAppId))
            Logger.LogAlways($"[GameScanner] '{dir}' claims steamDlcAppId={claimedAppId} but the unsigned " +
                              $"shell manifest expects {softExpectedAppId} for gameId '{gameId}' — listing as invalid.");
    }
}
