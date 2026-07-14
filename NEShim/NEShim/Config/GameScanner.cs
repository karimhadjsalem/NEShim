namespace NEShim.Config;

/// <summary>
/// Discovers installed games under <c>games/</c> for the carousel. Independent of mode
/// *detection* (<see cref="MultiGameMode.IsActive"/>, which checks for <c>games/multigame.json</c>
/// only) — <c>games/</c> can legitimately exist with zero valid subfolders (e.g. DLC still
/// downloading); <see cref="Scan"/> simply returns an empty list in that case.
///
/// Every subfolder produces a <see cref="GameManifest"/> — none are ever skipped. A folder is
/// classified invalid (<see cref="GameManifest.IsValid"/> false) when its <c>config.json</c> is
/// missing or fails to parse, or when it parses but its configured <c>RomPath</c> does not
/// resolve to a file that exists on disk. A missing thumbnail image is deliberately NOT a
/// validation failure — that's an art-asset gap, not a structural configuration problem.
/// Players only ever see a generic "Game Error" note for an invalid entry (see
/// LocalizationData.CarouselUnavailable) — the specific reason below is written to neshim.log
/// unconditionally (Logger.LogAlways) rather than carried on the manifest for display.
/// </summary>
internal static class GameScanner
{
    internal static IReadOnlyList<GameManifest> Scan(string gamesRoot)
    {
        if (!Directory.Exists(gamesRoot)) return Array.Empty<GameManifest>();

        var results = new List<GameManifest>();
        foreach (var dir in Directory.GetDirectories(gamesRoot))
        {
            string gameId = Path.GetFileName(dir);
            string configPath = Path.Combine(dir, "config.json");

            if (!File.Exists(configPath))
            {
                Logger.LogAlways($"[GameScanner] '{dir}' has no config.json — listing as invalid.");
                results.Add(new GameManifest(gameId, gameId, SteamDlcAppId: 0, ThumbnailPath: "", IsValid: false));
                continue;
            }

            if (!ConfigLoader.TryParseFrom(configPath, out var cfg))
            {
                Logger.LogAlways($"[GameScanner] '{dir}' config.json failed to parse — listing as invalid.");
                results.Add(new GameManifest(gameId, gameId, SteamDlcAppId: 0, ThumbnailPath: "", IsValid: false));
                continue;
            }

            string title = string.IsNullOrWhiteSpace(cfg.GameDisplayTitle) ? cfg.WindowTitle : cfg.GameDisplayTitle;
            var ctx = GameContext.ForGame(gamesRoot, gameId);
            string romAbsolute = GameContext.ResolvePath(cfg.RomPath, ctx);
            bool romExists = File.Exists(romAbsolute);
            if (!romExists)
                Logger.LogAlways($"[GameScanner] '{dir}' ROM file not found at '{romAbsolute}' — listing as invalid.");

            results.Add(new GameManifest(gameId, title, cfg.SteamDlcAppId, cfg.ThumbnailPath,
                Description: cfg.GameDescription,
                IsValid: romExists));
        }

        return results.OrderBy(g => g.DisplayTitle, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
