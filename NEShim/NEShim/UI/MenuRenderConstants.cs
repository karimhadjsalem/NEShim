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

    // On Steam Deck, panel widths scale proportionally to the viewport so menus maintain
    // the same visual fraction of the screen as on the 768px windowed baseline (47% for
    // the main panel). On desktop (IsSteamDeck = false) returns baseW unchanged.
    internal static int PanelW(int baseW, int viewportW) =>
        PlatformDetector.IsSteamDeck
            ? Math.Min((int)(viewportW * baseW / 768f), viewportW - 60)
            : baseW;
}
