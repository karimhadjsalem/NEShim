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
}
