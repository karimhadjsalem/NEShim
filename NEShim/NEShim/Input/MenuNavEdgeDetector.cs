namespace NEShim.Input;

/// <summary>
/// Shared edge-detection (+ optional debounce) for the 6 menu-nav directions
/// (Up/Down/Left/Right/Confirm/Back). Extracted from SDL3GamepadSource and SteamInputManager,
/// which each computed their own raw "is this direction currently held" booleans from different
/// underlying APIs (SDL3 gamepad state vs. Steam Input digital actions) but needed identical
/// edge-triggering logic on top of them.
/// </summary>
internal sealed class MenuNavEdgeDetector
{
    // Sentinel far enough in the past that the very first press is never itself debounced,
    // regardless of what the real/injected clock's initial value happens to be, without risking
    // overflow in the "now - lastFireTicks" subtraction.
    private const long NeverFired = long.MinValue / 2;

    // 0 disables debounce entirely (Debounce always accepts) — used by callers whose input
    // source doesn't need it (e.g. Steam Input, which debounces at its own layer already).
    private readonly long _debounceMs;

    private bool _prevUp, _prevDown, _prevLeft, _prevRight, _prevConfirm, _prevBack;

    private long _lastUpFireTicks      = NeverFired;
    private long _lastDownFireTicks    = NeverFired;
    private long _lastLeftFireTicks    = NeverFired;
    private long _lastRightFireTicks   = NeverFired;
    private long _lastConfirmFireTicks = NeverFired;
    private long _lastBackFireTicks    = NeverFired;

    // Test seam: injects a controllable time source for debounce testing; null means Environment.TickCount64.
    internal Func<long>? NowProvider;

    internal MenuNavEdgeDetector(long debounceMs = 0) => _debounceMs = debounceMs;

    /// <summary>
    /// Computes edge-triggered nav input from this poll's raw held-state booleans, then advances
    /// the previous-state snapshot for next time.
    /// </summary>
    internal MenuNavInput Advance(bool up, bool down, bool left, bool right, bool confirm, bool back)
    {
        long now = NowProvider != null ? NowProvider() : Environment.TickCount64;

        var nav = new MenuNavInput
        {
            Up      = up      && !_prevUp      && Debounce(ref _lastUpFireTicks,      now),
            Down    = down    && !_prevDown    && Debounce(ref _lastDownFireTicks,    now),
            Left    = left    && !_prevLeft    && Debounce(ref _lastLeftFireTicks,    now),
            Right   = right   && !_prevRight   && Debounce(ref _lastRightFireTicks,   now),
            Confirm = confirm && !_prevConfirm && Debounce(ref _lastConfirmFireTicks, now),
            Back    = back    && !_prevBack    && Debounce(ref _lastBackFireTicks,    now),
        };

        _prevUp = up; _prevDown = down; _prevLeft = left;
        _prevRight = right; _prevConfirm = confirm; _prevBack = back;

        return nav;
    }

    /// <summary>Clears the previous-state snapshot (e.g. when the device disconnects).</summary>
    internal void Reset() => _prevUp = _prevDown = _prevLeft = _prevRight = _prevConfirm = _prevBack = false;

    // Suppresses a detected edge if one for the same direction was already accepted within
    // _debounceMs. Only updates lastFireTicks when actually accepting, so a run of debounced
    // edges doesn't keep pushing the window forward.
    private bool Debounce(ref long lastFireTicks, long now)
    {
        if (_debounceMs <= 0) return true;
        if (now - lastFireTicks < _debounceMs) return false;
        lastFireTicks = now;
        return true;
    }
}
