using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active SDL3 gamepad identifiers to NES button names using config.InputMappings only.
/// Each binding entry has a primary slot (GamepadButton — D-pad, face buttons, etc.) and a
/// secondary slot (GamepadButton2 — analog-stick identifiers set in the default config) so
/// both D-pad and analog stick work simultaneously without any hardcoded mapping in this class.
/// </summary>
internal sealed class SDL3GamepadMapper : IInputMapper
{
    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        foreach (var (nesButton, binding) in config.InputMappings)
        {
            if (binding.GamepadButton is not null
                && (binding.GamepadButton != "Start" || config.OverrideStartBindingProtection)
                && rawIdentifiers.Contains(binding.GamepadButton))
            {
                target.Add(nesButton);
            }

            if (binding.GamepadButton2 is not null && rawIdentifiers.Contains(binding.GamepadButton2))
                target.Add(nesButton);
        }
    }
}
