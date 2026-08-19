using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active SDL3 gamepad identifiers to NES button names using config.InputMappings only.
/// Each binding entry has a primary slot (GamepadButton — D-pad, face buttons, etc.) and a
/// secondary slot (GamepadButton2 — analog-stick identifiers set in the default config) so
/// both D-pad and analog stick work simultaneously without any hardcoded mapping in this class.
/// <para>
/// Scoped to one player's config-key prefix (<c>"P{player} "</c>). This is required, not
/// cosmetic: different physical gamepads report the same raw identifier strings ("A", "DPadUp",
/// ...), so without this filter one player's physical button press would also satisfy every
/// other player's identically-named binding.
/// </para>
/// </summary>
internal sealed class SDL3GamepadMapper : IInputMapper
{
    private readonly string _playerPrefix;

    internal SDL3GamepadMapper(int player = 1) => _playerPrefix = $"P{player} ";

    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        foreach (var (nesButton, binding) in config.InputMappings)
        {
            if (!nesButton.StartsWith(_playerPrefix, StringComparison.Ordinal)) continue;

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
