namespace NEShim.UI;

/// <summary>
/// Data returned by <c>ScreenHandler.GetSliderData</c> for items that should be rendered
/// as a graphical fill bar rather than a text string.
/// </summary>
/// <param name="Label">Display label drawn to the left of the bar.</param>
/// <param name="Fill01">Fill fraction in [0, 1] (0 = empty, 1 = full).</param>
/// <param name="ValueText">Formatted value drawn to the right of the bar (e.g. "75" or "+5").</param>
internal readonly record struct SliderItemData(string Label, float Fill01, string ValueText);
