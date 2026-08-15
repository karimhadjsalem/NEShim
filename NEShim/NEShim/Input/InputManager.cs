using System.Collections.Generic;
using System.Collections.Immutable;
using SDL3;
using NEShim.Config;
using NEShim.Input.Mappers;
using NEShim.Input.Sources;
using NEShim.Steam;

namespace NEShim.Input;

/// <summary>
/// Coordinates input sources and mappers to produce per-frame InputSnapshot values.
/// Fires IoC events for hotkey edges, menu-toggle, and controller disconnect so callers
/// (EmulationThread, NEShimApp) register handlers once rather than polling each frame.
/// </summary>
internal sealed class InputManager : IInputReader
{
    private readonly KeyboardInputSource _keyboardSource;
    private readonly IInputSource        _gamepadSource;
    private readonly IInputSource        _steamSource;
    private readonly IInputMapper        _keyboardMapper;
    private readonly IInputMapper        _gamepadMapper;
    private readonly IInputMapper        _steamMapper;
    private readonly IGamepadDevice      _gamepadDevice;

    // Edge detection for hotkeys
    private readonly HashSet<SDL.Keycode> _prevHotkeyKeys = new();
    private GamepadState _prevHotkeyPad;

    // Controller disconnect tracking
    private bool _wasControllerConnected;
    private bool _prevLoggedGamepad;
    private bool _prevLoggedSteam;

    // ── IoC events ─────────────────────────────────────────────────────────────

    public event Action<string>? HotkeyFired;
    public event Action?         MenuToggleRequested;
    public event Action?         GamepadDisconnected;
    public event Action?         GamepadConnected;

    // ── Constructor ────────────────────────────────────────────────────────────

    internal InputManager(
        KeyboardInputSource keyboardSource,
        IInputSource        gamepadSource,
        IInputSource        steamSource,
        IInputMapper        keyboardMapper,
        IInputMapper        gamepadMapper,
        IInputMapper        steamMapper,
        IGamepadDevice      gamepadDevice)
    {
        _keyboardSource = keyboardSource;
        _gamepadSource  = gamepadSource;
        _steamSource    = steamSource;
        _keyboardMapper = keyboardMapper;
        _gamepadMapper  = gamepadMapper;
        _steamMapper    = steamMapper;
        _gamepadDevice  = gamepadDevice;
    }

    // ── IInputReader: keyboard forwarding ──────────────────────────────────────

    public void OnKeyDown(SDL.Keycode key) => _keyboardSource.OnKeyDown(key);
    public void OnKeyUp(SDL.Keycode key)   => _keyboardSource.OnKeyUp(key);

    // ── IInputReader: frame polling ────────────────────────────────────────────

    public InputSnapshot PollSnapshot(AppConfig config)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>();

        var steamIds = _steamSource.GetActiveIdentifiers(config);
        if (_steamSource.IsAvailable)
            _steamMapper.Map(steamIds, config, builder);

        var gamepadIds = _gamepadSource.GetActiveIdentifiers(config);
        if (_gamepadSource.IsAvailable)
            _gamepadMapper.Map(gamepadIds, config, builder);

        _keyboardMapper.Map(_keyboardSource.GetActiveIdentifiers(config), config, builder);

        bool controllerNow = _gamepadSource.IsAvailable || _steamSource.IsAvailable;
        if (_wasControllerConnected && !controllerNow)
            GamepadDisconnected?.Invoke();
        else if (!_wasControllerConnected && controllerNow)
            GamepadConnected?.Invoke();
        _wasControllerConnected = controllerNow;

        bool gamepadNow = _gamepadSource.IsAvailable;
        bool steamNow   = _steamSource.IsAvailable;
        if (gamepadNow != _prevLoggedGamepad || steamNow != _prevLoggedSteam)
        {
            Logger.Log($"[Input] Source: Gamepad={gamepadNow}, Steam={steamNow} — deadzone={config.GamepadDeadzone}, mode={config.AnalogStickMode}");
            _prevLoggedGamepad = gamepadNow;
            _prevLoggedSteam   = steamNow;
        }

        return new InputSnapshot(builder.ToImmutable());
    }

    public MenuNavInput PollMenuNav(AppConfig config)
    {
        var result = default(MenuNavInput);

        if (_gamepadSource is IMenuNavSource gamepadNav)
            result = MenuNavInput.Union(result, gamepadNav.GetMenuNav(config));

        if (_steamSource is IMenuNavSource steamNav)
            result = MenuNavInput.Union(result, steamNav.GetMenuNav(config));

        return result;
    }

    public (bool Left, bool Right) GetHeldSliderDir(AppConfig config)
    {
        bool keyLeft  = _keyboardSource.IsKeyPressed(SDL.Keycode.Left);
        bool keyRight = _keyboardSource.IsKeyPressed(SDL.Keycode.Right);

        var pad   = _gamepadDevice.GetState(0);
        int dz    = config.GamepadDeadzone;
        bool padLeft  = pad.Connected && (pad.DPadLeft  || AnalogStickHelper.StickLeft(pad.ThumbLX,  pad.ThumbLY, dz));
        bool padRight = pad.Connected && (pad.DPadRight || AnalogStickHelper.StickRight(pad.ThumbLX, pad.ThumbLY, dz));

        var (steamLeft, steamRight) = SteamInputManager.GetMenuHeldLeftRight();

        return (keyLeft || padLeft || steamLeft, keyRight || padRight || steamRight);
    }

    /// <summary>
    /// Detects edge transitions for all configured hotkeys and fires events.
    /// Call once per frame on the emulation thread, after PollSnapshot.
    /// </summary>
    public void AdvanceHotkeyState(AppConfig config)
    {
        var curr = _keyboardSource.GetPressedKeysCopy();
        var pad  = _gamepadDevice.GetState(0);

        // Menu toggle — fire at most once even if multiple triggers are active
        bool menuToggle =
            (curr.Contains(SDL.Keycode.Escape) && !_prevHotkeyKeys.Contains(SDL.Keycode.Escape))
            || (!config.OverrideStartBindingProtection
                && pad.Connected && pad.Start && !_prevHotkeyPad.Start)
            || (config.GamepadHotkeyMappings.TryGetValue("OpenMenu", out var openBtn)
                && pad.Connected
                && pad.GetButton(openBtn)
                && !_prevHotkeyPad.GetButton(openBtn));

        if (menuToggle)
            MenuToggleRequested?.Invoke();

        // Keyboard hotkeys
        foreach (var (action, keyName) in config.HotkeyMappings)
        {
            if (!KeycodeParser.TryParse(keyName, out var key)) continue;
            if (curr.Contains(key) && !_prevHotkeyKeys.Contains(key))
                HotkeyFired?.Invoke(action);
        }

        // Non-OpenMenu gamepad hotkeys
        foreach (var (action, buttonName) in config.GamepadHotkeyMappings)
        {
            if (action == "OpenMenu") continue;
            if (pad.Connected && pad.GetButton(buttonName) && !_prevHotkeyPad.GetButton(buttonName))
                HotkeyFired?.Invoke(action);
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

        var curr = _gamepadDevice.GetState(0);
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
        if (_gamepadSource is IAnyButtonSource ga) any |= ga.AnyJustPressed();
        if (_steamSource   is IAnyButtonSource sa) any |= sa.AnyJustPressed();
        return any;
    }

    public void FlushBindingEdges()
    {
        if (_gamepadSource is IBindingSource b) b.FlushEdges();
    }

    public string? PollAnyGamepadButtonPressed()
    {
        if (_gamepadSource is IBindingSource b) return b.PollAnyButtonPressed();
        return null;
    }
}
