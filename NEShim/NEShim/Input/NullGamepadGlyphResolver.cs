using NEShim.Localization;

namespace NEShim.Input;

/// <summary>
/// Null Object — the default <see cref="IGamepadGlyphResolver"/> used when a menu is
/// constructed without one supplied (e.g. existing tests that build <c>InGameMenu</c>/
/// <c>MainMenuScreen</c> directly without wiring the full composition root). Behaves exactly
/// like today's pre-glyph behavior: text only, no image, via <see cref="GamepadButtonLocalizer"/>.
/// In production, <c>NEShimApp</c> always supplies a real resolver from
/// <see cref="GamepadGlyphResolverFactory"/> instead.
/// </summary>
internal sealed class NullGamepadGlyphResolver : IGamepadGlyphResolver
{
    internal static readonly NullGamepadGlyphResolver Instance = new();

    private NullGamepadGlyphResolver() { }

    public GlyphResult Resolve(string? identifier, LocalizationData localization)
        => new(IntPtr.Zero, GamepadButtonLocalizer.Localize(identifier, localization));

    public void Dispose() { }
}
