using NEShim.Localization;

namespace NEShim.Input;

/// <summary>
/// Strategy interface for one glyph backend in the gamepad-binding value-column chain
/// (see <see cref="ChainedGamepadGlyphResolver"/>). Mirrors <see cref="NEShim.Localization.ILanguageResolver"/>'s
/// role for language resolution — each implementation adapts one glyph source (Steam Input,
/// bundled per-brand assets, plain text) behind a common try-resolve shape.
/// </summary>
internal interface IGamepadGlyphSource
{
    /// <summary>
    /// Returns a result if this source can answer for <paramref name="identifier"/>
    /// (an SDL-abstracted Xbox-style identifier, e.g. "A", "DPadUp"), or null if it has no
    /// answer — the chain moves on to the next source in that case.
    /// </summary>
    GlyphResult? TryResolve(string identifier, LocalizationData localization);
}
