namespace NEShim.Input;

/// <summary>
/// Strategy interface for gamepad button detection during the rebinding UI.
/// Implemented by <see cref="Sources.XInputSource"/>.
/// <see cref="InputManager.PollAnyGamepadButtonPressed"/> delegates to the first implementation found.
/// </summary>
internal interface IBindingSource
{
    /// <summary>
    /// Returns the name of a newly-pressed button/axis, or null if nothing changed.
    /// Maintains its own edge state independently of gameplay and menu-nav paths.
    /// </summary>
    string? PollAnyButtonPressed();

    /// <summary>
    /// Seeds the binding edge-state with the current hardware state so buttons held
    /// when rebinding mode opens are not detected as a new binding press.
    /// </summary>
    void FlushEdges();
}
