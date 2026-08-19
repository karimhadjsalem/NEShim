using NEShim.Rendering;
using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Presentational component for a menu panel's background fill + border — shared by
/// MenuRenderer and MainMenuRenderer's several distinct panel types (main list, disconnect
/// screen, rebind prompt).
/// </summary>
internal static class PanelFrameControl
{
    internal static void Draw(SDL3PaintContext ctx, SDL.FRect panelRect, SDL.Color fillColor,
        SDL.Color borderColor, float borderThickness = 2f)
    {
        ctx.FillRect(panelRect, fillColor);
        ctx.DrawRect(panelRect, borderColor, borderThickness);
    }
}
