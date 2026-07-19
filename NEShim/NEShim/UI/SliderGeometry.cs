using SDL3;

namespace NEShim.UI;

/// <summary>
/// Computed layout for one slider row (label / fill bar / value number), returned by
/// <c>MainMenuRenderer.ComputeSliderGeometry</c> so the geometry math is unit-testable
/// independent of the actual drawing, which needs a real <c>SDL3PaintContext</c>.
/// </summary>
/// <param name="LabelRect">Destination rect for the slider's label text.</param>
/// <param name="BarRect">Destination rect for the fill bar background. <c>W</c> may be negative
/// if the item is too narrow to fit label + bar + value — callers must check <c>BarRect.W > 0</c>
/// before drawing.</param>
/// <param name="ValueRect">Destination rect for the formatted value text, right-aligned within it.</param>
internal readonly record struct SliderGeometry(SDL.FRect LabelRect, SDL.FRect BarRect, SDL.FRect ValueRect);
