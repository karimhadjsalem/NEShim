namespace NEShim.Config;

/// <summary>
/// Detects whether this publish is a multi-game (carousel) deployment. Driven by a dedicated
/// manifest file, not by scanning for per-game content: <c>games/multigame.json</c> ships as
/// part of the base multi-game publish itself (built once, alongside the exe — the same way
/// <c>steam_appid.txt</c> does today), independent of whether any DLC depot has actually
/// landed on disk yet. This matters concretely: on a fresh install where Steam hasn't finished
/// downloading any game DLC, <c>games/</c> may not even exist yet — detection must not depend
/// on DLC content being present, only on whether this is fundamentally a multi-game build. A
/// single-game publish never ships this file, so its absence is an unambiguous, zero-cost
/// (one <see cref="File.Exists(string)"/> call) signal.
/// </summary>
internal static class MultiGameMode
{
    internal const string GamesFolderName  = "games";
    internal const string ManifestFileName = "multigame.json";

    internal static string GamesRoot    => Path.Combine(AppContext.BaseDirectory, GamesFolderName);
    internal static string ManifestPath => Path.Combine(GamesRoot, ManifestFileName);

    internal static readonly bool IsActive = File.Exists(ManifestPath);

    /// <summary>
    /// Discrete, carousel-only user preference store — separate from both the single-game
    /// scheme (%APPDATA%\&lt;windowTitle&gt;\user.json) and the per-game scheme
    /// (%APPDATA%\NEShim\Games\&lt;gameId&gt;\user.json), so the carousel's own window-mode
    /// preference (see NEShimApp.InitializeCarousel/SetWindowMode) survives both game
    /// transitions and a full process relaunch without being confused with any individual
    /// game's own WindowMode.
    /// </summary>
    internal static string ShellUserConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NEShim", "shell-user.json");
}
