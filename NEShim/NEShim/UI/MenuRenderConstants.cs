using NEShim.Platform;

namespace NEShim.UI;

/// <summary>
/// Layout constants shared between <see cref="MenuRenderer"/> and <see cref="MainMenuRenderer"/>.
/// Both menus show the same NES controller diagram column when a binding screen is wide enough.
/// </summary>
internal static class MenuRenderConstants
{
    internal const int ControllerAreaW = 260; // width of the right-side controller column
    internal const int FullPanelW      = 520; // panel width when controller is shown
    internal const int SlimPanelW      = 440; // panel width when controller is hidden
    internal const int MinWidthForCtrl = 580; // minimum bounds.Width to show controller column

    // On Steam Deck, panel widths scale by the same 1.5× factor as fonts and row heights,
    // capped at viewportW - 60 to prevent overflow on very small viewports.
    // On desktop (IsSteamDeck = false) returns baseW unchanged.
    internal static int PanelW(int baseW, int viewportW) =>
        PlatformDetector.IsSteamDeck
            ? Math.Min((int)Math.Round(baseW * MenuScale.Scale), viewportW - 60)
            : baseW;
}
