using System.Collections.Generic;
using System.Collections.Immutable;
using SDL3;
using NEShim.Config;
using NEShim.Input.Mappers;
using NEShim.Input.Sources;

namespace NEShim.Input;

/// <summary>
/// Coordinates input sources and mappers to produce per-frame InputSnapshot values.
/// Fires IoC events for hotkey edges, menu-toggle, and controller disconnect so callers
/// (EmulationThread, NEShimApp) register handlers once rather than polling each frame.
///
/// Gamepad/Steam sources+mappers are per-player lists (one entry per active player, sized by
/// AppConfig.PlayerCount, built by NEShimApp's composition root) so PollSnapshot fans out
/// gameplay button identifiers across every configured player without any per-player special
/// casing in the polling loop itself (OCP: adding a player means adding a list entry, not a new
/// branch here). Hotkeys, menu navigation, the "any button" wake gesture, and the disconnect-
/// screen trigger are deliberately NOT fanned out — they stay wired to index 0 (player 1) only,
/// exactly matching this class's pre-multiplayer behavior, since these are session-level/system
/// concerns rather than per-player gameplay concerns.
/// </summary>
internal sealed class InputManager : IInputReader
{
    private readonly KeyboardInputSource _keyboardSource;
    private readonly IInputMapper        _keyboardMapper;
    private readonly IGamepadDevice      _gamepadDevice;

    private readonly IReadOnlyList<IInputSource> _gamepadSources;
    private readonly IReadOnlyList<IInputMapper> _gamepadMappers;
    private readonly IReadOnlyList<IInputSource> _steamSources;
    private readonly IReadOnlyList<IInputMapper> _steamMappers;

    // Edge detection for hotkeys
    private readonly HashSet<SDL.Keycode> _prevHotkeyKeys = new();
    private GamepadState _prevHotkeyPad;

    // Controller disconnect tracking — player 1 only, see class doc comment.
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
        KeyboardInputSource         keyboardSource,
        IReadOnlyList<IInputSource> gamepadSources,
        IReadOnlyList<IInputSource> steamSources,
        IInputMapper                keyboardMapper,
        IReadOnlyList<IInputMapper> gamepadMappers,
        IReadOnlyList<IInputMapper> steamMappers,
        IGamepadDevice              gamepadDevice)
    {
        _keyboardSource = keyboardSource;
        _gamepadSources = gamepadSources;
        _steamSources   = steamSources;
        _keyboardMapper = keyboardMapper;
        _gamepadMappers = gamepadMappers;
        _steamMappers   = steamMappers;
        _gamepadDevice  = gamepadDevice;
    }

    // ── IInputReader: keyboard forwarding ──────────────────────────────────────

    public void OnKeyDown(SDL.Keycode key) => _keyboardSource.OnKeyDown(key);
    public void OnKeyUp(SDL.Keycode key)   => _keyboardSource.OnKeyUp(key);

    // ── IInputReader: frame polling ────────────────────────────────────────────

    public InputSnapshot PollSnapshot(AppConfig config)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>();

        for (int i = 0; i < _steamSources.Count; i++)
        {
            var ids = _steamSources[i].GetActiveIdentifiers(config);
            if (_steamSources[i].IsAvailable)
                _steamMappers[i].Map(ids, config, builder);
        }

        for (int i = 0; i < _gamepadSources.Count; i++)
        {
            var ids = _gamepadSources[i].GetActiveIdentifiers(config);
            if (_gamepadSources[i].IsAvailable)
                _gamepadMappers[i].Map(ids, config, builder);
        }

        _keyboardMapper.Map(_keyboardSource.GetActiveIdentifiers(config), config, builder);

        // Player-1-only, matching this class's pre-multiplayer disconnect-screen behavior — see
        // class doc comment.
        bool gamepadNow = _gamepadSources[0].IsAvailable;
        bool steamNow   = _steamSources[0].IsAvailable;

        bool controllerNow = gamepadNow || steamNow;
        if (_wasControllerConnected && !controllerNow)
            GamepadDisconnected?.Invoke();
        else if (!_wasControllerConnected && controllerNow)
            GamepadConnected?.Invoke();
        _wasControllerConnected = controllerNow;

        if (gamepadNow != _prevLoggedGamepad || steamNow != _prevLoggedSteam)
        {
            Logger.Log($"[Input] Source (P1): Gamepad={gamepadNow}, Steam={steamNow} — deadzone={config.GamepadDeadzone}, mode={config.AnalogStickMode}");
            _prevLoggedGamepad = gamepadNow;
            _prevLoggedSteam   = steamNow;
        }

        return new InputSnapshot(builder.ToImmutable());
    }

    public MenuNavInput PollMenuNav(AppConfig config)
    {
        var result = default(MenuNavInput);

        if (_gamepadSources[0] is IMenuNavSource gamepadNav)
            result = MenuNavInput.Union(result, gamepadNav.GetMenuNav(config));

        if (_steamSources[0] is IMenuNavSource steamNav)
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

        (bool steamLeft, bool steamRight) = _steamSources[0] is IHeldDirectionSource steamHeld
            ? steamHeld.GetHeldLeftRight(config)
            : (false, false);

        return (keyLeft || padLeft || steamLeft, keyRight || padRight || steamRight);
    }

    /// <summary>
    /// Detects edge transitions for all configured hotkeys and fires events.
    /// Call once per frame on the emulation thread, after PollSnapshot.
    /// Player 1's gamepad only — see class doc comment.
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
        if (_gamepadSources[0] is IAnyButtonSource ga) any |= ga.AnyJustPressed();
        if (_steamSources[0]   is IAnyButtonSource sa) any |= sa.AnyJustPressed();
        return any;
    }

    /// <param name="player">1-based player whose gamepad binding-capture state to seed.
    /// Defaults to player 1. Clamped to the configured player range.</param>
    public void FlushBindingEdges(int player = 1)
    {
        if (_gamepadSources[GamepadIndexFor(player)] is IBindingSource b) b.FlushEdges();
    }

    /// <param name="player">1-based player whose gamepad to poll for a newly-pressed
    /// button/axis — the rebinding UI must read the physical device belonging to the player
    /// being rebound, not always player 1's. Defaults to player 1. Clamped to the configured
    /// player range.</param>
    public string? PollAnyGamepadButtonPressed(int player = 1)
    {
        if (_gamepadSources[GamepadIndexFor(player)] is IBindingSource b) return b.PollAnyButtonPressed();
        return null;
    }

    private int GamepadIndexFor(int player) => Math.Clamp(player - 1, 0, _gamepadSources.Count - 1);
}
