using System.Collections.Generic;
using SDL3;
using NEShim.Config;

namespace NEShim.Input.Sources;

/// <summary>
/// Captures keyboard state via SDL KeyDown/KeyUp events forwarded from SDL3WindowHost.
/// Returns active key names as strings matching SDL.Keycode.ToString() convention
/// used in InputBinding.Key (e.g. SDL.Keycode.W → "W", SDL.Keycode.Period → "Period").
/// Thread-safe: OnKeyDown/OnKeyUp are called on the main thread; GetActiveIdentifiers
/// and hotkey helpers are called on the emulation thread.
/// </summary>
internal sealed class KeyboardInputSource : IInputSource
{
    private readonly HashSet<SDL.Keycode> _pressedKeys = new();
    private readonly object _keyLock = new();

    public bool IsAvailable => true;

    public IReadOnlySet<string> GetActiveIdentifiers(AppConfig config)
    {
        lock (_keyLock)
        {
            var result = new HashSet<string>(_pressedKeys.Count);
            foreach (var key in _pressedKeys)
                result.Add(key.ToString());
            return result;
        }
    }

    public void OnKeyDown(SDL.Keycode key)
    {
        lock (_keyLock) _pressedKeys.Add(key);
    }

    public void OnKeyUp(SDL.Keycode key)
    {
        lock (_keyLock) _pressedKeys.Remove(key);
    }

    internal bool IsKeyPressed(SDL.Keycode key)
    {
        lock (_keyLock) return _pressedKeys.Contains(key);
    }

    internal HashSet<SDL.Keycode> GetPressedKeysCopy()
    {
        lock (_keyLock) return new HashSet<SDL.Keycode>(_pressedKeys);
    }
}
