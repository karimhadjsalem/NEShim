using NEShim.Localization;
using NEShim.Rendering;
using NEShim.Steam;

namespace NEShim.Input.Sources;

/// <summary>
/// Adapter — first strategy in <see cref="ChainedGamepadGlyphResolver"/>'s chain. Wraps
/// <see cref="SteamInputGlyphManager"/> (itself a thin Steamworks wrapper) behind
/// <see cref="IGamepadGlyphSource"/>, isolating the untestable native-call boundary to as
/// little surface as possible. Returns null whenever Steam/controller/origin/asset is
/// unavailable so the chain moves on — never throws for the "not available" case.
/// </summary>
internal sealed class SteamInputGlyphSource : IGamepadGlyphSource
{
    private readonly int _controllerIndex;

    /// <param name="controllerIndex">0-based Steam Input controller slot (player 1 = 0, ...).</param>
    internal SteamInputGlyphSource(int controllerIndex = 0) => _controllerIndex = controllerIndex;

    public GlyphResult? TryResolve(string identifier, LocalizationData localization)
    {
        if (!XboxOriginMap.Map.TryGetValue(identifier, out var xboxOrigin))
            return null;

        string? path = SteamInputGlyphManager.GetGlyphPath(xboxOrigin, _controllerIndex);
        if (path is null) return null;

        IntPtr surface = SdlSurfaceLoader.LoadFromFile(path);
        if (surface == IntPtr.Zero) return null;

        // Text is never consumed by binding-row rendering when Glyph is non-zero (see
        // MenuRenderer/MainMenuRenderer's DrawItemRow) — the label row's own text comes
        // independently from GetGamepadLabel. Left empty here rather than duplicating
        // MenuBindingHelpers' text formatting, which would pull an Input-layer class into UI.
        return new GlyphResult(surface, string.Empty);
    }
}
