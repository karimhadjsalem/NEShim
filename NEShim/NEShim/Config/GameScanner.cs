namespace NEShim.Config;

/// <summary>
/// Discovers installed games under <c>games/</c> for the carousel. Independent of mode
/// *detection* (<see cref="MultiGameMode.IsActive"/>, which checks for <c>games/multigame.json</c>
/// only) — <c>games/</c> can legitimately exist with zero valid subfolders (e.g. DLC still
/// downloading); <see cref="Scan"/> simply returns an empty list in that case.
/// </summary>
internal static class GameScanner
{
    internal static IReadOnlyList<GameManifest> Scan(string gamesRoot)
    {
        if (!Directory.Exists(gamesRoot)) return Array.Empty<GameManifest>();

        var results = new List<GameManifest>();
        foreach (var dir in Directory.GetDirectories(gamesRoot))
        {
            string configPath = Path.Combine(dir, "config.json");
            if (!File.Exists(configPath))
            {
                Logger.Log($"[GameScanner] Skipping '{dir}' — no config.json.");
                continue;
            }

            // File.Exists was already checked above, so LoadFrom's "auto-create defaults if
            // missing" branch is never reached here.
            var cfg = ConfigLoader.LoadFrom(configPath);

            string gameId = Path.GetFileName(dir);
            string title  = string.IsNullOrWhiteSpace(cfg.GameDisplayTitle) ? cfg.WindowTitle : cfg.GameDisplayTitle;
            results.Add(new GameManifest(gameId, title, cfg.SteamDlcAppId, ThumbnailPath: ""));
        }

        return results.OrderBy(g => g.DisplayTitle, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
