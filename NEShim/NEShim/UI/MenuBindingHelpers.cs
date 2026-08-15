using System.Collections.Generic;
using NEShim.Config;
using NEShim.Localization;

namespace NEShim.UI;

/// <summary>
/// Static helpers shared by <see cref="InGameMenu"/> and <see cref="MainMenuScreen"/>
/// for building binding-action tables and applying key/button assignments to config.
/// </summary>
internal static class MenuBindingHelpers
{
    private static readonly HashSet<string> AnalogIdentifiers =
        new() { "AnalogUp", "AnalogDown", "AnalogLeft", "AnalogRight" };

    /// <summary>
    /// Returns the standard NES-button binding action table, localised via
    /// <paramref name="localization"/>. The last entry has an empty ConfigKey and
    /// represents the Back row.
    /// </summary>
    public static (string Label, string ConfigKey)[] BuildBindingActions(LocalizationData localization) =>
        new (string, string)[]
        {
            (localization.BindUp,     "P1 Up"),
            (localization.BindDown,   "P1 Down"),
            (localization.BindLeft,   "P1 Left"),
            (localization.BindRight,  "P1 Right"),
            (localization.BindA,      "P1 A"),
            (localization.BindB,      "P1 B"),
            (localization.BindStart,  "P1 Start"),
            (localization.BindSelect, "P1 Select"),
            (localization.Back,       ""),
        };

    /// <summary>
    /// Returns the gamepad binding table. When <c>OverrideStartBindingProtection</c> is
    /// enabled the table appends an OpenMenu row before the final Back row.
    /// </summary>
    public static (string Label, string ConfigKey)[] BuildGamepadBindingActions(
        LocalizationData localization,
        AppConfig config,
        (string Label, string ConfigKey)[] bindingActions)
    {
        return config.OverrideStartBindingProtection
            ? bindingActions[..^1]
                .Append((localization.BindOpenMenu, "OpenMenu"))
                .Append((localization.Back, ""))
                .ToArray()
            : bindingActions;
    }

    /// <summary>
    /// Assigns <paramref name="keyName"/> to <paramref name="action"/> in
    /// <paramref name="config"/> and clears the same key from any other action
    /// to prevent duplicate keyboard bindings.
    /// </summary>
    public static void SetBinding(AppConfig config, string action, string keyName)
    {
        foreach (var kvp in config.InputMappings)
        {
            if (kvp.Key != action && kvp.Value.Key == keyName)
                kvp.Value.Key = null;
        }

        if (config.InputMappings.TryGetValue(action, out var binding))
            binding.Key = keyName;
        else
            config.InputMappings[action] = new InputBinding(keyName, null);
    }

    /// <summary>
    /// Assigns <paramref name="buttonName"/> to <paramref name="action"/> in
    /// <paramref name="config"/> and clears the same button from any other action
    /// to prevent duplicate gamepad bindings.
    /// </summary>
    public static void SetGamepadBinding(AppConfig config, string action, string buttonName)
    {
        foreach (var kvp in config.InputMappings)
        {
            if (kvp.Key != action)
            {
                if (kvp.Value.GamepadButton  == buttonName) kvp.Value.GamepadButton  = null;
                if (kvp.Value.GamepadButton2 == buttonName) kvp.Value.GamepadButton2 = null;
            }
        }

        if (config.InputMappings.TryGetValue(action, out var binding))
        {
            binding.GamepadButton = buttonName;
            // If the user explicitly binds an analog identifier as the primary button,
            // clear the secondary slot on the same action. Without this, an action whose
            // default GamepadButton2 was "AnalogDown" would fire for both AnalogUp
            // (GamepadButton) and AnalogDown (GamepadButton2) simultaneously.
            if (AnalogIdentifiers.Contains(buttonName))
                binding.GamepadButton2 = null;
        }
        else
        {
            config.InputMappings[action] = new InputBinding(null, buttonName);
        }
    }

    /// <summary>
    /// Localizes a raw gamepad identifier for the Gamepad Bindings value column. Forwards to
    /// <see cref="NEShim.Input.GamepadButtonLocalizer"/>, which lives in the Input layer so
    /// <see cref="NEShim.Input.Sources.TextGlyphSource"/> can share the same logic without
    /// depending back into UI.
    /// </summary>
    public static string LocalizeGamepadButton(string? identifier, LocalizationData localization)
        => NEShim.Input.GamepadButtonLocalizer.Localize(identifier, localization);
}
