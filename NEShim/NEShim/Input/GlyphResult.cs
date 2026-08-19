namespace NEShim.Input;

/// <summary>
/// Result of resolving a gamepad binding identifier to display data for a binding row.
/// <see cref="Glyph"/> is an SDL surface handle (<see cref="IntPtr.Zero"/> when no image is
/// available for this identifier); <see cref="Text"/> is always populated as a text fallback.
/// </summary>
internal readonly record struct GlyphResult(IntPtr Glyph, string Text);
