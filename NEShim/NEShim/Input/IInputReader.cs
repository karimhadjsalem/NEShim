using System.Windows.Forms;
using NEShim.Config;

namespace NEShim.Input;

internal interface IInputReader
{
    // ── Frame polling ──────────────────────────────────────────────────────────

    InputSnapshot PollSnapshot(AppConfig config);
    MenuNavInput  PollMenuNav(AppConfig config);

    /// <summary>
    /// Detects edge transitions for all configured hotkeys and fires the
    /// corresponding events (<see cref="HotkeyFired"/>, <see cref="MenuToggleRequested"/>).
    /// Call once per frame on the emulation thread, after <see cref="PollSnapshot"/>.
    /// </summary>
    void AdvanceHotkeyState(AppConfig config);

    /// <summary>Forwards a WM_KEYDOWN event from the UI thread to the input source.</summary>
    void OnKeyDown(Keys key);

    /// <summary>Forwards a WM_KEYUP event from the UI thread to the input source.</summary>
    void OnKeyUp(Keys key);

    // ── Binding UI ─────────────────────────────────────────────────────────────

    bool    IsAnyInputJustPressed();
    string? PollAnyGamepadButtonPressed();

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
