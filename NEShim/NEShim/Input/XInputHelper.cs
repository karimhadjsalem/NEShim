using SDL3;

namespace NEShim.Input;

/// <summary>
/// Provides cross-platform gamepad state via the SDL3 gamepad API.
/// Wraps SDL_GetGamepadButton / SDL_GetGamepadAxis into the same GamepadState
/// data contract that XInputSource and InputManager depend on for polling.
/// <para>
/// Axis convention note: SDL3 LeftY is -32768 (up) .. 32767 (down). ThumbLY is negated
/// so that positive values map to "up", matching the XInput convention that
/// AnalogStickHelper and all config bindings expect.
/// </para>
/// </summary>
internal static class XInputHelper
{
    private static IntPtr _gamepad = IntPtr.Zero;

    public struct GamepadState
    {
        public bool DPadUp, DPadDown, DPadLeft, DPadRight;
        public bool Start, Back;
        public bool LeftShoulder, RightShoulder;
        public bool LeftThumb, RightThumb;
        public bool A, B, X, Y;
        public short ThumbLX, ThumbLY;
        public bool Connected;
    }

    public static GamepadState GetState(uint userIndex = 0)
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
            ThumbLY       = (short)-SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftY),
        };
    }

    /// <summary>Returns the named button value from a GamepadState by config name string.</summary>
    public static bool GetButton(in GamepadState state, string? buttonName)
    {
        if (buttonName is null) return false;
        return buttonName switch
        {
            "DPadUp"        => state.DPadUp,
            "DPadDown"      => state.DPadDown,
            "DPadLeft"      => state.DPadLeft,
            "DPadRight"     => state.DPadRight,
            "Start"         => state.Start,
            "Back"          => state.Back,
            "LeftShoulder"  => state.LeftShoulder,
            "RightShoulder" => state.RightShoulder,
            "LeftThumb"     => state.LeftThumb,
            "RightThumb"    => state.RightThumb,
            "A"             => state.A,
            "B"             => state.B,
            "X"             => state.X,
            "Y"             => state.Y,
            _ => false,
        };
    }

    internal static void Dispose()
    {
        if (_gamepad == IntPtr.Zero) return;
        SDL.CloseGamepad(_gamepad);
        _gamepad = IntPtr.Zero;
    }

    private static IntPtr EnsureGamepadOpen(uint userIndex)
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
