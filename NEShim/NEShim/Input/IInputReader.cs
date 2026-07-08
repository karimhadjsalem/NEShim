using SDL3;
using NEShim.Config;

namespace NEShim.Input;

internal interface IInputReader
{
    // ── Frame polling ──────────────────────────────────────────────────────────

    InputSnapshot PollSnapshot(AppConfig config);
    MenuNavInput  PollMenuNav(AppConfig config);

    /// <summary>
    /// Returns raw held state for left/right directional input — not edge-triggered.
    /// Used to drive continuous slider movement while a direction is held.
    /// </summary>
    (bool Left, bool Right) GetHeldSliderDir(AppConfig config);

    /// <summary>
    /// Detects edge transitions for all configured hotkeys and fires the
    /// corresponding events (<see cref="HotkeyFired"/>, <see cref="MenuToggleRequested"/>).
    /// Call once per frame on the emulation thread, after <see cref="PollSnapshot"/>.
    /// </summary>
    void AdvanceHotkeyState(AppConfig config);

    /// <summary>Forwards a SDL KeyDown event from the main thread to the input source.</summary>
    void OnKeyDown(SDL.Keycode key);

    /// <summary>Forwards a SDL KeyUp event from the main thread to the input source.</summary>
    void OnKeyUp(SDL.Keycode key);

    // ── Binding UI ─────────────────────────────────────────────────────────────

    bool    IsAnyInputJustPressed();
    string? PollAnyGamepadButtonPressed();

    /// <summary>
    /// Returns true on the first frame any controller button or axis is pressed.
    /// Composes all <see cref="IAnyButtonSource"/> implementations (XInput, Steam).
    /// Suitable for coarse "did the user touch anything?" checks (logo skip, disconnect dismiss)
    /// without coupling the call site to a specific input backend.
    /// </summary>
    bool PollAnyControllerButton();

    /// <summary>
    /// Seeds the gamepad binding edge-state with the current hardware state so buttons
    /// held when rebinding mode opens are not immediately detected as a new binding press.
    /// Call once on the frame rebinding mode is entered.
    /// </summary>
    void FlushBindingEdges();

    // ── IoC events — fired on the emulation thread ─────────────────────────────
    // Handlers that touch WinForms/UI state must marshal via BeginInvoke.

    /// <summary>
    /// Fired when a user-configured hotkey is edge-triggered.
    /// Payload is the action name (e.g. "SaveActiveSlot", "SelectSlot1").
    /// </summary>
    event Action<string>? HotkeyFired;

    /// <summary>
    /// Fired when Esc, the configured gamepad OpenMenu hotkey, or the reserved
    /// Start button (when not overridden) is edge-triggered.
    /// </summary>
    event Action? MenuToggleRequested;

    /// <summary>
    /// Fired once when a previously-connected controller is no longer detected.
    /// </summary>
    event Action? GamepadDisconnected;
}
