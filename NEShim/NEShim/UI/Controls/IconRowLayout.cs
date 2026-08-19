using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Computed layout for one <see cref="IconRowControl"/> row, returned by
/// <c>IconRowControl.ComputeLayout</c> so the geometry math is unit-testable independent of the
/// actual drawing, which needs a real <c>SDL3PaintContext</c>.
/// </summary>
/// <param name="IconRect">Destination rect for the left-hand icon.</param>
/// <param name="TextRect">Destination rect for the label text, offset past the icon.</param>
internal readonly record struct IconRowLayout(SDL.Rect IconRect, SDL.FRect TextRect);
