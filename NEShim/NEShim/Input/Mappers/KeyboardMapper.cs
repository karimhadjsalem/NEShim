using System.Collections.Generic;
using System.Collections.Immutable;
using SDL3;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active keyboard identifiers (SDL.Keycode.ToString() names) to NES button names
/// via InputBinding.Key entries in config.InputMappings.
/// Uses Enum.TryParse to normalise config binding names before comparing with identifiers.
/// </summary>
internal sealed class KeyboardMapper : IInputMapper
{
    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        var pressedKeys = new HashSet<SDL.Keycode>();
        foreach (var id in rawIdentifiers)
        {
            if (Enum.TryParse<SDL.Keycode>(id, ignoreCase: true, out var k))
                pressedKeys.Add(k);
        }

        foreach (var (nesButton, binding) in config.InputMappings)
        {
            if (binding.Key is null) continue;
            if (!Enum.TryParse<SDL.Keycode>(binding.Key, ignoreCase: true, out var key)) continue;
            if (pressedKeys.Contains(key))
                target.Add(nesButton);
        }
    }
}
