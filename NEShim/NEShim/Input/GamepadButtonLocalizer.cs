using NEShim.Localization;

namespace NEShim.Input;

/// <summary>
/// Localizes a raw gamepad identifier (e.g. "DPadUp", "LeftShoulder", "A") into display text.
/// Lives in the Input layer (rather than <c>UI.MenuBindingHelpers</c>, which forwards to this)
/// so <see cref="Sources.TextGlyphSource"/> can use it without an Input-to-UI layering inversion.
/// See <see cref="LocalizationData"/>'s GamepadButton*/GamepadDpad*/GamepadAnalog* doc comment
/// for the loanword/native-term rationale.
/// </summary>
internal static class GamepadButtonLocalizer
{
    /// <summary>
    /// Unrecognized identifiers pass through unchanged as a defensive fallback rather than
    /// disappearing, so a future new identifier doesn't silently show blank.
    /// </summary>
    public static string Localize(string? identifier, LocalizationData localization) => identifier switch
    {
        null            => localization.BindNone,
        "A"             => localization.BindA,
        "B"             => localization.BindB,
        "X"             => "X",
        "Y"             => "Y",
        "Start"         => localization.BindStart,
        "Back"          => localization.BindSelect,
        "LeftShoulder"  => localization.GamepadButtonLeftShoulder,
        "RightShoulder" => localization.GamepadButtonRightShoulder,
        "LeftThumb"     => localization.GamepadButtonLeftThumb,
        "RightThumb"    => localization.GamepadButtonRightThumb,
        "DPadUp"        => localization.GamepadDpadUp,
        "DPadDown"      => localization.GamepadDpadDown,
        "DPadLeft"      => localization.GamepadDpadLeft,
        "DPadRight"     => localization.GamepadDpadRight,
        "AnalogUp"      => localization.GamepadAnalogUp,
        "AnalogDown"    => localization.GamepadAnalogDown,
        "AnalogLeft"    => localization.GamepadAnalogLeft,
        "AnalogRight"   => localization.GamepadAnalogRight,
        _               => identifier,
    };
}
