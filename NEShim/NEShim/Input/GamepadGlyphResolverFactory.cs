using NEShim.Input.Sources;

namespace NEShim.Input;

/// <summary>
/// Factory — builds and wires the gamepad glyph resolution chain in one place, mirroring the
/// project's existing "construct the right strategy for this environment" convention
/// (<c>RendererFactory</c>, <c>OverlayRendererFactory</c>, <c>MotionEffectFactory</c>/
/// <c>SdlMotionEffectFactory</c>, <c>D3D11FilterFactory</c>/<c>SdlFilterFactory</c>).
/// Called once from <c>NEShimApp</c>'s composition root.
/// </summary>
internal static class GamepadGlyphResolverFactory
{
    /// <param name="playerIndex">0-based player slot (player 1 = 0, ...) this resolver serves.
    /// Each player gets its own resolver instance with its own cache (see
    /// <see cref="CachingGamepadGlyphResolver"/>'s doc comment) so a different-brand gamepad for
    /// one player can never be shadowed by another player's cached glyph for the same raw
    /// identifier string.</param>
    internal static CachingGamepadGlyphResolver Create(IGamepadDevice gamepadDevice, int playerIndex = 0)
    {
        var chain = new ChainedGamepadGlyphResolver(new IGamepadGlyphSource[]
        {
            new SteamInputGlyphSource(playerIndex),
            new SdlBundledGlyphSource(gamepadDevice, (uint)playerIndex),
            new TextGlyphSource(),
        });
        return new CachingGamepadGlyphResolver(chain);
    }
}
