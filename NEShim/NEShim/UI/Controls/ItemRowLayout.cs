using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Computed layout for one <see cref="ItemRowControl"/> row, returned by
/// <c>ItemRowControl.ComputeLayout</c> so the geometry math is unit-testable independent of the
/// actual drawing, which needs a real <c>SDL3PaintContext</c>.
/// </summary>
/// <param name="AccentBarRect">Destination rect for the selection accent bar (drawn only when selected).</param>
/// <param name="TextRect">Destination rect for the label text. When <c>GlyphRect</c> is non-default,
/// this is a fixed-width label column; otherwise it spans the rest of the row and relies on
/// <c>TabStop</c> for internal tab-separated alignment.</param>
/// <param name="GlyphRect">Destination rect for the right-hand value glyph, or <c>default</c> when
/// the row has no glyph (plain text row).</param>
/// <param name="TabStop">Tab-stop offset passed through to <c>SDL3PaintContext.DrawText</c> for
/// glyph-less rows whose text contains an embedded tab separator.</param>
internal readonly record struct ItemRowLayout(
    SDL.FRect AccentBarRect,
    SDL.FRect TextRect,
    SDL.Rect GlyphRect,
    float TabStop);
