using System.Collections.Generic;
using NEShim.Config;

namespace NEShim.Input.Sources;

/// <summary>
/// Captures XInput gamepad state via XInputHelper.GetState(0).
/// Implements IInputSource (gameplay button identifiers) and IMenuNavSource
/// (edge-triggered menu navigation with its own edge-detection state).
/// The two polling paths are independent because gameplay and menu navigation
/// run in mutually exclusive phases of the emulation loop.
/// </summary>
internal sealed class XInputSource : IInputSource, IMenuNavSource
{
    private readonly Func<XInputHelper.GamepadState>? _stateProvider;

    private XInputHelper.GamepadState _lastState;
    private XInputHelper.GamepadState _prevMenuState;

    internal XInputSource() { }

    internal XInputSource(Func<XInputHelper.GamepadState> stateProvider)
    {
        _stateProvider = stateProvider;
    }

    public bool IsAvailable => _lastState.Connected;

    public IReadOnlySet<string> GetActiveIdentifiers(AppConfig config)
    {
        _lastState = _stateProvider?.Invoke() ?? XInputHelper.GetState(0);

        if (!_lastState.Connected)
            return new HashSet<string>();

        var result = new HashSet<string>(16);

        if (_lastState.DPadUp)        result.Add("DPadUp");
        if (_lastState.DPadDown)      result.Add("DPadDown");
        if (_lastState.DPadLeft)      result.Add("DPadLeft");
        if (_lastState.DPadRight)     result.Add("DPadRight");
        if (_lastState.Start)         result.Add("Start");
        if (_lastState.Back)          result.Add("Back");
        if (_lastState.LeftShoulder)  result.Add("LeftShoulder");
        if (_lastState.RightShoulder) result.Add("RightShoulder");
        if (_lastState.LeftThumb)     result.Add("LeftThumb");
        if (_lastState.RightThumb)    result.Add("RightThumb");
        if (_lastState.A)             result.Add("A");
        if (_lastState.B)             result.Add("B");
        if (_lastState.X)             result.Add("X");
        if (_lastState.Y)             result.Add("Y");

        int dz = config.GamepadDeadzone;
        int lx = _lastState.ThumbLX;
        int ly = _lastState.ThumbLY;

        bool diagonal = config.AnalogStickMode.Equals("Diagonal", StringComparison.OrdinalIgnoreCase);
        if (diagonal)
        {
            if (ly >  dz) result.Add("AnalogUp");
            if (ly < -dz) result.Add("AnalogDown");
            if (lx < -dz) result.Add("AnalogLeft");
            if (lx >  dz) result.Add("AnalogRight");
        }
        else
        {
            if (AnalogStickHelper.StickUp(lx, ly, dz))    result.Add("AnalogUp");
            if (AnalogStickHelper.StickDown(lx, ly, dz))  result.Add("AnalogDown");
            if (AnalogStickHelper.StickLeft(lx, ly, dz))  result.Add("AnalogLeft");
            if (AnalogStickHelper.StickRight(lx, ly, dz)) result.Add("AnalogRight");
        }

        return result;
    }

    public MenuNavInput GetMenuNav(AppConfig config)
    {
        var curr = _stateProvider?.Invoke() ?? XInputHelper.GetState(0);
        var prev = _prevMenuState;

        _prevMenuState = curr.Connected ? curr : default;

        if (!curr.Connected)
            return default;

        int dz = config.GamepadDeadzone;

        bool up      = curr.DPadUp    || AnalogStickHelper.StickUp(curr.ThumbLX,   curr.ThumbLY,   dz);
        bool down    = curr.DPadDown  || AnalogStickHelper.StickDown(curr.ThumbLX,  curr.ThumbLY,  dz);
        bool left    = curr.DPadLeft  || AnalogStickHelper.StickLeft(curr.ThumbLX,  curr.ThumbLY,  dz);
        bool right   = curr.DPadRight || AnalogStickHelper.StickRight(curr.ThumbLX, curr.ThumbLY,  dz);
        bool confirm = curr.A;
        bool back    = curr.B || curr.Back;

        bool prevUp      = prev.Connected && (prev.DPadUp    || AnalogStickHelper.StickUp(prev.ThumbLX,   prev.ThumbLY,   dz));
        bool prevDown    = prev.Connected && (prev.DPadDown  || AnalogStickHelper.StickDown(prev.ThumbLX,  prev.ThumbLY,  dz));
        bool prevLeft    = prev.Connected && (prev.DPadLeft  || AnalogStickHelper.StickLeft(prev.ThumbLX,  prev.ThumbLY,  dz));
        bool prevRight   = prev.Connected && (prev.DPadRight || AnalogStickHelper.StickRight(prev.ThumbLX, prev.ThumbLY,  dz));
        bool prevConfirm = prev.Connected && prev.A;
        bool prevBack    = prev.Connected && (prev.B || prev.Back);

        return new MenuNavInput
        {
            Up      = up      && !prevUp,
            Down    = down    && !prevDown,
            Left    = left    && !prevLeft,
            Right   = right   && !prevRight,
            Confirm = confirm && !prevConfirm,
            Back    = back    && !prevBack,
        };
    }
}
