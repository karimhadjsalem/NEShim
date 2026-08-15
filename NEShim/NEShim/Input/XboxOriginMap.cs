using Steamworks;

namespace NEShim.Input;

/// <summary>
/// Pure lookup table from NEShim's SDL-abstracted Xbox-style gamepad identifiers
/// (the same strings <see cref="SDL3GamepadDevice"/>/<see cref="Config.InputBinding"/> already
/// use) to the Steamworks <see cref="EXboxOrigin"/> vocabulary. No I/O, no Steamworks calls —
/// this is the table <c>Steam.SteamInputGlyphManager</c> uses to translate a raw SDL button
/// identifier into the argument <c>ISteamInput::GetActionOriginFromXboxOrigin</c> expects.
/// </summary>
internal static class XboxOriginMap
{
    internal static readonly IReadOnlyDictionary<string, EXboxOrigin> Map = new Dictionary<string, EXboxOrigin>
    {
        ["A"]             = EXboxOrigin.k_EXboxOrigin_A,
        ["B"]             = EXboxOrigin.k_EXboxOrigin_B,
        ["X"]             = EXboxOrigin.k_EXboxOrigin_X,
        ["Y"]             = EXboxOrigin.k_EXboxOrigin_Y,
        ["LeftShoulder"]  = EXboxOrigin.k_EXboxOrigin_LeftBumper,
        ["RightShoulder"] = EXboxOrigin.k_EXboxOrigin_RightBumper,
        ["Start"]         = EXboxOrigin.k_EXboxOrigin_Menu,
        ["Back"]          = EXboxOrigin.k_EXboxOrigin_View,
        ["LeftThumb"]     = EXboxOrigin.k_EXboxOrigin_LeftStick_Click,
        ["RightThumb"]    = EXboxOrigin.k_EXboxOrigin_RightStick_Click,
        ["DPadUp"]        = EXboxOrigin.k_EXboxOrigin_DPad_North,
        ["DPadDown"]      = EXboxOrigin.k_EXboxOrigin_DPad_South,
        ["DPadLeft"]      = EXboxOrigin.k_EXboxOrigin_DPad_West,
        ["DPadRight"]     = EXboxOrigin.k_EXboxOrigin_DPad_East,
        ["AnalogUp"]      = EXboxOrigin.k_EXboxOrigin_LeftStick_DPadNorth,
        ["AnalogDown"]    = EXboxOrigin.k_EXboxOrigin_LeftStick_DPadSouth,
        ["AnalogLeft"]    = EXboxOrigin.k_EXboxOrigin_LeftStick_DPadWest,
        ["AnalogRight"]   = EXboxOrigin.k_EXboxOrigin_LeftStick_DPadEast,
    };
}
