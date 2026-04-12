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

    // Edge detection for hotkeys — tracks previous frame's key state
    private readonly HashSet<Keys> _prevHotkeyKeys = new();
    private readonly HashSet<Keys> _currHotkeyKeys = new();

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

        var gamepad = XInputHelper.GetState(0);
        var builder = ImmutableHashSet.CreateBuilder<string>();

        foreach (var (nesButton, binding) in config.InputMappings)
        {
            bool pressed = false;

            if (binding.Key is not null &&
                Enum.TryParse<Keys>(binding.Key, out var mappedKey) &&
                keys.Contains(mappedKey))
            {
                pressed = true;
            }

            if (!pressed && gamepad.Connected &&
                XInputHelper.GetButton(in gamepad, binding.GamepadButton))
            {
                pressed = true;
            }

            if (pressed) builder.Add(nesButton);
        }

        // Analog stick → D-pad conversion
        if (gamepad.Connected)
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
    }
}
