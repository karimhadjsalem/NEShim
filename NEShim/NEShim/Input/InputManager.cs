using System.Collections.Generic;
using System.Collections.Immutable;
using System.Windows.Forms;
using NEShim.Config;

namespace NEShim.Input;

/// <summary>
/// Maintains keyboard state (updated on UI thread) and polls XInput (on emulation thread).
/// PollSnapshot() combines both into an InputSnapshot.
/// </summary>
internal sealed class InputManager
{
    private readonly HashSet<Keys> _pressedKeys = new();
    private readonly object _keyLock = new();

    // Edge detection for keyboard hotkeys — tracks previous frame's key state
    private readonly HashSet<Keys> _prevHotkeyKeys = new();
    private readonly HashSet<Keys> _currHotkeyKeys = new();

    // Edge detection for gamepad hotkeys — updated once per frame in AdvanceHotkeyState
    private XInputHelper.GamepadState _prevHotkeyPad;

    // Edge detection for gamepad menu navigation — updated each PollMenuNav call
    private XInputHelper.GamepadState _prevMenuPad;

    public void OnKeyDown(Keys key)
    {
        lock (_keyLock) _pressedKeys.Add(key);
    }

    public void OnKeyUp(Keys key)
    {
        lock (_keyLock) _pressedKeys.Remove(key);
    }

    /// <summary>
    /// Builds a snapshot of currently pressed NES buttons from keyboard + gamepad.
    /// Call once per frame from the emulation thread.
    /// </summary>
    public InputSnapshot PollSnapshot(AppConfig config)
    {
        HashSet<Keys> keys;
        lock (_keyLock) keys = new HashSet<Keys>(_pressedKeys);

        var builder = ImmutableHashSet.CreateBuilder<string>();

        // Steam Input takes precedence: query first so HasConnectedController is current.
        // When a Steam controller is active its action-set mapping is authoritative;
        // XInput is skipped entirely for that controller to prevent double-mapping conflicts.
        var steamButtons = NEShim.Steam.SteamInputManager.GetActiveGameplayButtons();
        foreach (var btn in steamButtons) builder.Add(btn);

        bool useXInput = !NEShim.Steam.SteamInputManager.HasConnectedController;
        var gamepad = useXInput ? XInputHelper.GetState(0) : default;

        foreach (var (nesButton, binding) in config.InputMappings)
        {
            bool pressed = false;

            if (binding.Key is not null &&
                Enum.TryParse<Keys>(binding.Key, out var mappedKey) &&
                keys.Contains(mappedKey))
            {
                pressed = true;
            }

            if (!pressed && useXInput && gamepad.Connected &&
                XInputHelper.GetButton(in gamepad, binding.GamepadButton))
            {
                pressed = true;
            }

            if (pressed) builder.Add(nesButton);
        }

        // Analog stick → D-pad conversion (XInput only; Steam handles this via action sets)
        if (useXInput && gamepad.Connected)
        {
            int deadzone = config.GamepadDeadzone;
            if (gamepad.ThumbLY >  deadzone) builder.Add("P1 Up");
            if (gamepad.ThumbLY < -deadzone) builder.Add("P1 Down");
            if (gamepad.ThumbLX < -deadzone) builder.Add("P1 Left");
            if (gamepad.ThumbLX >  deadzone) builder.Add("P1 Right");
        }

        return new InputSnapshot(builder.ToImmutable());
    }

    /// <summary>
    /// Polls gamepad state and returns edge-triggered menu navigation.
    /// Combines XInput and Steam Input; call once per menu poll interval (≈16ms while paused).
    /// </summary>
    public MenuNavInput PollMenuNav(AppConfig config)
    {
        var pad = XInputHelper.GetState(0);
        var prev = _prevMenuPad;

        if (pad.Connected)
            _prevMenuPad = pad;
        else
            _prevMenuPad = default;

        int dz = config.GamepadDeadzone;

        bool up      = pad.Connected && (pad.DPadUp    || pad.ThumbLY >  dz);
        bool down    = pad.Connected && (pad.DPadDown  || pad.ThumbLY < -dz);
        bool left    = pad.Connected && (pad.DPadLeft  || pad.ThumbLX < -dz);
        bool right   = pad.Connected && (pad.DPadRight || pad.ThumbLX >  dz);
        bool confirm = pad.Connected && pad.A;
        bool back    = pad.Connected && (pad.B || pad.Back);

        bool prevUp      = prev.Connected && (prev.DPadUp    || prev.ThumbLY >  dz);
        bool prevDown    = prev.Connected && (prev.DPadDown  || prev.ThumbLY < -dz);
        bool prevLeft    = prev.Connected && (prev.DPadLeft  || prev.ThumbLX < -dz);
        bool prevRight   = prev.Connected && (prev.DPadRight || prev.ThumbLX >  dz);
        bool prevConfirm = prev.Connected && prev.A;
        bool prevBack    = prev.Connected && (prev.B || prev.Back);

        // OR in Steam Input menu nav (its own edge detection runs inside SteamInputManager)
        var steam = NEShim.Steam.SteamInputManager.GetMenuNav();

        return new MenuNavInput
        {
            Up      = (up      && !prevUp)      || steam.Up,
            Down    = (down    && !prevDown)    || steam.Down,
            Left    = (left    && !prevLeft)    || steam.Left,
            Right   = (right   && !prevRight)   || steam.Right,
            Confirm = (confirm && !prevConfirm) || steam.Confirm,
            Back    = (back    && !prevBack)    || steam.Back,
        };
    }

    /// <summary>
    /// Returns true if the named gamepad hotkey was just pressed this frame (edge-triggered).
    /// Uses GamepadHotkeyMappings from config. Call after AdvanceHotkeyState was called
    /// at the end of the previous frame.
    /// </summary>
    /// <summary>
    /// Returns the name of the first gamepad button that was just pressed this interval,
    /// or null if no new button press was detected.  Uses the same _prevMenuPad state as
    /// PollMenuNav — call one or the other per interval, not both.
    /// </summary>
    public string? PollAnyGamepadButtonPressed()
    {
        var pad  = XInputHelper.GetState(0);
        var prev = _prevMenuPad;

        if (!pad.Connected) { _prevMenuPad = default; return null; }
        _prevMenuPad = pad;

        if (pad.A             && !prev.A)             return "A";
        if (pad.B             && !prev.B)             return "B";
        if (pad.X             && !prev.X)             return "X";
        if (pad.Y             && !prev.Y)             return "Y";
        if (pad.Start         && !prev.Start)         return "Start";
        if (pad.Back          && !prev.Back)          return "Back";
        if (pad.LeftShoulder  && !prev.LeftShoulder)  return "LeftShoulder";
        if (pad.RightShoulder && !prev.RightShoulder) return "RightShoulder";
        if (pad.LeftThumb     && !prev.LeftThumb)     return "LeftThumb";
        if (pad.RightThumb    && !prev.RightThumb)    return "RightThumb";
        if (pad.DPadUp        && !prev.DPadUp)        return "DPadUp";
        if (pad.DPadDown      && !prev.DPadDown)      return "DPadDown";
        if (pad.DPadLeft      && !prev.DPadLeft)      return "DPadLeft";
        if (pad.DPadRight     && !prev.DPadRight)     return "DPadRight";

        return null;
    }

    public bool IsGamepadHotkeyJustPressed(string action, AppConfig config)
    {
        if (!config.GamepadHotkeyMappings.TryGetValue(action, out var buttonName)) return false;
        var curr = XInputHelper.GetState(0);
        return XInputHelper.GetButton(in curr, buttonName)
            && !XInputHelper.GetButton(in _prevHotkeyPad, buttonName);
    }

    /// <summary>
    /// Returns whether a hotkey was just pressed this frame (edge triggered).
    /// Maps the config key name to a Keys value and checks for new press.
    /// </summary>
    public bool IsHotkeyJustPressed(string action, AppConfig config)
    {
        if (!config.HotkeyMappings.TryGetValue(action, out var keyName)) return false;
        if (!Enum.TryParse<Keys>(keyName, out var key)) return false;

        bool currPressed;
        lock (_keyLock) currPressed = _pressedKeys.Contains(key);

        bool wasPressed = _prevHotkeyKeys.Contains(key);
        if (currPressed) _currHotkeyKeys.Add(key);

        return currPressed && !wasPressed;
    }

    /// <summary>Called at the end of each frame to advance edge-detection state.</summary>
    public void AdvanceHotkeyState()
    {
        _prevHotkeyKeys.Clear();
        foreach (var k in _currHotkeyKeys) _prevHotkeyKeys.Add(k);
        _currHotkeyKeys.Clear();

        // Populate curr from current physical state
        lock (_keyLock)
        {
            foreach (var k in _pressedKeys) _currHotkeyKeys.Add(k);
        }

        // Advance gamepad hotkey edge-detection
        _prevHotkeyPad = XInputHelper.GetState(0);
    }
}
