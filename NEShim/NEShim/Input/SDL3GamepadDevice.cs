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
    private IntPtr _gamepad = IntPtr.Zero;

    public GamepadState GetState(uint userIndex = 0)
    {
        IntPtr pad = EnsureGamepadOpen(userIndex);
        if (pad == IntPtr.Zero) return default;

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
        if (_gamepad == IntPtr.Zero) return;
        SDL.CloseGamepad(_gamepad);
        _gamepad = IntPtr.Zero;
    }

    private IntPtr EnsureGamepadOpen(uint userIndex)
    {
        if (_gamepad != IntPtr.Zero && SDL.GamepadConnected(_gamepad))
            return _gamepad;

        if (_gamepad != IntPtr.Zero) { SDL.CloseGamepad(_gamepad); _gamepad = IntPtr.Zero; }

        var ids = SDL.GetGamepads(out int count);
        if (ids is null || count == 0) return IntPtr.Zero;

        uint targetId = userIndex < (uint)count ? ids[userIndex] : ids[0];
        _gamepad = SDL.OpenGamepad(targetId);
        return _gamepad;
    }
}
