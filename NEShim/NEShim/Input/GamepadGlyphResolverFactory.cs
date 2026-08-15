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
    internal static CachingGamepadGlyphResolver Create(IGamepadDevice gamepadDevice)
    {
        var chain = new ChainedGamepadGlyphResolver(new IGamepadGlyphSource[]
        {
            new SteamInputGlyphSource(),
            new SdlBundledGlyphSource(gamepadDevice),
            new TextGlyphSource(),
        });
        return new CachingGamepadGlyphResolver(chain);
    }
}
