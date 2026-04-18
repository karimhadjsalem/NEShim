using System.Diagnostics;
using System.Threading;
using NEShim.Achievements;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.Input;
using NEShim.Rendering;
using NEShim.Saves;
using NEShim.Steam;
using NEShim.UI;

namespace NEShim.GameLoop;

/// <summary>
/// Runs the NES emulation loop on a dedicated high-priority thread at ~60Hz.
/// Owns frame timing, pause logic, hotkey dispatch, audio submission, and Steam ticks.
/// </summary>
internal sealed class EmulationThread
{
    [Flags]
    public enum PauseReasons
    {
        None      = 0,
        Menu      = 1,
        Overlay   = 2,
        FocusLost = 4,
        MainMenu  = 8,   // Paused at the pre-game main menu; cleared when the user picks New/Resume
    }

    private readonly EmulatorHost      _host;
    private readonly AppConfig         _config;
    private readonly InputManager      _input;
    private readonly AudioPlayer       _audio;
    private readonly FrameBuffer       _frameBuffer;
    private readonly GamePanel         _gamePanel;
    private readonly SaveStateManager  _saveStates;
    private readonly InGameMenu        _menu;
    private readonly AchievementManager? _achievements;

    private readonly ManualResetEventSlim _resumeEvent = new(initialState: true);
    private volatile int _pauseReasonBits = 0;
    private volatile bool _stopRequested;

    private Thread? _thread;

    // FPS counter
    private int _frameCount;
    private long _fpsTimestamp;

    public float CurrentFps { get; private set; }

    public PauseReasons ActivePauseReasons => (PauseReasons)_pauseReasonBits;
    public bool IsPaused => _pauseReasonBits != 0;

    public EmulationThread(
        EmulatorHost       host,
        AppConfig          config,
        InputManager       input,
        AudioPlayer        audio,
        FrameBuffer        frameBuffer,
        GamePanel          gamePanel,
        SaveStateManager   saveStates,
        InGameMenu         menu,
        AchievementManager? achievements = null)
    {
        _host         = host;
        _config       = config;
        _input        = input;
        _audio        = audio;
        _frameBuffer  = frameBuffer;
        _gamePanel    = gamePanel;
        _saveStates   = saveStates;
        _menu         = menu;
        _achievements = achievements;

        // Wire menu events to pause/resume and Steam Input action set switches
        _menu.Opened += () =>
        {
            SetPauseReason(PauseReasons.Menu, true);
            Steam.SteamInputManager.ActivateMenuSet();
        };
        _menu.Closed += () =>
        {
            SetPauseReason(PauseReasons.Menu, false);
            Steam.SteamInputManager.ActivateGameplaySet();
        };
    }

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
        _stopRequested = true;
        _resumeEvent.Set(); // Unblock if paused so thread can exit
        _thread?.Join(2000);
    }

    private void Loop()
    {
        long ticksPerFrame = (long)((double)Stopwatch.Frequency
            * _host.VsyncDenominator / _host.VsyncNumerator);
        // Spin for the last ~1ms to achieve precise timing
        long spinThreshold = Stopwatch.Frequency / 1000;

        _fpsTimestamp = Stopwatch.GetTimestamp();

        while (!_stopRequested)
        {
            // 1. Poll input
            var snapshot = _input.PollSnapshot(_config);
            _host.Controller.Update(snapshot);

            // 2. Handle edge-triggered hotkeys
            HandleHotkeys();
            _input.AdvanceHotkeyState();

            // 3. Steam callbacks
            SteamManager.Tick();

            // 4. Pause check — block here while paused, polling gamepad for menu input
            if (IsPaused)
            {
                if (_gamePanel.IsWaitingForGamepadButton)
                {
                    // Rebind mode: capture any newly pressed button
                    string? btn = _input.PollAnyGamepadButtonPressed();
                    if (btn != null)
                        _gamePanel.BeginInvoke(() => { _gamePanel.HandleGamepadButtonPress(btn); _gamePanel.Invalidate(); });
                }
                else
                {
                    // Normal menu navigation
                    var nav = _input.PollMenuNav(_config);
                    if (nav.Any)
                        _gamePanel.BeginInvoke(() => { _gamePanel.HandleGamepadNav(nav); _gamePanel.Invalidate(); });
                }

                // Wait up to 16ms (~60fps menu poll). Returns early if unpaused or stopped.
                _resumeEvent.Wait(16);
                if (_stopRequested) break;
                continue;
            }

            // Frame start is measured after the pause gate so a freshly-resumed
            // frame doesn't inherit a stale start time that puts target in the past.
            long frameStart = Stopwatch.GetTimestamp();

            // 5. Emulate one frame
            _host.RunFrame();

            // 5a. Check achievement triggers against the post-frame memory state
            _achievements?.Tick();

            // 6. Copy video to front buffer
            var videoBuffer = _host.Video.GetVideoBuffer();
            _frameBuffer.WriteBack(videoBuffer, _host.Video.BufferWidth, _host.Video.BufferHeight);
            _frameBuffer.Swap();

            // 7. Push FPS state into panel then notify UI to repaint (non-blocking)
            _gamePanel.ShowFps   = _config.ShowFps;
            _gamePanel.CurrentFps = CurrentFps;
            _gamePanel.BeginInvoke(_gamePanel.UpdateFrame);

            // 8. Submit audio
            _host.Sound.GetSamplesSync(out short[] samples, out int nsamp);
            _audio.Enqueue(samples, nsamp);

            // 9. FPS tracking
            _frameCount++;
            long now = Stopwatch.GetTimestamp();
            if (now - _fpsTimestamp >= Stopwatch.Frequency)
            {
                CurrentFps    = (float)_frameCount * Stopwatch.Frequency / (now - _fpsTimestamp);
                _frameCount   = 0;
                _fpsTimestamp = now;
            }

            // 10. Frame timing — coarse sleep then spin
            long target = frameStart + ticksPerFrame;
            long remaining = target - Stopwatch.GetTimestamp();
            if (remaining > spinThreshold * 2)
            {
                // Sleep for the bulk of the frame (1ms at a time)
                long sleepUntil = target - spinThreshold;
                while (Stopwatch.GetTimestamp() < sleepUntil)
                    Thread.Sleep(1);
            }
            // Spin for the last ~1ms for precision
            while (Stopwatch.GetTimestamp() < target)
                Thread.SpinWait(10);
        }
    }

    /// <summary>Hard-resets the emulated NES (called via in-game menu).</summary>
    public void ResetGame() => _host.Reset();

    /// <summary>
    /// Called when the user picks New Game or Resume from the main menu.
    /// The save (if any) has already been loaded by MainMenuScreen before this is called.
    /// Safe to call from the UI thread while the thread is blocked on MainMenu.
    /// </summary>
    public void DismissMainMenu()
    {
        Steam.SteamInputManager.ActivateGameplaySet();
        SetPauseReason(PauseReasons.MainMenu, false);
    }

    private void HandleHotkeys()
    {
        // Don't process in-game hotkeys while the pre-game main menu is visible
        if ((_pauseReasonBits & (int)PauseReasons.MainMenu) != 0) return;

        // Open/close menu — Escape (system-reserved), gamepad Start (always reserved),
        // or the configured gamepad hotkey (left bumper by default)
        bool openMenuPressed = _input.IsEscJustPressed()
            || _input.IsGamepadHotkeyJustPressed("OpenMenu", _config)
            || _input.IsGamepadStartJustPressed();
        if (openMenuPressed)
        {
            if (_menu.IsOpen)
            {
                // Don't close while rebinding — the Start press will be picked up by
                // PollAnyGamepadButtonPressed and surfaced as a "reserved" toast instead.
                if (!_menu.IsGamepadRebinding)
                    _menu.Close();
            }
            else
            {
                _menu.Open(_frameBuffer.CaptureFront());
                // Menu is now open and emulation will pause — trigger a repaint
                // so the overlay appears immediately rather than waiting for the next frame.
                _gamePanel.BeginInvoke(_gamePanel.Invalidate);
            }
            return; // Skip other hotkeys if menu just toggled
        }

        if (_menu.IsOpen) return; // Menu consumes other hotkeys

        // Save/load active slot
        if (_input.IsHotkeyJustPressed("SaveActiveSlot", _config))
        {
            _saveStates.SaveToActiveSlot();
            _gamePanel.BeginInvoke(() =>
                _gamePanel.ShowToast($"Saved to Slot {_saveStates.ActiveSlot + 1}"));
        }

        if (_input.IsHotkeyJustPressed("LoadActiveSlot", _config))
        {
            bool loaded = _saveStates.LoadFromActiveSlot();
            _gamePanel.BeginInvoke(() =>
                _gamePanel.ShowToast(loaded
                    ? $"Loaded Slot {_saveStates.ActiveSlot + 1}"
                    : $"Slot {_saveStates.ActiveSlot + 1} — Empty"));
        }

        // Slot selection F1–F8
        for (int i = 0; i < 8; i++)
        {
            string action = $"SelectSlot{i + 1}";
            if (_input.IsHotkeyJustPressed(action, _config))
            {
                _saveStates.ActiveSlot = _config.ActiveSlot = i;
                int slot = i; // capture
                _gamePanel.BeginInvoke(() =>
                    _gamePanel.ShowToast($"Slot {slot + 1} Selected"));
                break;
            }
        }

        // Window mode toggle handled by MainForm via F11
    }
}
