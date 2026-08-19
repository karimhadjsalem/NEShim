using SDL3;
using NEShim.Localization;

namespace NEShim.Input;

/// <summary>
/// Decorator — wraps an inner <see cref="IGamepadGlyphResolver"/> (in production, a
/// <see cref="ChainedGamepadGlyphResolver"/>) with caching, keeping "how do I resolve a glyph"
/// and "how do I cache one" as separately testable responsibilities. Caches the whole resolved
/// <see cref="GlyphResult"/> per identifier — cheap and correct regardless of which chain tier
/// answered, since disposal only needs to know "is there a surface to free," not which source
/// produced it.
/// <para>
/// Cache is invalidated on a gamepad connect/disconnect edge (a different controller may now be
/// active) and on a language change (the text tier's result is localization-dependent) — both
/// wired from <c>NEShimApp</c>'s composition root via <see cref="InvalidateCache"/>. This class
/// never runs on the gameplay thread — glyph resolution only happens from menu-render/menu-label
/// code, so there is no hot-path impact from caching or its invalidation.
/// </para>
/// </summary>
internal sealed class CachingGamepadGlyphResolver : IGamepadGlyphResolver
{
    private readonly IGamepadGlyphResolver _inner;
    private readonly Dictionary<string, GlyphResult> _cache = new();

    internal CachingGamepadGlyphResolver(IGamepadGlyphResolver inner) => _inner = inner;

    public GlyphResult Resolve(string? identifier, LocalizationData localization)
    {
        if (identifier is null)
            return _inner.Resolve(null, localization);

        if (_cache.TryGetValue(identifier, out var cached))
            return cached;

        var result = _inner.Resolve(identifier, localization);
        _cache[identifier] = result;
        return result;
    }

    /// <summary>Frees every cached surface and clears the cache. Safe to call repeatedly.</summary>
    internal void InvalidateCache()
    {
        foreach (var result in _cache.Values)
            if (result.Glyph != IntPtr.Zero)
                SDL.DestroySurface(result.Glyph);
        _cache.Clear();
    }

    public void Dispose()
    {
        InvalidateCache();
        _inner.Dispose();
    }
}
