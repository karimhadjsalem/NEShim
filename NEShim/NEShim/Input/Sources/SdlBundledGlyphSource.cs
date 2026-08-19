using System.Reflection;
using SDL3;
using NEShim.Localization;
using NEShim.Rendering;

namespace NEShim.Input.Sources;

/// <summary>
/// Adapter — second strategy in <see cref="ChainedGamepadGlyphResolver"/>'s chain, used when
/// the Steam Input glyph tier has no answer (typically: Steam not running at all — a
/// dev/local-testing-only case, since the shipped game always relaunches itself through Steam).
/// Maps <see cref="IGamepadDevice.GetGamepadType"/> to a bundled per-brand embedded glyph PNG,
/// following the same "NEShim.Assets.{...}.png" embedded-resource convention as
/// <see cref="Localization.FlagImageLoader"/>. Returns null when the brand is unknown or the
/// specific asset is missing so the chain falls through to the text tier.
/// <para>
/// Stateless by design — caching (and the resulting surface's disposal) is owned entirely by
/// <see cref="CachingGamepadGlyphResolver"/>, not duplicated here.
/// </para>
/// </summary>
internal sealed class SdlBundledGlyphSource : IGamepadGlyphSource
{
    private static readonly Assembly Assembly = typeof(SdlBundledGlyphSource).Assembly;

    private readonly IGamepadDevice _gamepadDevice;
    private readonly uint _playerIndex;

    /// <param name="playerIndex">0-based gamepad slot this source reads the brand from (player 1 = 0, ...).</param>
    internal SdlBundledGlyphSource(IGamepadDevice gamepadDevice, uint playerIndex = 0)
    {
        _gamepadDevice = gamepadDevice;
        _playerIndex   = playerIndex;
    }

    public GlyphResult? TryResolve(string identifier, LocalizationData localization)
    {
        string? brand = BrandFor(_gamepadDevice.GetGamepadType(_playerIndex));
        if (brand is null) return null;

        IntPtr surface = Load(brand, identifier);
        return surface == IntPtr.Zero ? null : new GlyphResult(surface, string.Empty);
    }

    /// <summary>Pure lookup, exposed internally so it's unit-testable without the SDL_image decode this class's own TryResolve otherwise requires.</summary>
    internal static string? BrandFor(SDL.GamepadType type) => type switch
    {
        // "Standard" is SDL's fallback for a controller it recognizes as following the spec
        // layout (what generic/XInput-compatible pads report) but can't attribute to a specific
        // brand. Its face-button semantics (A=south, B=east, X=west, Y=north) match Xbox's, so
        // reusing Xbox art here is a faithful mapping, unlike PlayStation/Switch (different
        // physical layout/labels) or the bundled Kenney "Generic" pack (aimed at joysticks/HOTAS
        // devices — unlabeled buttons, no D-pad, no Start/Back — not a fit for this controller class).
        SDL.GamepadType.Xbox360 or SDL.GamepadType.XboxOne or SDL.GamepadType.Standard => "xbox",
        SDL.GamepadType.PS3 or SDL.GamepadType.PS4 or SDL.GamepadType.PS5                      => "playstation",
        SDL.GamepadType.NintendoSwitchPro                                                      => "switchpro",
        // A lone Joy-Con (left or right, held sideways) reuses switchpro's art. SDL's own
        // remapping of a standalone Joy-Con's physical buttons onto the canonical 18-identifier
        // scheme (e.g. what "Y" even means on a controller with no Y button) isn't something
        // this codebase has verified against real hardware, so this deliberately doesn't attempt
        // a more precise single-Joycon-specific icon set — reusing switchpro is correct for the
        // vast majority of identifiers regardless (A/B/X/Y/shoulders/thumbsticks/Start/Back/
        // analog-stick directions are all identical art to the Pro Controller either way).
        SDL.GamepadType.NintendoSwitchJoyconLeft or SDL.GamepadType.NintendoSwitchJoyconRight   => "switchpro",
        // Paired Joy-Cons form one full virtual controller — same art as switchpro except the
        // D-pad-equivalent identifiers, which use the paired-Joycons'-4-round-face-buttons icon
        // set instead of a real cross-shaped D-pad (Joy-Cons don't have one).
        SDL.GamepadType.NintendoSwitchJoyconPair                                               => "switchjoyconpair",
        // SDL has no way to distinguish a Steam Deck's built-in controller from a standalone
        // Steam Controller — both report as this single type (a separate
        // SDL_GAMEPAD_TYPE_STEAM_CONTROLLER was proposed upstream and closed as "not planned":
        // github.com/libsdl-org/SDL/issues/12250). Steam Deck art is the more broadly useful
        // default for this game.
        SDL.GamepadType.Steam                                                                  => "steamdeck",
        _ => null,
    };

    private static IntPtr Load(string brand, string identifier)
    {
        string resourceName = $"NEShim.Assets.gamepad_glyphs.{brand}.{identifier}.png";
        try
        {
            using var stream = Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                Logger.Log($"[Input] SdlBundledGlyphSource: resource '{resourceName}' not found — glyph omitted.");
                return IntPtr.Zero;
            }
            return SdlSurfaceLoader.LoadFromStream(stream);
        }
        catch (Exception ex)
        {
            Logger.Log($"[Input] SdlBundledGlyphSource: failed to load '{resourceName}': {ex.Message}");
            return IntPtr.Zero;
        }
    }
}
