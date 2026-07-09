using System.Collections.Generic;
using NEShim.Config;

namespace NEShim.Input.Sources;

/// <summary>
/// Captures gamepad state via an <see cref="IGamepadDevice"/>.
/// Implements IInputSource (gameplay button identifiers), IMenuNavSource
/// (edge-triggered menu navigation), and IBindingSource (rebinding UI).
/// The three polling paths each maintain their own edge-detection state and run
/// in mutually exclusive phases of the emulation loop.
/// </summary>
internal sealed class SDL3GamepadSource : IInputSource, IMenuNavSource, IBindingSource, IAnyButtonSource
{
    // Firm-push threshold for rebinding (60% of full range). High enough that accidental
    // touches won't register; deliberate pushes will.
    private const int BindingAnalogThreshold = 19660;

    // Moderate threshold for "any input" detection (logo skip, disconnect dismiss).
    // Matches the default gameplay deadzone so any movement that would register
    // in-game also registers here.
    private const int AnyInputAnalogThreshold = 8000;

    private readonly IGamepadDevice _device;

    private GamepadState _lastState;
    private GamepadState _prevMenuState;
    private GamepadState _prevBindingState;
    private GamepadState _prevAnyState;

    // Change-detection for analog stick logging — only logs when stick moves or analog output changes.
    private short _lastLoggedLX;
    private short _lastLoggedLY;
    private bool  _lastHadAnalog;

    internal SDL3GamepadSource(IGamepadDevice device)
    {
        _device = device;
    }

    public bool IsAvailable => _lastState.Connected;

    public IReadOnlySet<string> GetActiveIdentifiers(AppConfig config)
    {
        _lastState = _device.GetState(0);

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

        LogAnalogIfChanged(result, config.GamepadDeadzone, config.AnalogStickMode);
        return result;
    }

    private void LogAnalogIfChanged(HashSet<string> identifiers, int dz, string mode)
    {
        if (!Logger.IsEnabled || !_lastState.Connected) return;

        bool hasAnalog = identifiers.Contains("AnalogUp")   || identifiers.Contains("AnalogDown")
                      || identifiers.Contains("AnalogLeft")  || identifiers.Contains("AnalogRight");
        bool thumbMoved = Math.Abs(_lastState.ThumbLX - _lastLoggedLX) > 1000
                       || Math.Abs(_lastState.ThumbLY - _lastLoggedLY) > 1000;

        if (!thumbMoved && hasAnalog == _lastHadAnalog) return;

        string analog = hasAnalog
            ? string.Join(",", new[] { "AnalogUp","AnalogDown","AnalogLeft","AnalogRight" }
                .Where(identifiers.Contains))
            : "none";
        Logger.Log($"[Gamepad] LX={_lastState.ThumbLX} LY={_lastState.ThumbLY} dz={dz} mode={mode} → {analog}");
        _lastLoggedLX   = _lastState.ThumbLX;
        _lastLoggedLY   = _lastState.ThumbLY;
        _lastHadAnalog  = hasAnalog;
    }

    public MenuNavInput GetMenuNav(AppConfig config)
    {
        var curr = _device.GetState(0);
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

    // ── IBindingSource ─────────────────────────────────────────────────────────

    public void FlushEdges()
    {
        var state = _device.GetState(0);
        _prevBindingState = state.Connected ? state : default;
    }

    public string? PollAnyButtonPressed()
    {
        var curr = _device.GetState(0);
        var prev = _prevBindingState;

        if (!curr.Connected) { _prevBindingState = default; return null; }
        _prevBindingState = curr;

        if (curr.A             && !prev.A)             return "A";
        if (curr.B             && !prev.B)             return "B";
        if (curr.X             && !prev.X)             return "X";
        if (curr.Y             && !prev.Y)             return "Y";
        if (curr.Start         && !prev.Start)         return "Start";
        if (curr.Back          && !prev.Back)          return "Back";
        if (curr.LeftShoulder  && !prev.LeftShoulder)  return "LeftShoulder";
        if (curr.RightShoulder && !prev.RightShoulder) return "RightShoulder";
        if (curr.LeftThumb     && !prev.LeftThumb)     return "LeftThumb";
        if (curr.RightThumb    && !prev.RightThumb)    return "RightThumb";
        if (curr.DPadUp        && !prev.DPadUp)        return "DPadUp";
        if (curr.DPadDown      && !prev.DPadDown)      return "DPadDown";
        if (curr.DPadLeft      && !prev.DPadLeft)      return "DPadLeft";
        if (curr.DPadRight     && !prev.DPadRight)     return "DPadRight";

        // Analog stick: requires a firm deliberate push with dominant-axis logic so a diagonal
        // push doesn't produce two bindings.
        bool currUp    = AnalogStickHelper.StickUp(curr.ThumbLX,    curr.ThumbLY,    BindingAnalogThreshold);
        bool currDown  = AnalogStickHelper.StickDown(curr.ThumbLX,  curr.ThumbLY,    BindingAnalogThreshold);
        bool currLeft  = AnalogStickHelper.StickLeft(curr.ThumbLX,  curr.ThumbLY,    BindingAnalogThreshold);
        bool currRight = AnalogStickHelper.StickRight(curr.ThumbLX, curr.ThumbLY,    BindingAnalogThreshold);
        bool prevUp    = AnalogStickHelper.StickUp(prev.ThumbLX,    prev.ThumbLY,    BindingAnalogThreshold);
        bool prevDown  = AnalogStickHelper.StickDown(prev.ThumbLX,  prev.ThumbLY,    BindingAnalogThreshold);
        bool prevLeft  = AnalogStickHelper.StickLeft(prev.ThumbLX,  prev.ThumbLY,    BindingAnalogThreshold);
        bool prevRight = AnalogStickHelper.StickRight(prev.ThumbLX, prev.ThumbLY,    BindingAnalogThreshold);

        if (currUp    && !prevUp)    return "AnalogUp";
        if (currDown  && !prevDown)  return "AnalogDown";
        if (currLeft  && !prevLeft)  return "AnalogLeft";
        if (currRight && !prevRight) return "AnalogRight";

        return null;
    }

    // ── IAnyButtonSource ───────────────────────────────────────────────────────

    public bool AnyJustPressed()
    {
        var curr = _device.GetState(0);
        var prev = _prevAnyState;

        if (!curr.Connected) { _prevAnyState = default; return false; }
        _prevAnyState = curr;

        if ((curr.A             && !prev.A)             || (curr.B             && !prev.B)             ||
            (curr.X             && !prev.X)             || (curr.Y             && !prev.Y)             ||
            (curr.Start         && !prev.Start)         || (curr.Back          && !prev.Back)          ||
            (curr.LeftShoulder  && !prev.LeftShoulder)  || (curr.RightShoulder && !prev.RightShoulder) ||
            (curr.LeftThumb     && !prev.LeftThumb)     || (curr.RightThumb    && !prev.RightThumb)    ||
            (curr.DPadUp        && !prev.DPadUp)        || (curr.DPadDown      && !prev.DPadDown)      ||
            (curr.DPadLeft      && !prev.DPadLeft)      || (curr.DPadRight     && !prev.DPadRight))
            return true;

        bool currAnyAnalog = Math.Abs(curr.ThumbLX) > AnyInputAnalogThreshold
                          || Math.Abs(curr.ThumbLY) > AnyInputAnalogThreshold;
        bool prevAnyAnalog = Math.Abs(prev.ThumbLX) > AnyInputAnalogThreshold
                          || Math.Abs(prev.ThumbLY) > AnyInputAnalogThreshold;
        return currAnyAnalog && !prevAnyAnalog;
    }
}
