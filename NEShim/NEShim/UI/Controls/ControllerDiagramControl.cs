using NEShim.Rendering;
using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Presentational component for the NES controller diagram shown on gamepad binding screens
/// (active-button highlight + optional label above it) — shared by MenuRenderer and
/// MainMenuRenderer.
/// </summary>
internal static class ControllerDiagramControl
{
    private const float ControllerAspect  = 2.43f;
    private const float MinLabelGap       = 12f;
    private const float MaxLabelFontSize  = 14f;

    private static readonly SDL.Color LabelColor = new() { R = 200, G = 210, B = 230, A = 180 };

    internal static ControllerDiagramLayout ComputeLayout(SDL.FRect area, float scale)
    {
        float ctrlW = area.W;
        float ctrlH = ctrlW / ControllerAspect;
        if (ctrlH > area.H) { ctrlH = area.H; ctrlW = ctrlH * ControllerAspect; }
        float ox = area.X + (area.W - ctrlW) * 0.5f;
        float oy = area.Y + (area.H - ctrlH) * 0.5f;
        var controllerRect = new SDL.Rect { X = (int)ox, Y = (int)oy, W = (int)ctrlW, H = (int)ctrlH };

        float labelGap  = oy - area.Y;
        bool  showLabel = labelGap >= MinLabelGap;
        var   labelRect = new SDL.FRect { X = area.X, Y = area.Y, W = area.W, H = labelGap };
        float fontSize  = Math.Min(MaxLabelFontSize, labelGap * 0.75f) * scale;

        return new ControllerDiagramLayout(controllerRect, labelRect, fontSize, showLabel);
    }

    internal static void Draw(SDL3PaintContext ctx, SDL.FRect area, string? activeButton, string label,
        string fontFamily, float scale)
    {
        var layout = ComputeLayout(area, scale);
        ctx.BlitSurface(ControllerSprites.Base, null, layout.ControllerRect);
        ControllerSprites.DrawHighlight(ctx, layout.ControllerRect, activeButton);

        if (layout.ShowLabel)
            ctx.DrawText(label, layout.LabelRect, LabelColor, fontFamily, layout.LabelFontSize, bold: true);
    }
}
