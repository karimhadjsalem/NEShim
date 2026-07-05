using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active XInput identifiers to NES button names.
/// Digital buttons are mapped via InputBinding.GamepadButton entries in config.InputMappings.
/// Synthetic analog identifiers ("AnalogUp" etc.) are always mapped to the standard
/// NES directional buttons regardless of config.
/// </summary>
internal sealed class XInputMapper : IInputMapper
{
    private static readonly IReadOnlyDictionary<string, string> AnalogToNesButton =
        new Dictionary<string, string>
        {
            ["AnalogUp"]    = "P1 Up",
            ["AnalogDown"]  = "P1 Down",
            ["AnalogLeft"]  = "P1 Left",
            ["AnalogRight"] = "P1 Right",
        };

    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        foreach (var (analogId, nesButton) in AnalogToNesButton)
        {
            if (rawIdentifiers.Contains(analogId))
                target.Add(nesButton);
        }

        foreach (var (nesButton, binding) in config.InputMappings)
        {
            if (binding.GamepadButton is not null
                && (binding.GamepadButton != "Start" || config.OverrideStartBindingProtection)
                && rawIdentifiers.Contains(binding.GamepadButton))
            {
                target.Add(nesButton);
            }
        }
    }
}
