using SDL3;

namespace NEShim.Input;

/// <summary>
/// Reads gamepad state via the SDL3 gamepad API. Implements <see cref="IGamepadDevice"/>.
/// <para>
/// Axis convention: SDL3 LeftY is -32768 (up) to 32767 (down). ThumbLY is negated so
/// positive = up, matching what AnalogStickHelper and all config bindings expect.
/// </para>
/// </summary>
internal sealed class SDL3GamepadDevice : IGamepadDevice
{
    // One shared registry for up to 4 simultaneously-open physical gamepads, one slot per
    // player (userIndex 0-3), keyed by SDL.GetGamepads()'s enumeration order. A single instance
    // owns all slots — rather than one SDL3GamepadDevice per player — so SDL.GetGamepads()
    // enumeration and reconnect bookkeeping stay centralized in one place instead of being
    // independently polled/tracked once per player.
    private const int MaxSlots = 4;

    private readonly IntPtr[]          _gamepads    = new IntPtr[MaxSlots];
    private readonly bool[]            _justOpened  = new bool[MaxSlots];
    private readonly SDL.GamepadType[] _cachedTypes = new SDL.GamepadType[MaxSlots];

    public SDL.GamepadType GetGamepadType(uint userIndex = 0)
    {
        IntPtr pad = EnsureGamepadOpen(userIndex);
        return pad == IntPtr.Zero ? SDL.GamepadType.Unknown : _cachedTypes[userIndex];
    }

    public GamepadState GetState(uint userIndex = 0)
    {
        IntPtr pad = EnsureGamepadOpen(userIndex);
        if (pad == IntPtr.Zero) return default;

        // A gamepad's button/axis state right after SDL_OpenGamepad can be stale/incomplete
        // until SDL's internal state has a chance to settle (a real, documented SDL3 gamepad
        // quirk — see github.com/libsdl-org/SDL/issues/8177, "gamepad opened too early" — plus a
        // related "not working if plugged in at game start" report). Reporting Connected=false
        // for just the one GetState call immediately after opening — rather than trusting
        // whatever that first read happens to contain — means every downstream edge-detector
        // (MenuNavEdgeDetector etc.) starts from a genuinely settled baseline instead of a
        // possibly-garbage one, which otherwise manifested as a burst of spurious extra menu
        // moves right at the start of the very first press after the controller connects.
        if (_justOpened[userIndex])
        {
            _justOpened[userIndex] = false;
            return default;
        }

        return new GamepadState
        {
            Connected     = true,
            DPadUp        = SDL.GetGamepadButton(pad, SDL.GamepadButton.DPadUp),
            DPadDown      = SDL.GetGamepadButton(pad, SDL.GamepadButton.DPadDown),
            DPadLeft      = SDL.GetGamepadButton(pad, SDL.GamepadButton.DPadLeft),
            DPadRight     = SDL.GetGamepadButton(pad, SDL.GamepadButton.DPadRight),
            Start         = SDL.GetGamepadButton(pad, SDL.GamepadButton.Start),
            Back          = SDL.GetGamepadButton(pad, SDL.GamepadButton.Back),
            LeftShoulder  = SDL.GetGamepadButton(pad, SDL.GamepadButton.LeftShoulder),
            RightShoulder = SDL.GetGamepadButton(pad, SDL.GamepadButton.RightShoulder),
            LeftThumb     = SDL.GetGamepadButton(pad, SDL.GamepadButton.LeftStick),
            RightThumb    = SDL.GetGamepadButton(pad, SDL.GamepadButton.RightStick),
            A             = SDL.GetGamepadButton(pad, SDL.GamepadButton.South),
            B             = SDL.GetGamepadButton(pad, SDL.GamepadButton.East),
            X             = SDL.GetGamepadButton(pad, SDL.GamepadButton.West),
            Y             = SDL.GetGamepadButton(pad, SDL.GamepadButton.North),
            ThumbLX       = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftX),
            ThumbLY       = (short)Math.Clamp(-(int)SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftY), short.MinValue + 1, short.MaxValue),
        };
    }

    public void Dispose()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (_gamepads[i] == IntPtr.Zero) continue;
            SDL.CloseGamepad(_gamepads[i]);
            _gamepads[i] = IntPtr.Zero;
        }
    }

    private IntPtr EnsureGamepadOpen(uint userIndex)
    {
        if (userIndex >= MaxSlots) return IntPtr.Zero;

        if (_gamepads[userIndex] != IntPtr.Zero && SDL.GamepadConnected(_gamepads[userIndex]))
            return _gamepads[userIndex];

        if (_gamepads[userIndex] != IntPtr.Zero) { SDL.CloseGamepad(_gamepads[userIndex]); _gamepads[userIndex] = IntPtr.Zero; }

        var ids = SDL.GetGamepads(out int count);
        // No fallback to ids[0] when this slot's index is out of range — unlike a single-slot
        // device (where userIndex was always 0 and this branch was unreachable), falling back
        // here would mean an unassigned player silently double-drives player 1's physical
        // gamepad instead of correctly reporting as disconnected.
        if (ids is null || userIndex >= (uint)count) return IntPtr.Zero;

        _gamepads[userIndex] = SDL.OpenGamepad(ids[userIndex]);
        if (_gamepads[userIndex] != IntPtr.Zero)
        {
            _justOpened[userIndex]  = true;
            _cachedTypes[userIndex] = SDL.GetGamepadType(_gamepads[userIndex]);
        }
        return _gamepads[userIndex];
    }
}
