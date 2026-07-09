using NEShim.Config;
using NEShim.Input;
using NEShim.UI;

namespace NEShim.GameLoop;

/// <summary>
/// Handles all per-frame input work: snapshot polling, disconnect-screen dismissal,
/// hotkey edge advancement, and paused-state gamepad dispatch.
/// </summary>
internal sealed class InputProcessor
{
    private readonly IInputReader    _input;
    private readonly InGameMenu      _menu;
    private readonly IMenuInputTarget _menuInput;
    private readonly Action<Action>  _marshalToUiThread;

    private bool                _justDismissedDisconnectScreen;
    private bool                _prevIsWaitingForGamepadButton;
    private SliderRepeatTracker _sliderRepeat;

    /// <summary>
    /// Tracks timing for continuous left/right slider movement when a direction is held.
    /// Fires after an initial delay then at a fixed repeat interval, accelerating after 1 s.
    /// </summary>
    private struct SliderRepeatTracker
    {
        internal const long InitialDelayMs          = 400;
        internal const long SlowRepeatIntervalMs    =  80;  // ~12 Hz
        internal const long FastRepeatIntervalMs    =  30;  // ~33 Hz
        internal const long AccelerationThresholdMs = 1000; // total hold time before fast phase

        private int  _heldDirection; // -1 = left, 0 = none, +1 = right
        private long _holdStartTick;
        private long _nextFireTick;

        // Overridable for unit tests; null means Environment.TickCount64.
        internal Func<long>? NowProvider;

        /// <summary>
        /// Advances the tracker for the current frame. Returns a synthetic nav input when
        /// the repeat timer fires, or null when no repeat should trigger this frame.
        /// Repeat rate accelerates after <c>AccelerationThresholdMs</c> of continuous hold.
        /// </summary>
        public MenuNavInput? Advance(bool heldLeft, bool heldRight)
        {
            int direction = heldRight ? 1 : heldLeft ? -1 : 0;

            if (direction == 0)
            {
                _heldDirection = 0;
                return null;
            }

            long now = NowProvider != null ? NowProvider() : Environment.TickCount64;

            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _holdStartTick = now;
                _nextFireTick  = now + InitialDelayMs;
                return null;
            }

            if (now < _nextFireTick) return null;

            long totalHeld   = now - _holdStartTick;
            long interval    = totalHeld >= AccelerationThresholdMs ? FastRepeatIntervalMs : SlowRepeatIntervalMs;
            _nextFireTick    = now + interval;
            return direction < 0
                ? new MenuNavInput { Left  = true }
                : new MenuNavInput { Right = true };
        }
    }

    // Test seam: injects a controllable time source into the slider repeat tracker.
    internal Func<long>? SliderTrackerNowProvider { set => _sliderRepeat.NowProvider = value; }

    /// <summary>
    /// True for the frame on which the controller-disconnect overlay was dismissed.
    /// Read by <see cref="EmulationThread.HandleMenuToggle"/> to suppress a simultaneous Esc/Start press.
    /// </summary>
    public bool JustDismissedDisconnectScreen => _justDismissedDisconnectScreen;

    public InputProcessor(
        IInputReader input,
        InGameMenu menu,
        IMenuInputTarget menuInput,
        Action<Action> marshalToUiThread)
    {
        _input = input;
        _menu = menu;
        _menuInput = menuInput;
        _marshalToUiThread = marshalToUiThread;
    }

    /// <summary>Polls all input sources and returns the frame snapshot.</summary>
    public InputSnapshot Poll(AppConfig config) => _input.PollSnapshot(config);

    /// <summary>
    /// Dismisses the controller-disconnect screen on any input.
    /// Must be called before <see cref="AdvanceHotkeys"/> so <c>IsAnyInputJustPressed</c>
    /// uses the previous frame's edge state.
    /// </summary>
    /// <returns>True if the screen was dismissed this frame.</returns>
    public bool TryDismissDisconnectScreen(bool isMainMenuActive)
    {
        _justDismissedDisconnectScreen = false;
        if (!isMainMenuActive
            && _menu.IsOpen
            && _menu.Current == InGameMenu.Screen.ControllerDisconnected
            && _input.IsAnyInputJustPressed())
        {
            Logger.Log("[Emulation] Input received — dismissing disconnect screen.");
            _menu.Close();
            _justDismissedDisconnectScreen = true;
        }
        return _justDismissedDisconnectScreen;
    }

    /// <summary>Advances hotkey edge state, firing events on <see cref="IInputReader"/>.</summary>
    public void AdvanceHotkeys(AppConfig config) => _input.AdvanceHotkeyState(config);

    /// <summary>
    /// Polls gamepad input while the emulation loop is paused, dispatching navigation
    /// or button-rebind presses to the active menu on the UI thread.
    /// </summary>
    public void PollPausedMenuInput(AppConfig config)
    {
        bool isWaiting = _menuInput.IsWaitingForGamepadButton;

        if (isWaiting)
        {
            // Entering binding mode: seed _prevBindingPad so the button used to confirm the
            // slot selection (A) is already "seen" and won't fire as the new binding.
            if (!_prevIsWaitingForGamepadButton)
                _input.FlushBindingEdges();

            // Rebind is always XInput-only. Native Steam controllers remap via
            // Steam's controller configurator; their binding rows are read-only
            // in the menu when IsUsingNativeActions() is true.
            string? btn = _input.PollAnyGamepadButtonPressed();
            if (btn != null)
                _marshalToUiThread(() => _menuInput.HandleGamepadButtonPress(btn));
        }
        else
        {
            // Exiting binding mode: advance nav edge state past the button just used for
            // binding so it doesn't fire a navigation action (e.g. B → back) on the next frame.
            if (_prevIsWaitingForGamepadButton)
                _input.PollMenuNav(config);

            var nav = _input.PollMenuNav(config);
            if (nav.Any)
                _marshalToUiThread(() => _menuInput.HandleGamepadNav(nav));

            var (heldLeft, heldRight) = _input.GetHeldSliderDir(config);
            var repeat = _sliderRepeat.Advance(heldLeft, heldRight);
            if (repeat.HasValue)
            {
                var repeatNav = repeat.Value;
                _marshalToUiThread(() => _menuInput.HandleGamepadNav(repeatNav));
            }
        }

        _prevIsWaitingForGamepadButton = isWaiting;
    }
}
