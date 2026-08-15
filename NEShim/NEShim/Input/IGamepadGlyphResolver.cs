using NEShim.Localization;

namespace NEShim.Input;

/// <summary>
/// Facade injected into gamepad-binding UI code (see <see cref="UI.InGameMenu"/>/<see cref="UI.MainMenuScreen"/>'s
/// constructors) to resolve a bound identifier into display data for the binding row's value
/// column. Kept separate from <see cref="IGamepadGlyphSource"/> so consumers depend on one
/// simple entry point rather than the chain/caching machinery behind it.
/// </summary>
internal interface IGamepadGlyphResolver : IDisposable
{
    /// <summary>
    /// Resolves <paramref name="identifier"/> (an SDL-abstracted Xbox-style identifier, e.g.
    /// "A", "DPadUp", or null for an unbound row) to a glyph/text pair. Never fails — an unbound
    /// identifier or exhausted chain always falls back to a valid text result.
    /// </summary>
    GlyphResult Resolve(string? identifier, LocalizationData localization);
}
