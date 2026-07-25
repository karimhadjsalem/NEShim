using NEShim.Platform;

namespace NEShim.UI;

/// <summary>
/// Layout constants shared between <see cref="MenuRenderer"/> and <see cref="MainMenuRenderer"/>.
/// Both menus show the same NES controller diagram column when a binding screen is wide enough.
/// </summary>
internal static class MenuRenderConstants
{
    internal const int ControllerAreaW = 260; // width of the right-side controller column

    // FullPanelW - ControllerAreaW gives the binding screens' item-list column its width. The
    // longest values shown there are gamepad button names (e.g. "RightShoulder", "LeftThumb"),
    // longer than any keyboard key name or localized action label — 620 keeps that column wide
    // enough for those to render on one line in the common case (DrawText still word-wraps
    // rather than bleeding/truncating if a future longer string doesn't fit).
    internal const int FullPanelW      = 620; // panel width when controller is shown
    internal const int SlimPanelW      = 480; // panel width when controller is hidden
    internal const int MinWidthForCtrl = 580; // minimum bounds.Width to show controller column

    // On Steam Deck, panel widths scale by the same 1.5× factor as fonts and row heights,
    // capped at viewportW - 60 to prevent overflow on very small viewports.
    // On desktop (IsSteamDeck = false) returns baseW unchanged — deliberately preserved:
    // narrower panels (Settings, and any other screen using SlimPanelW/MainPanelMaxW/
    // RebindPanelMaxW/DisconnectPanelW) are meant to read as visibly skinnier than the wider
    // binding-screen panels, and scaling every panel by the same resolution-relative
    // MenuScale.Scale on desktop erased that distinction (they all grew by the same ratio,
    // so a Slim panel just became a bigger Slim panel instead of staying comparatively narrow).
    // See ScaledPanelW for the one case that DOES need to grow on desktop too.
    internal static int PanelW(int baseW, int viewportW) =>
        PlatformDetector.IsSteamDeck
            ? Math.Min((int)Math.Round(baseW * MenuScale.Scale), viewportW - 60)
            : baseW;

    // Used only for the binding-screen geometry (FullPanelW, ControllerAreaW) — the controller
    // diagram column and its enclosing panel must grow with MenuScale.Scale on every platform,
    // not just Steam Deck, or the item list text (which already scales via MenuScale.Scale in
    // every DrawText call) outgrows its fixed-width column and visibly bleeds into the
    // controller diagram at any fullscreen resolution meaningfully larger than the 1024×672
    // reference (reproduced on both Windows and native Linux at 1920×1080, July 2026).
    internal static int ScaledPanelW(int baseW, int viewportW) =>
        Math.Min((int)Math.Round(baseW * MenuScale.Scale), viewportW - 60);
}
