using NEShim.Localization;

namespace NEShim.Input.Sources;

/// <summary>
/// Adapter + Null Object — third and terminal strategy in <see cref="ChainedGamepadGlyphResolver"/>'s
/// chain. Wraps <see cref="GamepadButtonLocalizer"/> (today's plain-text behavior). Never returns
/// null: this guarantees the chain always terminates with a valid result, so callers never need
/// to null-check the resolver's final output.
/// </summary>
internal sealed class TextGlyphSource : IGamepadGlyphSource
{
    public GlyphResult? TryResolve(string identifier, LocalizationData localization)
        => new GlyphResult(IntPtr.Zero, GamepadButtonLocalizer.Localize(identifier, localization));
}
