using System.Runtime.InteropServices;

namespace NEShim.Input;

/// <summary>
/// Minimal P/Invoke wrapper for XInput. Uses xinput1_4.dll (present on Windows 8+).
/// </summary>
internal static class XInputHelper
{
    private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    // wButtons bitmask values
    private const ushort XINPUT_GAMEPAD_DPAD_UP        = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN       = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT       = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT      = 0x0008;
    private const ushort XINPUT_GAMEPAD_START           = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK            = 0x0020;
    private const ushort XINPUT_GAMEPAD_LEFT_THUMB      = 0x0040;
    private const ushort XINPUT_GAMEPAD_RIGHT_THUMB     = 0x0080;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER   = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER  = 0x0200;
    private const ushort XINPUT_GAMEPAD_A               = 0x1000;
    private const ushort XINPUT_GAMEPAD_B               = 0x2000;
    private const ushort XINPUT_GAMEPAD_X               = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y               = 0x8000;

    public struct GamepadState
    {
        public bool DPadUp, DPadDown, DPadLeft, DPadRight;
        public bool Start, Back;
        public bool LeftShoulder, RightShoulder;
        public bool LeftThumb, RightThumb;
        public bool A, B, X, Y;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        public bool Connected;
    }

    public static GamepadState GetState(uint userIndex = 0)
    {
        uint result = XInputGetState(userIndex, out var state);
        if (result == ERROR_DEVICE_NOT_CONNECTED)
            return default; // Connected = false

        ushort buttons = state.Gamepad.wButtons;
        return new GamepadState
        {
            Connected      = true,
            DPadUp         = (buttons & XINPUT_GAMEPAD_DPAD_UP)       != 0,
            DPadDown       = (buttons & XINPUT_GAMEPAD_DPAD_DOWN)      != 0,
            DPadLeft       = (buttons & XINPUT_GAMEPAD_DPAD_LEFT)      != 0,
            DPadRight      = (buttons & XINPUT_GAMEPAD_DPAD_RIGHT)     != 0,
            Start          = (buttons & XINPUT_GAMEPAD_START)          != 0,
            Back           = (buttons & XINPUT_GAMEPAD_BACK)           != 0,
            LeftShoulder   = (buttons & XINPUT_GAMEPAD_LEFT_SHOULDER)  != 0,
            RightShoulder  = (buttons & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0,
            LeftThumb      = (buttons & XINPUT_GAMEPAD_LEFT_THUMB)     != 0,
            RightThumb     = (buttons & XINPUT_GAMEPAD_RIGHT_THUMB)    != 0,
            A              = (buttons & XINPUT_GAMEPAD_A)              != 0,
            B              = (buttons & XINPUT_GAMEPAD_B)              != 0,
            X              = (buttons & XINPUT_GAMEPAD_X)              != 0,
            Y              = (buttons & XINPUT_GAMEPAD_Y)              != 0,
            ThumbLX        = state.Gamepad.sThumbLX,
            ThumbLY        = state.Gamepad.sThumbLY,
            ThumbRX        = state.Gamepad.sThumbRX,
            ThumbRY        = state.Gamepad.sThumbRY,
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
}
