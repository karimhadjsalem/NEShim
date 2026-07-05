namespace NEShim.Input;

/// <summary>
/// Strategy interface for detecting any button press on a controller source.
/// Used for coarse "did the user press anything?" checks (logo skip, disconnect dismiss)
/// without coupling the call site to XInput or Steam specifics.
/// Implemented by <see cref="Sources.XInputSource"/> and <see cref="Sources.SteamInputSource"/>.
/// <see cref="InputManager.PollAnyControllerButton"/> OR-unions all implementations.
/// </summary>
internal interface IAnyButtonSource
{
    /// <summary>
    /// Returns true on the first frame any button or axis crosses its active threshold.
    /// Maintains its own edge state independently of gameplay, nav, and binding paths.
    /// </summary>
    bool AnyJustPressed();
}
