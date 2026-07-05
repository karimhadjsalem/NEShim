using System.Collections.Generic;
using System.Windows.Forms;
using NEShim.Config;

namespace NEShim.Input.Sources;

/// <summary>
/// Captures keyboard state via WM_KEYDOWN/WM_KEYUP events forwarded from MainForm.
/// Returns active key names as strings matching the Keys.ToString() convention
/// used in InputBinding.Key (e.g. Keys.W → "W", Keys.OemPeriod → "OemPeriod").
/// Thread-safe: OnKeyDown/OnKeyUp are called on the UI thread; GetActiveIdentifiers
/// and hotkey helpers are called on the emulation thread.
/// </summary>
internal sealed class KeyboardInputSource : IInputSource
{
    private readonly HashSet<Keys> _pressedKeys = new();
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

    public void OnKeyDown(Keys key)
    {
        lock (_keyLock) _pressedKeys.Add(key);
    }

    public void OnKeyUp(Keys key)
    {
        lock (_keyLock) _pressedKeys.Remove(key);
    }

    internal bool IsKeyPressed(Keys key)
    {
        lock (_keyLock) return _pressedKeys.Contains(key);
    }

    internal HashSet<Keys> GetPressedKeysCopy()
    {
        lock (_keyLock) return new HashSet<Keys>(_pressedKeys);
    }
}
