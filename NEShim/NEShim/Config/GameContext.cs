namespace NEShim.Config;

/// <summary>
/// Identifies one game's content root in multi-game mode and resolves its relative paths.
/// A null <see cref="GameContext"/> means single-game mode — every path resolves relative to
/// <see cref="AppContext.BaseDirectory"/>, exactly as it always has.
/// Public (not internal) because it appears in <see cref="ConfigLoader"/>'s public API surface.
/// </summary>
public sealed class GameContext
{
    public string RootDirectory { get; }
    public string GameId        { get; }

    internal GameContext(string rootDirectory, string gameId)
    {
        RootDirectory = rootDirectory;
        GameId        = gameId;
    }

    internal static GameContext ForGame(string gamesRoot, string gameId) =>
        new(Path.Combine(gamesRoot, gameId), gameId);

    /// <summary>
    /// Resolves a config-supplied path the same way every per-game path in NEShim always has:
    /// absolute paths are used verbatim; relative paths resolve against the active content
    /// root — <see cref="AppContext.BaseDirectory"/> when <paramref name="ctx"/> is null
    /// (today's single-game behavior), or this game's own folder when it's set.
    /// </summary>
    internal static string ResolvePath(string configuredPath, GameContext? ctx) =>
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(ctx?.RootDirectory ?? AppContext.BaseDirectory, configuredPath);
}
