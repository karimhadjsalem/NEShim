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

        // Steam Input: apply the fixed VDF action→NES button table.
        // No config lookup — the mapping is defined by the VDF file, not by the user.
        var steamActions = NEShim.Steam.SteamInputManager.GetActiveActions();
        foreach (var (action, nesButton) in NEShim.Steam.SteamInputManager.ActionToNesButton)
            if (steamActions.Contains(action))
                builder.Add(nesButton);

        // Keyboard + XInput: from config.InputMappings.
        var gamepad = XInputHelper.GetState(0);
        foreach (var (nesButton, binding) in config.InputMappings)
        {
            if (binding.Key is not null &&
                Enum.TryParse<Keys>(binding.Key, out var mappedKey) &&
                keys.Contains(mappedKey))
            {
                builder.Add(nesButton);
                continue;
            }

            if (gamepad.Connected &&
                binding.GamepadButton != "Start" &&
                XInputHelper.GetButton(in gamepad, binding.GamepadButton))
            {
                builder.Add(nesButton);
            }
        }

        // Analog stick → D-pad conversion (Steam handles this via action sets when active).
        // Cardinal mode (default): dominant-axis helpers ensure only one direction fires when
        // the stick is pushed diagonally, preventing accidental diagonal NES inputs.
        // Diagonal mode: raw per-axis threshold — both axes can register simultaneously for
        // games that use 8-directional movement.
        if (gamepad.Connected)
        {
            int dz = config.GamepadDeadzone;
            int lx = gamepad.ThumbLX;
            int ly = gamepad.ThumbLY;
            bool diagonal = config.AnalogStickMode.Equals("Diagonal", StringComparison.OrdinalIgnoreCase);

            if (diagonal)
            {
                if (ly >  dz) builder.Add("P1 Up");
                if (ly < -dz) builder.Add("P1 Down");
                if (lx < -dz) builder.Add("P1 Left");
                if (lx >  dz) builder.Add("P1 Right");
            }
            else
            {
                if (StickUp(lx, ly, dz))    builder.Add("P1 Up");
                if (StickDown(lx, ly, dz))  builder.Add("P1 Down");
                if (StickLeft(lx, ly, dz))  builder.Add("P1 Left");
                if (StickRight(lx, ly, dz)) builder.Add("P1 Right");
            }
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

        // Menu navigation is always 4-directional: apply dominant-axis suppression
        // unconditionally so a diagonal stick push only fires one direction.
        bool up      = pad.Connected && (pad.DPadUp    || StickUp(pad.ThumbLX,   pad.ThumbLY,   dz));
        bool down    = pad.Connected && (pad.DPadDown  || StickDown(pad.ThumbLX,  pad.ThumbLY,  dz));
        bool left    = pad.Connected && (pad.DPadLeft  || StickLeft(pad.ThumbLX,  pad.ThumbLY,  dz));
        bool right   = pad.Connected && (pad.DPadRight || StickRight(pad.ThumbLX, pad.ThumbLY,  dz));
        bool confirm = pad.Connected && pad.A;
        bool back    = pad.Connected && (pad.B || pad.Back);

        bool prevUp      = prev.Connected && (prev.DPadUp    || StickUp(prev.ThumbLX,   prev.ThumbLY,   dz));
        bool prevDown    = prev.Connected && (prev.DPadDown  || StickDown(prev.ThumbLX,  prev.ThumbLY,  dz));
        bool prevLeft    = prev.Connected && (prev.DPadLeft  || StickLeft(prev.ThumbLX,  prev.ThumbLY,  dz));
        bool prevRight   = prev.Connected && (prev.DPadRight || StickRight(prev.ThumbLX, prev.ThumbLY,  dz));
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

    /// <summary>
    /// Returns true if Escape was just pressed this frame (edge-triggered).
    /// Escape is a reserved system key that always opens/closes the menu regardless of config.
    /// </summary>
    public bool IsEscJustPressed()
    {
        bool currPressed;
        lock (_keyLock) currPressed = _pressedKeys.Contains(Keys.Escape);
        bool wasPressed = _prevHotkeyKeys.Contains(Keys.Escape);
        if (currPressed) _currHotkeyKeys.Add(Keys.Escape);
        return currPressed && !wasPressed;
    }

    /// <summary>
    /// Returns true if the gamepad Start button was just pressed (edge-triggered).
    /// Start is a reserved system button that always opens/closes the menu regardless of config.
    /// </summary>
    public bool IsGamepadStartJustPressed()
    {
        var curr = XInputHelper.GetState(0);
        return curr.Connected && curr.Start && !_prevHotkeyPad.Start;
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

    // Cardinal-mode helpers: each direction is only active when its axis is dominant.
    private static bool StickUp(int lx, int ly, int dz)    => ly >  dz && Math.Abs(ly) >= Math.Abs(lx);
    private static bool StickDown(int lx, int ly, int dz)  => ly < -dz && Math.Abs(ly) >= Math.Abs(lx);
    private static bool StickLeft(int lx, int ly, int dz)  => lx < -dz && Math.Abs(lx) >  Math.Abs(ly);
    private static bool StickRight(int lx, int ly, int dz) => lx >  dz && Math.Abs(lx) >  Math.Abs(ly);

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
