using NEShim.Input;

namespace NEShim.UI;

/// <summary>
/// Receives gamepad menu navigation dispatched from the emulation thread.
/// Implemented by MainForm so EmulationThread does not depend on GamePanel.
/// </summary>
internal interface IMenuInputTarget
{
    /// <summary>True when the active menu is waiting for a gamepad button press (rebind mode).</summary>
    bool IsWaitingForGamepadButton { get; }

    /// <summary>Routes a directional / confirm / back input to the active menu.</summary>
    void HandleGamepadNav(MenuNavInput nav);

    /// <summary>Routes a button-press to the active menu's rebind handler.</summary>
    void HandleGamepadButtonPress(string buttonName);
}
