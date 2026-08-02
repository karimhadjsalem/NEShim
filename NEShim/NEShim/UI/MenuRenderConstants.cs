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

    // Every panel width must grow with MenuScale.Scale, on every platform — not just Steam Deck.
    // Item/title text (via DrawText) and row spacing (via MenuRenderer/MainMenuRenderer's S())
    // already re-read MenuScale.Scale live on every frame; a panel width that doesn't scale the
    // same way desyncs from its own content the moment MenuScale.Scale departs 1.0. This was
    // previously desktop-only-fixed-width for SlimPanelW/MainPanelMaxW/RebindPanelMaxW/
    // DisconnectPanelW, on the theory that it kept those panels visibly skinnier than the wider
    // binding-screen panels — but a fixed baseW while its own text scales up is exactly what
    // causes the text to overflow/word-wrap the panel, worse the further MenuScale.Scale departs
    // 1.0 (i.e. most visible at fullscreen on a large display). Confirmed via real overflow of
    // "Select Save Slot"/"Return to Main Menu"/"Yes, return to main menu" et al at fullscreen on
    // both Windows and native Linux, and selected (bold) text wrapping when the same regular-
    // weight string barely fit (bold glyphs render wider at an identical font size and rect
    // width — see MenuRenderer/MainMenuRenderer's DrawItemRow). Scaling every panel by the same
    // factor still preserves Slim-vs-Full's relative narrowness (their width ratio is constant
    // regardless of scale) without letting either one's content outgrow it. Capped at
    // viewportW - 60 to prevent overflow on very small viewports.
    internal static int ScaledPanelW(int baseW, int viewportW) =>
        Math.Min((int)Math.Round(baseW * MenuScale.Scale), viewportW - 60);
}
