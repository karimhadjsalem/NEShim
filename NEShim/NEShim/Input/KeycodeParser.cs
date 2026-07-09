using System.Collections.Generic;
using SDL3;

namespace NEShim.Input;

/// <summary>
/// Adapts config key name strings to SDL.Keycode values.
/// Handles legacy Windows Forms names that differ from SDL3 Keycode names so
/// existing configs continue to work without the user remapping their controls.
/// </summary>
internal static class KeycodeParser
{
    private static readonly Dictionary<string, string> _legacyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Enter", "Return" },
        };

    public static bool TryParse(string name, out SDL.Keycode key)
    {
        if (_legacyNames.TryGetValue(name, out var canonical))
            name = canonical;
        return Enum.TryParse(name, ignoreCase: true, out key);
    }
}
