namespace NEShim.Platform;

/// <summary>
/// Scale factor applied to menu font sizes and row-height layout constants on Steam Deck.
/// Evaluated once at startup from <see cref="PlatformDetector.IsSteamDeck"/>.
/// </summary>
internal static class MenuScale
{
    private const float SteamDeckScale = 1.3f;

    internal static float Scale { get; } = PlatformDetector.IsSteamDeck ? SteamDeckScale : 1.0f;
}
