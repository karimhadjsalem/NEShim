namespace NEShim.Platform;

/// <summary>
/// Scale factor applied to menu font sizes and row-height layout constants on Steam Deck.
/// Evaluated once at startup from <see cref="PlatformDetector.IsSteamDeck"/>.
/// </summary>
internal static class MenuScale
{
    private const float NormalScale = 1.0f;
    // 1.5× targets 18pt item text and 63px row height on the 7-inch 1280×800 panel
    // held at ~12-18 inches. Matches SteamOS's own 125-150% UI scaling recommendation
    // for this display at handheld distance. 1.3× (the previous value) left text at
    // 15.6pt which was below the comfortable legibility threshold.
    private const float SteamDeckScale = 1.5f;

    internal static float Scale { get; } = PlatformDetector.IsSteamDeck ? SteamDeckScale : NormalScale;
}
