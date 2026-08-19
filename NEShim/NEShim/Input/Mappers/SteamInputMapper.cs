using System.Collections.Generic;
using System.Collections.Immutable;
using NEShim.Config;

namespace NEShim.Input.Mappers;

/// <summary>
/// Maps active Steam action names to NES button names via <see cref="Steam.SteamInputManager.NesButtonFor"/>
/// (action names fixed by the VDF file, not by config; scoped to one player by translating to
/// that player's "P{player} …" config key).
/// </summary>
internal sealed class SteamInputMapper : IInputMapper
{
    private readonly int _player;

    internal SteamInputMapper(int player = 1) => _player = player;

    public void Map(IReadOnlySet<string> rawIdentifiers, AppConfig config,
                    ImmutableHashSet<string>.Builder target)
    {
        foreach (var actionName in rawIdentifiers)
        {
            var nesButton = Steam.SteamInputManager.NesButtonFor(actionName, _player);
            if (nesButton is not null)
                target.Add(nesButton);
        }
    }
}
