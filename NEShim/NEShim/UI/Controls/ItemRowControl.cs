using NEShim.Rendering;
using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Presentational component for one plain-text menu item row (label, optional right-hand value
/// glyph, optional selection accent bar) — shared by MenuRenderer and MainMenuRenderer. Takes
/// only plain data (props) — no menu-type reference — so it composes into either renderer
/// unchanged.
/// </summary>
internal static class ItemRowControl
{
    // Left indent every item text observes, selected or not (keeps text column stable).
    // Deliberately unscaled, matching the constant's original per-renderer usage.
    private const float ItemTextIndent = 13f;

    // Gamepad glyph box (Kenney's input-prompts art, 64x64 source) — deliberately square.
    private const int GlyphSize = 18;

    private static readonly SDL.Color AccentBar = new() { R = 160, G = 200, B = 255, A = 255 };

    internal static ItemRowLayout ComputeLayout(SDL.Rect itemRect, float scale, bool hasValueIcon)
    {
        float tabStop = S(120, scale);
        var accentBarRect = new SDL.FRect { X = itemRect.X + 4f, Y = itemRect.Y + 5f, W = 3f, H = itemRect.H - 10f };

        if (hasValueIcon)
        {
            var textRect  = new SDL.FRect { X = itemRect.X + ItemTextIndent, Y = itemRect.Y, W = tabStop, H = itemRect.H };
            int glyphSize = S(GlyphSize, scale);
            var glyphRect = new SDL.Rect
            {
                X = (int)(itemRect.X + ItemTextIndent + tabStop),
                Y = itemRect.Y + (itemRect.H - glyphSize) / 2,
                W = glyphSize,
                H = glyphSize,
            };
            return new ItemRowLayout(accentBarRect, textRect, glyphRect, tabStop);
        }

        var fullTextRect = new SDL.FRect
        {
            X = itemRect.X + ItemTextIndent,
            Y = itemRect.Y,
            W = itemRect.W - ItemTextIndent,
            H = itemRect.H,
        };
        return new ItemRowLayout(accentBarRect, fullTextRect, default, tabStop);
    }

    internal static void Draw(SDL3PaintContext ctx, string text, SDL.Rect itemRect, SDL.Color color,
        string fontFamily, bool bold, bool selected, IntPtr valueIcon, float scale)
    {
        var layout = ComputeLayout(itemRect, scale, valueIcon != IntPtr.Zero);

        if (selected)
            ctx.FillRect(layout.AccentBarRect, AccentBar);

        if (valueIcon != IntPtr.Zero)
        {
            var (leftPart, _) = SDL3PaintContext.SplitTabText(text);
            ctx.DrawText(leftPart, layout.TextRect, color, fontFamily, 12f * scale, bold, TextHAlign.Near, TextVAlign.Center);
            ctx.BlitSurface(valueIcon, null, layout.GlyphRect);
            return;
        }

        ctx.DrawText(text, layout.TextRect, color, fontFamily, 12f * scale, bold,
            TextHAlign.Near, TextVAlign.Center, tabStop: layout.TabStop);
    }

    private static int S(int value, float scale) => (int)Math.Round(value * scale);
}
