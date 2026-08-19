using Steamworks;

namespace NEShim.Steam;

/// <summary>
/// Thin wrapper around the ISteamInput glyph-lookup calls — kept separate from
/// <see cref="SteamInputManager"/>, whose own doc comment scopes it explicitly to action-set
/// polling. Glyph lookup is a distinct responsibility that happens to reuse
/// <see cref="SteamInputManager.ControllerHandle"/>.
///
/// Deliberately does NOT require the game_actions VDF / action-set system: GetActionOriginFromXboxOrigin
/// translates a raw Xbox-style button directly to the origin representing that same physical
/// input on the player's actual connected controller, independent of any action-manifest
/// (confirmed via Valve's "Steam Input Gamepad Emulation - Best Practices" docs).
/// </summary>
internal static class SteamInputGlyphManager
{
    /// <summary>
    /// Returns a local filesystem path to the PNG glyph for <paramref name="xboxOrigin"/> on the
    /// connected controller at <paramref name="controllerIndex"/> (0-based, player 1 = 0), or
    /// null when Steam Input is unavailable, no controller is connected at that index, or the
    /// origin/glyph can't be resolved.
    /// </summary>
    internal static string? GetGlyphPath(EXboxOrigin xboxOrigin, int controllerIndex = 0)
    {
        if (!SteamManager.IsAvailable || !SteamInputManager.IsAvailable) return null;

        var handle = SteamInputManager.ControllerHandle(controllerIndex);
        if (handle == default) return null;

        try
        {
            var origin = SteamInput.GetActionOriginFromXboxOrigin(handle, xboxOrigin);
            if (origin == EInputActionOrigin.k_EInputActionOrigin_None) return null;

            string? path = SteamInput.GetGlyphPNGForActionOrigin(
                origin, ESteamInputGlyphSize.k_ESteamInputGlyphSize_Medium, 0);
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (Exception ex)
        {
            Logger.Log($"[SteamInput] Glyph lookup failed for {xboxOrigin}: {ex.Message}");
            return null;
        }
    }
}
