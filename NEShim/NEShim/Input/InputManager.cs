using System.Collections.Generic;
using System.Collections.Immutable;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Input.Mappers;
using NEShim.Input.Sources;

namespace NEShim.Input;

/// <summary>
/// Coordinates input sources and mappers to produce per-frame InputSnapshot values.
/// Fires IoC events for hotkey edges, menu-toggle, and controller disconnect so callers
/// (EmulationThread, MainForm) register handlers once rather than polling each frame.
/// </summary>
internal sealed class InputManager : IInputReader
{
    private readonly KeyboardInputSource _keyboardSource;
    private readonly IInputSource        _xInputSource;
    private readonly IInputSource        _steamSource;
    private readonly IInputMapper        _keyboardMapper;
    private readonly IInputMapper        _xInputMapper;
    private readonly IInputMapper        _steamMapper;

    // Edge detection for hotkeys
    private readonly HashSet<Keys> _prevHotkeyKeys = new();
    private XInputHelper.GamepadState _prevHotkeyPad;

    // Controller disconnect tracking
    private bool _wasControllerConnected;
    private bool _prevLoggedXInput;
    private bool _prevLoggedSteam;

    // ── IoC events ─────────────────────────────────────────────────────────────

    public event Action<string>? HotkeyFired;
    public event Action?         MenuToggleRequested;
    public event Action?         GamepadDisconnected;

    // ── Constructors ───────────────────────────────────────────────────────────

    internal InputManager()
        : this(new KeyboardInputSource(),
               new XInputSource(),
               new SteamInputSource(),
               new KeyboardMapper(),
               new XInputMapper(),
               new SteamInputMapper())
    { }

    internal InputManager(
        KeyboardInputSource keyboardSource,
        IInputSource        xInputSource,
        IInputSource        steamSource,
        IInputMapper        keyboardMapper,
        IInputMapper        xInputMapper,
        IInputMapper        steamMapper)
    {
        _keyboardSource = keyboardSource;
        _xInputSource   = xInputSource;
        _steamSource    = steamSource;
        _keyboardMapper = keyboardMapper;
        _xInputMapper   = xInputMapper;
        _steamMapper    = steamMapper;
    }

    // ── IInputReader: keyboard forwarding ──────────────────────────────────────

    public void OnKeyDown(Keys key) => _keyboardSource.OnKeyDown(key);
    public void OnKeyUp(Keys key)   => _keyboardSource.OnKeyUp(key);

    // ── IInputReader: frame polling ────────────────────────────────────────────

    public InputSnapshot PollSnapshot(AppConfig config)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>();

        var steamIds = _steamSource.GetActiveIdentifiers(config);
        if (_steamSource.IsAvailable)
            _steamMapper.Map(steamIds, config, builder);

        var xIds = _xInputSource.GetActiveIdentifiers(config);
        if (_xInputSource.IsAvailable)
            _xInputMapper.Map(xIds, config, builder);

        _keyboardMapper.Map(_keyboardSource.GetActiveIdentifiers(config), config, builder);

        bool controllerNow = _xInputSource.IsAvailable || _steamSource.IsAvailable;
        if (_wasControllerConnected && !controllerNow)
            GamepadDisconnected?.Invoke();
        _wasControllerConnected = controllerNow;

        bool xInputNow = _xInputSource.IsAvailable;
        bool steamNow  = _steamSource.IsAvailable;
        if (xInputNow != _prevLoggedXInput || steamNow != _prevLoggedSteam)
        {
            Logger.Log($"[Input] Source: XInput={xInputNow}, Steam={steamNow} — deadzone={config.GamepadDeadzone}, mode={config.AnalogStickMode}");
            _prevLoggedXInput = xInputNow;
            _prevLoggedSteam  = steamNow;
        }

        return new InputSnapshot(builder.ToImmutable());
    }

    public MenuNavInput PollMenuNav(AppConfig config)
    {
        var result = default(MenuNavInput);

        if (_xInputSource is IMenuNavSource xNav)
            result = MenuNavInput.Union(result, xNav.GetMenuNav(config));

        if (_steamSource is IMenuNavSource steamNav)
            result = MenuNavInput.Union(result, steamNav.GetMenuNav(config));

        return result;
    }

    /// <summary>
    /// Detects edge transitions for all configured hotkeys and fires events.
    /// Call once per frame on the emulation thread, after PollSnapshot.
    /// </summary>
    public void AdvanceHotkeyState(AppConfig config)
    {
        var curr = _keyboardSource.GetPressedKeysCopy();
        var pad  = XInputHelper.GetState(0);

        // Menu toggle — fire at most once even if multiple triggers are active
        bool menuToggle =
            (curr.Contains(Keys.Escape) && !_prevHotkeyKeys.Contains(Keys.Escape))
            || (!config.OverrideStartBindingProtection
                && pad.Connected && pad.Start && !_prevHotkeyPad.Start)
            || (config.GamepadHotkeyMappings.TryGetValue("OpenMenu", out var openBtn)
                && pad.Connected
                && XInputHelper.GetButton(in pad, openBtn)
                && !XInputHelper.GetButton(in _prevHotkeyPad, openBtn));

        if (menuToggle)
            MenuToggleRequested?.Invoke();

        // Keyboard hotkeys
        foreach (var (action, keyName) in config.HotkeyMappings)
        {
            if (!Enum.TryParse<Keys>(keyName, out var key)) continue;
            if (curr.Contains(key) && !_prevHotkeyKeys.Contains(key))
                HotkeyFired?.Invoke(action);
        }

        // Non-OpenMenu gamepad hotkeys
        foreach (var (action, buttonName) in config.GamepadHotkeyMappings)
        {
            if (action == "OpenMenu") continue;
            if (pad.Connected
                && XInputHelper.GetButton(in pad, buttonName)
                && !XInputHelper.GetButton(in _prevHotkeyPad, buttonName))
            {
                HotkeyFired?.Invoke(action);
            }
        }

        // Advance edge state
        _prevHotkeyKeys.Clear();
        foreach (var k in curr) _prevHotkeyKeys.Add(k);
        _prevHotkeyPad = pad;
    }

    // ── IInputReader: binding UI ───────────────────────────────────────────────

    public bool IsAnyInputJustPressed()
    {
        var keys = _keyboardSource.GetPressedKeysCopy();
        foreach (var k in keys)
            if (!_prevHotkeyKeys.Contains(k)) return true;

        var curr = XInputHelper.GetState(0);
        if (!curr.Connected) return false;
        var prev = _prevHotkeyPad;
        return (curr.A             && !prev.A)             || (curr.B             && !prev.B)             ||
               (curr.X             && !prev.X)             || (curr.Y             && !prev.Y)             ||
               (curr.Start         && !prev.Start)         || (curr.Back          && !prev.Back)          ||
               (curr.LeftShoulder  && !prev.LeftShoulder)  || (curr.RightShoulder && !prev.RightShoulder) ||
               (curr.LeftThumb     && !prev.LeftThumb)     || (curr.RightThumb    && !prev.RightThumb)    ||
               (curr.DPadUp        && !prev.DPadUp)        || (curr.DPadDown      && !prev.DPadDown)      ||
               (curr.DPadLeft      && !prev.DPadLeft)      || (curr.DPadRight     && !prev.DPadRight);
    }

    public bool PollAnyControllerButton()
    {
        bool any = false;
        if (_xInputSource is IAnyButtonSource xa) any |= xa.AnyJustPressed();
        if (_steamSource  is IAnyButtonSource sa) any |= sa.AnyJustPressed();
        return any;
    }

    public void FlushBindingEdges()
    {
        if (_xInputSource is IBindingSource b) b.FlushEdges();
    }

    public string? PollAnyGamepadButtonPressed()
    {
        if (_xInputSource is IBindingSource b) return b.PollAnyButtonPressed();
        return null;
    }
}
