using NEShim.Rendering;
using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Presentational component for one slider row (label / fill bar / value number) — shared by
/// MenuRenderer and MainMenuRenderer. Previously only MainMenuRenderer extracted this geometry
/// (as <c>ComputeSliderGeometry</c>) for unit testing while MenuRenderer computed it inline;
/// unifying here gives the in-game menu the same test coverage for the first time.
/// </summary>
internal static class SliderControl
{
    private const float ItemTextIndent = 13f;

    private static readonly SDL.Color AccentBar = new() { R = 160, G = 200, B = 255, A = 255 };
    private static readonly SDL.Color BarFill   = new() { R =  80, G = 140, B = 240, A = 255 };
    private static readonly SDL.Color BarEmpty  = new() { R =  35, G =  35, B =  55, A = 220 };

    /// <summary>
    /// Pure geometry for one slider row, unit-tested independent of drawing (which needs a real
    /// SDL3PaintContext) — mirrors the ComputeDisplayRect/ComputeCoverSrcFRect convention used
    /// elsewhere for extracting testable layout math out of stateless renderers.
    /// </summary>
    internal static SliderGeometry ComputeGeometry(SDL.Rect itemRect, float labelColumnW, float scale)
    {
        float contentX = itemRect.X + ItemTextIndent;
        float contentW = itemRect.W - ItemTextIndent;
        float labelW   = labelColumnW;
        float valueW   =  48f * scale;
        float valuePad =   5f * scale; // keeps the value number from hugging the panel's right edge
        float barGap   =   6f * scale;
        float barH     =   8f * scale;
        float barX     = contentX + labelW + barGap;
        float barW     = contentW - labelW - barGap * 2f - valueW - valuePad;
        float barY     = itemRect.Y + (itemRect.H - barH) * 0.5f;
        float valueX   = barX + Math.Max(0f, barW) + barGap;

        return new SliderGeometry(
            LabelRect: new SDL.FRect { X = contentX, Y = itemRect.Y, W = labelW, H = itemRect.H },
            BarRect:   new SDL.FRect { X = barX, Y = barY, W = barW, H = barH },
            ValueRect: new SDL.FRect { X = valueX, Y = itemRect.Y, W = valueW, H = itemRect.H });
    }

    internal static void Draw(SDL3PaintContext ctx, SliderItemData data, SDL.Rect itemRect,
        SDL.Color textColor, string fontFamily, bool selected, float labelColumnW, float scale)
    {
        if (selected)
        {
            var accentLine = new SDL.FRect { X = itemRect.X + 4f, Y = itemRect.Y + 5f, W = 3f, H = itemRect.H - 10f };
            ctx.FillRect(accentLine, AccentBar);
        }

        var geo = ComputeGeometry(itemRect, labelColumnW, scale);

        ctx.DrawText(data.Label, geo.LabelRect,
            textColor, fontFamily, 12f * scale, bold: false, TextHAlign.Near, TextVAlign.Center);

        if (geo.BarRect.W > 0)
        {
            ctx.FillRect(geo.BarRect, BarEmpty);
            float fillW = Math.Clamp(data.Fill01, 0f, 1f) * geo.BarRect.W;
            if (fillW > 0)
                ctx.FillRect(geo.BarRect with { W = fillW }, BarFill);
        }

        // Near (left) rather than Far (right): ValueRect's far edge is always pinned to the row's
        // own right edge regardless of its width (see ComputeGeometry — barW absorbs whatever's
        // left after subtracting it), so right-aligning here glued the visible digits to the
        // panel's right edge — far from the bar they describe. Left-aligning puts them
        // immediately after it.
        ctx.DrawText(data.ValueText, geo.ValueRect,
            textColor, fontFamily, 11f * scale, bold: false, TextHAlign.Near, TextVAlign.Center);
    }

    /// <summary>
    /// Measures every slider label among <paramref name="sliderData"/> and returns a column width
    /// wide enough to fit the longest one (with a small padding margin), so every bar on screen
    /// starts at the same X position as the user navigates. Takes pre-fetched data rather than a
    /// menu reference so this stays a pure presentational component.
    /// </summary>
    internal static float ComputeLabelColumnW(SDL3PaintContext ctx, IReadOnlyList<SliderItemData?> sliderData,
        string fontFamily, float scale)
    {
        float maxLabelW = 0f;
        foreach (var slider in sliderData)
        {
            if (!slider.HasValue) continue;
            var (w, _) = ctx.MeasureText(slider.Value.Label, fontFamily, 12f * scale, bold: false);
            maxLabelW = Math.Max(maxLabelW, w);
        }
        const float MinColumnW   = 80f;
        const float LabelPadding = 10f;
        return Math.Max(MinColumnW * scale, maxLabelW + LabelPadding * scale);
    }
}
