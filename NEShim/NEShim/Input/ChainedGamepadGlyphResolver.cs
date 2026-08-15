using NEShim.Localization;

namespace NEShim.Input;

/// <summary>
/// Chain of Responsibility over <see cref="IGamepadGlyphSource"/> strategies — tries each in
/// order, returns the first non-null result. Mirrors <see cref="Localization.ChainedLanguageResolver"/>'s
/// shape for the analogous "try several strategies in priority order" problem in language
/// resolution. Pure orchestration: no caching, no Steamworks/SDL calls of its own, so it's
/// testable with mocked <see cref="IGamepadGlyphSource"/> instances.
/// </summary>
internal sealed class ChainedGamepadGlyphResolver : IGamepadGlyphResolver
{
    private readonly IReadOnlyList<IGamepadGlyphSource> _sources;

    internal ChainedGamepadGlyphResolver(IReadOnlyList<IGamepadGlyphSource> sources)
        => _sources = sources;

    public GlyphResult Resolve(string? identifier, LocalizationData localization)
    {
        if (identifier is null)
            return new GlyphResult(IntPtr.Zero, localization.BindNone);

        foreach (var source in _sources)
        {
            var result = source.TryResolve(identifier, localization);
            if (result.HasValue) return result.Value;
        }

        // Unreachable in production wiring (GamepadGlyphResolverFactory always places a
        // TextGlyphSource last, which never returns null), but keeps this class correct even if
        // constructed directly with an incomplete chain (e.g. in a test).
        return new GlyphResult(IntPtr.Zero, GamepadButtonLocalizer.Localize(identifier, localization));
    }

    public void Dispose() { }
}
