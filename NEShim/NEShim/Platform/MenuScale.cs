namespace NEShim.Platform;

/// <summary>
/// Scale factor applied to menu/carousel font sizes and row-height layout constants, so text
/// stays visually consistent across window sizes instead of staying pinned to a fixed pixel
/// size — which looks tiny on a large fullscreen display and comparatively huge in a small
/// window. <see cref="Scale"/> is refreshed by <see cref="UpdateViewport"/> on every overlay
/// render pass (see D3D11Renderer/SDL3HwRenderer's RenderOverlayBitmap), so it always reflects
/// the window's current pixel resolution — including live changes from windowed/fullscreen
/// toggling (F11/Y) or manual resizing. Both read and write happen on the render/main thread
/// only, so no synchronization is needed.
/// </summary>
internal static class MenuScale
{
    // The app's default windowed launch size (see Program.cs) — the resolution every menu font
    // size in this codebase was originally tuned against.
    private const int ReferenceWidth  = 1024;
    private const int ReferenceHeight = 672;

    // 1.5× targets 18pt item text and 63px row height on the 7-inch 1280×800 panel held at
    // ~12-18 inches. Matches SteamOS's own 125-150% UI scaling recommendation for this display
    // at handheld distance — a fixed device-class tuning (viewing distance, not pixel density),
    // so it overrides rather than combines with the resolution-relative factor below.
    private const float SteamDeckScale = 1.5f;

    private static float _scale = 1.0f;

    internal static float Scale => _scale;

    internal static void UpdateViewport(int viewportWidth, int viewportHeight) =>
        _scale = ComputeScale(viewportWidth, viewportHeight);

    /// <summary>Pure computation, exposed separately so it's unit-testable without mutating the live <see cref="Scale"/> state.</summary>
    internal static float ComputeScale(int viewportWidth, int viewportHeight) =>
        PlatformDetector.IsSteamDeck
            ? SteamDeckScale
            : Math.Min(viewportWidth / (float)ReferenceWidth, viewportHeight / (float)ReferenceHeight);
}
