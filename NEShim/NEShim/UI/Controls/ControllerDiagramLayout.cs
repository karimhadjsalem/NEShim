using SDL3;

namespace NEShim.UI.Controls;

/// <summary>
/// Computed layout for the <see cref="ControllerDiagramControl"/>, returned by
/// <c>ControllerDiagramControl.ComputeLayout</c> so the aspect-fit math is unit-testable
/// independent of the actual drawing, which needs a real <c>SDL3PaintContext</c>.
/// </summary>
/// <param name="ControllerRect">Destination rect for the NES controller sprite, fit into the
/// available area while preserving its aspect ratio.</param>
/// <param name="LabelRect">Destination rect for the optional label above the sprite.</param>
/// <param name="LabelFontSize">Font size for the label, scaled to fit the available gap.</param>
/// <param name="ShowLabel">Whether the gap above the sprite is tall enough to draw the label at all.</param>
internal readonly record struct ControllerDiagramLayout(
    SDL.Rect ControllerRect, SDL.FRect LabelRect, float LabelFontSize, bool ShowLabel);
