namespace NEShim.Config;

/// <summary>
/// One discovered entry under <c>games/</c> — enough to list and select it in the carousel
/// without loading its full <see cref="AppConfig"/>. <see cref="ThumbnailPath"/> and
/// <see cref="Description"/> are raw, unresolved config values (resolution against this
/// game's own <see cref="GameContext"/> happens in the carousel screen). An entry with
/// <see cref="IsValid"/> false still appears in the carousel — with a folder-name fallback
/// <see cref="DisplayTitle"/> when no config could be read — but cannot be launched; see
/// <see cref="GameScanner.Scan"/> for the exact validity rules.
/// </summary>
internal sealed record GameManifest(
    string GameId,
    string DisplayTitle,
    uint SteamDlcAppId,
    string ThumbnailPath,
    string Description = "",
    bool IsValid = true,
    string? ValidationError = null);
