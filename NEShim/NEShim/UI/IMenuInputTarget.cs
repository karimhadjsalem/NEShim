using NEShim.Input;

namespace NEShim.UI;

/// <summary>
/// Receives gamepad menu navigation dispatched from the emulation thread.
/// Implemented by NEShimApp so EmulationThread does not depend on the UI/menu layer directly.
/// </summary>
internal interface IMenuInputTarget
{
    /// <summary>True when the active menu is waiting for a gamepad button press (rebind mode).</summary>
    bool IsWaitingForGamepadButton { get; }

    /// <summary>
    /// The 1-based player whose gamepad the rebind capture should read while
    /// <see cref="IsWaitingForGamepadButton"/> is true — parsed from the active menu's
    /// GamepadRebindingAction config key (e.g. "P2 Up" → 2), defaulting to player 1. Rebinding a
    /// non-player-1 screen must read that player's own physical device, not player 1's.
    /// </summary>
    int WaitingForGamepadButtonPlayer { get; }

    /// <summary>Routes a directional / confirm / back input to the active menu.</summary>
    void HandleGamepadNav(MenuNavInput nav);

    /// <summary>Routes a button-press to the active menu's rebind handler.</summary>
    void HandleGamepadButtonPress(string buttonName);
}
