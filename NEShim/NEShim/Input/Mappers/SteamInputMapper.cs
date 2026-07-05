using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active Steam action names to NES button names via the fixed
/// SteamInputManager.ActionToNesButton table (defined by the VDF file, not by config).
/// </summary>
internal sealed class SteamInputMapper : IInputMapper
{
    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        foreach (var (actionName, nesButton) in Steam.SteamInputManager.ActionToNesButton)
        {
            if (rawIdentifiers.Contains(actionName))
                target.Add(nesButton);
        }
    }
}
