using System.Collections.Generic;
using System.Collections.Immutable;
using System.Windows.Forms;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active keyboard identifiers (Keys.ToString() names) to NES button names
/// via InputBinding.Key entries in config.InputMappings.
/// Uses Enum.TryParse to normalise config binding names before comparing with identifiers.
/// This handles aliases where the config stores one name (e.g. "Return") but
/// Keys.ToString() produces another (e.g. "Enter") for the same underlying value.
/// </summary>
internal sealed class KeyboardMapper : IInputMapper
{
    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        // Parse all identifiers to Keys values so comparison is by enum value, not string.
        // This handles aliases where ToString() produces a different spelling than what the
        // config stores (e.g. binding "Return" == identifier "Enter", binding "OemComma" == "Oemcomma").
        var pressedKeys = new HashSet<Keys>();
        foreach (var id in rawIdentifiers)
        {
            if (Enum.TryParse<Keys>(id, ignoreCase: true, out var k))
                pressedKeys.Add(k);
        }

        foreach (var (nesButton, binding) in config.InputMappings)
        {
            if (binding.Key is null) continue;
            if (!Enum.TryParse<Keys>(binding.Key, ignoreCase: true, out var key)) continue;
            if (pressedKeys.Contains(key))
                target.Add(nesButton);
        }
    }
}
