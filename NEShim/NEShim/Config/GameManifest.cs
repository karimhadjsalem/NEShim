namespace NEShim.Config;

/// <summary>
/// One discovered entry under <c>games/</c> — enough to list and select it in the carousel
/// without loading its full <see cref="AppConfig"/>. <see cref="ThumbnailPath"/> is reserved
/// for future carousel artwork; v1 does not load or render it.
/// </summary>
internal sealed record GameManifest(string GameId, string DisplayTitle, uint SteamDlcAppId, string ThumbnailPath);
