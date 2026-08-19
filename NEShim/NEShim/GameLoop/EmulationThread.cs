using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.Input;
using NEShim.Rendering;
using NEShim.Saves;
using NEShim.UI;

namespace NEShim.GameLoop;

/// <summary>
/// Runs the NES emulation loop on a dedicated high-priority thread at ~60Hz.
/// Owns thread lifecycle, pause state, and frame timing; delegates per-frame work
/// to <see cref="FramePipeline"/>, <see cref="InputProcessor"/>, and <see cref="RenderCoordinator"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmulationThread
{
    [Flags]
    public enum PauseReasons
    {
        None       = 0,
        Menu       = 1,
        Overlay    = 2,
        FocusLost  = 4,
        MainMenu   = 8,   // Paused at the pre-game main menu; cleared when the user picks New/Resume
        DeviceLost = 16,  // D3D11 device was lost; cleared after successful reinitialisation
    }

    private readonly IEmulationCore     _core;
    private readonly AppConfig          _config;
    private readonly IInputReader       _input;
    private readonly IAudioSink         _audio;
    private readonly ISaveManager       _saveStates;
    private readonly InGameMenu         _menu;
    private readonly Action<Action>          _marshalToUiThread;
    private readonly Action?            _afterFramePresented;
    private readonly Action?            _onInGameMenuOpened;
    private readonly Action?            _onInGameMenuClosed;

    private readonly InputProcessor     _inputProcessor;
    private readonly RenderCoordinator  _renderCoordinator;
    private readonly FramePipeline      _framePipeline;

    private readonly ManualResetEventSlim _resumeEvent = new(initialState: true);
    private volatile int _pauseReasonBits = 0;
    private volatile bool _stopRequested;

    private Thread? _thread;

    public float CurrentFps => _framePipeline.CurrentFps;

    public PauseReasons ActivePauseReasons => (PauseReasons)_pauseReasonBits;
    public bool IsPaused => _pauseReasonBits != 0;

    public EmulationThread(
        IEmulationCore      host,
        AppConfig           config,
        IInputReader        input,
        IAudioSink          audio,
        FrameBuffer         frameBuffer,
        Action<Action>          marshalToUiThread,
        IMenuInputTarget    menuInput,
        ISaveManager        saveStates,
        InGameMenu          menu,
        IFrameRenderer      renderer,
        Achievements.AchievementManager? achievements        = null,
        Action?             afterFramePresented = null,
        Action?             onInGameMenuOpened  = null,
        Action?             onInGameMenuClosed  = null)
    {
        _core                = host;
        _config              = config;
        _input               = input;
        _audio               = audio;
        _saveStates          = saveStates;
        _menu                = menu;
        _marshalToUiThread   = marshalToUiThread;
        _afterFramePresented = afterFramePresented;
        _onInGameMenuOpened  = onInGameMenuOpened;
        _onInGameMenuClosed  = onInGameMenuClosed;

        _inputProcessor    = new InputProcessor(input, menu, menuInput, marshalToUiThread);
        _renderCoordinator = new RenderCoordinator(frameBuffer, renderer, marshalToUiThread);
        _framePipeline     = new FramePipeline(host, audio, new AchievementProcessor(achievements),
                                               _renderCoordinator, saveStates);

        _menu.Opened += () =>
        {
            _saveStates.AutoSave();
            SetPauseReason(PauseReasons.Menu, true);
            if (_onInGameMenuOpened != null)
                _marshalToUiThread(_onInGameMenuOpened);
        };
        _menu.Closed += () =>
        {
            SetPauseReason(PauseReasons.Menu, false);
            if (_onInGameMenuClosed != null)
                _marshalToUiThread(_onInGameMenuClosed);
        };

        _input.HotkeyFired         += HandleHotkeyAction;
        _input.MenuToggleRequested += HandleMenuToggle;
        _input.GamepadDisconnected += HandleGamepadDisconnected;
    }

    /// <summary>
    /// Replaces the renderer after device loss recovery.
    /// Must only be called on the UI thread while the emulation thread is blocked on
    /// <see cref="PauseReasons.DeviceLost"/> — the ManualResetEventSlim barrier guarantees visibility.
    /// </summary>
    internal void UpdateRenderer(IFrameRenderer renderer) => _renderCoordinator.UpdateRenderer(renderer);

    public void SetPauseReason(PauseReasons reason, bool active)
    {
        int prev, next;
        do
        {
            prev = _pauseReasonBits;
            next = active
                ? prev | (int)reason
                : prev & ~(int)reason;
        } while (Interlocked.CompareExchange(ref _pauseReasonBits, next, prev) != prev);

        if (next != prev)
        {
            if (next == 0)
                Logger.Log("[Emulation] Resumed — all pause reasons cleared.");
            else
                Logger.Log($"[Emulation] Paused — active reasons: {(PauseReasons)next}.");
        }

        if (next == 0)
        {
            _audio.SetPaused(false);
            _resumeEvent.Set();
        }
        else
        {
            _audio.SetPaused(true);
            _resumeEvent.Reset();
        }
    }

    public void Start()
    {
        Logger.Log("[Emulation] Thread starting.");
        _stopRequested = false;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Priority     = ThreadPriority.AboveNormal,
            Name         = "EmulationThread",
        };
        _thread.Start();
    }

    public void Stop()
    {
        Logger.Log("[Emulation] Thread stopping.");
        _stopRequested = true;
        _resumeEvent.Set(); // Unblock if paused so thread can exit
        _thread?.Join(2000);
        Logger.Log("[Emulation] Thread stopped.");
    }

    private void Loop()
    {
        long ticksPerFrame = (long)((double)Stopwatch.Frequency
            * _core.VsyncDenominator / _core.VsyncNumerator);
        long spinThreshold = Stopwatch.Frequency / 1000;

        // frameStart tracks the absolute deadline grid. Each frame's target is
        // frameStart + ticksPerFrame; after the wait we advance frameStart to that
        // target so overhead (input poll, hotkeys, etc.) is absorbed within the
        // budget rather than pushing deadlines later every frame.
        // Reset to Stopwatch.GetTimestamp() after any pause so a stale target
        // (possibly far in the past) does not cause a burst of catch-up frames.
        long frameStart = Stopwatch.GetTimestamp();

        while (!_stopRequested)
        {
            // 1. Poll input
            var snapshot = _inputProcessor.Poll(_config);
            // (GamepadDisconnected event may fire here → HandleGamepadDisconnected opens overlay)

            // 2. Dismiss disconnect overlay on any input — must run before AdvanceHotkeys
            // so IsAnyInputJustPressed uses the previous frame's edge state.
            bool isMainMenuActive = (_pauseReasonBits & (int)PauseReasons.MainMenu) != 0;
            _inputProcessor.TryDismissDisconnectScreen(isMainMenuActive);

            // 3. Detect edge-triggered hotkeys; events fire into HandleHotkeyAction / HandleMenuToggle
            _inputProcessor.AdvanceHotkeys(_config);

            // 4. Pause check — block here while paused, polling gamepad for menu input
            // (Steam callbacks are ticked on the UI thread via NEShimApp.OnIdle)
            if (IsPaused)
            {
                _inputProcessor.PollPausedMenuInput(_config);

                // Wait up to 16ms (~60fps menu poll). Returns early if unpaused or stopped.
                _resumeEvent.Wait(16);
                if (_stopRequested) break;

                // Reset the deadline grid so the first frame after resume targets
                // "now + one frame" rather than a target that may be far in the past.
                frameStart = Stopwatch.GetTimestamp();
                continue;
            }

            // 5. Run one frame through the pipeline: core, achievements, auto-save, render, audio
            _framePipeline.RunFrame(snapshot, _config, _afterFramePresented);

            // 6. Frame timing — coarse sleep then spin
            long target = frameStart + ticksPerFrame;
            long remaining = target - Stopwatch.GetTimestamp();
            if (remaining > spinThreshold * 2)
            {
                long sleepUntil = target - spinThreshold;
                while (Stopwatch.GetTimestamp() < sleepUntil)
                    Thread.Sleep(1);
            }
            while (Stopwatch.GetTimestamp() < target)
                Thread.SpinWait(10);

            // Advance the deadline grid by exactly one frame. This keeps input poll
            // time and other per-loop overhead inside the budget. If this frame ran
            // late (target is already past), the next frame's deadline is still at
            // the correct absolute time, naturally absorbing the slip.
            frameStart = target;
        }
    }

    /// <summary>Hard-resets the emulated NES (called via in-game menu).</summary>
    public void ResetGame()
    {
        Logger.Log("[Emulation] Game reset requested.");
        _core.Reset();
    }

    /// <summary>
    /// Called when the user picks New Game or Resume from the main menu.
    /// The save (if any) has already been loaded by MainMenuScreen before this is called.
    /// Safe to call from the UI thread while the thread is blocked on MainMenu.
    /// </summary>
    public void DismissMainMenu()
    {
        Logger.Log("[Emulation] Main menu dismissed — starting gameplay.");
        SetPauseReason(PauseReasons.MainMenu, false);
    }

    // ── Input event handlers (subscribed in constructor) ───────────────────────

    private void HandleGamepadDisconnected()
    {
        if ((_pauseReasonBits & (int)PauseReasons.MainMenu) != 0) return;
        if (_menu.IsOpen) return;

        Logger.Log("[Emulation] Controller disconnected — opening disconnect screen.");
        _menu.Open(Screen.ControllerDisconnected);
    }

    private void HandleMenuToggle()
    {
        if ((_pauseReasonBits & (int)PauseReasons.MainMenu) != 0) return;
        // If the disconnect overlay was dismissed this frame by user input, suppress
        // the simultaneous toggle so pressing Esc/Start doesn't immediately open the menu.
        if (_inputProcessor.JustDismissedDisconnectScreen) return;
        if (_menu.IsOpen && _menu.Current == Screen.ControllerDisconnected) return;

        if (_menu.IsOpen)
        {
            // Don't close while rebinding — the Start press will be surfaced as a
            // "reserved" toast by PollAnyGamepadButtonPressed instead.
            if (!_menu.IsGamepadRebinding)
            {
                Logger.Log("[Emulation] Hotkey: in-game menu closed.");
                _menu.Close();
            }
        }
        else
        {
            Logger.Log("[Emulation] Hotkey: in-game menu opened.");
            _menu.Open();
        }
    }

    private void HandleHotkeyAction(string action)
    {
        if ((_pauseReasonBits & (int)PauseReasons.MainMenu) != 0) return;
        if (_menu.IsOpen) return; // Menu consumes all hotkeys

        switch (action)
        {
            case "SaveActiveSlot":
                Logger.Log($"[Emulation] Hotkey: save slot {_saveStates.ActiveSlot + 1}.");
                _saveStates.SaveToActiveSlot();
                _renderCoordinator.ShowToast(string.Format(_menu.Localization.ToastSavedToSlot, _saveStates.ActiveSlot + 1));
                break;

            case "LoadActiveSlot":
                Logger.Log($"[Emulation] Hotkey: load slot {_saveStates.ActiveSlot + 1}.");
                bool loaded = _saveStates.LoadFromActiveSlot();
                _renderCoordinator.ShowToast(string.Format(loaded
                    ? _menu.Localization.ToastLoadedSlot
                    : _menu.Localization.ToastSlotEmpty, _saveStates.ActiveSlot + 1));
                break;

            default:
                // Slot selection: SelectSlot1 … SelectSlot8
                for (int i = 0; i < 8; i++)
                {
                    if (action == $"SelectSlot{i + 1}")
                    {
                        Logger.Log($"[Emulation] Hotkey: select slot {i + 1}.");
                        _saveStates.ActiveSlot = _config.ActiveSlot = i;
                        int slot = i;
                        _renderCoordinator.ShowToast(string.Format(_menu.Localization.ToastSlotSelected, slot + 1));
                        break;
                    }
                }
                break;
        }
        // Window mode toggle handled by NEShimApp.SetWindowMode via F11 key event
    }
}
