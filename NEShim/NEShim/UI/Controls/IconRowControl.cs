using NEShim.Rendering;
using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Presentational component for a menu item row with a left-hand icon (used only by the
/// Language screen's flag rows today) — shared by MenuRenderer and MainMenuRenderer. Takes the
/// already-resolved text color as a prop; callers resolve enabled/disabled color before calling
/// (see ScreenHandler.IsItemEnabled), so this component has no internal enabled/disabled branch
/// of its own — the icon rows it draws for (Language) are always enabled in practice.
/// </summary>
internal static class IconRowControl
{
    private const int IconW   = 20;
    private const int IconH   = 14;
    private const int IconGap = 4;

    internal static IconRowLayout ComputeLayout(SDL.Rect itemRect, float scale)
    {
        int iconW = S(IconW, scale);
        int iconH = S(IconH, scale);
        var iconRect = new SDL.Rect
        {
            X = itemRect.X + S(2, scale),
            Y = itemRect.Y + (itemRect.H - iconH) / 2,
            W = iconW,
            H = iconH,
        };

        int textOffsetX = S(IconW + IconGap, scale);
        var textRect = new SDL.FRect { X = itemRect.X + textOffsetX, Y = itemRect.Y, W = itemRect.W - textOffsetX, H = itemRect.H };

        return new IconRowLayout(iconRect, textRect);
    }

    internal static void Draw(SDL3PaintContext ctx, IntPtr icon, string text, bool selected,
        SDL.Rect itemRect, SDL.Color textColor, string fontFamily, float scale)
    {
        var layout = ComputeLayout(itemRect, scale);
        ctx.BlitSurface(icon, null, layout.IconRect);
        ctx.DrawText(text, layout.TextRect, textColor, fontFamily, 12f * scale, selected, TextHAlign.Near, TextVAlign.Center);
    }

    private static int S(int value, float scale) => (int)Math.Round(value * scale);
}
