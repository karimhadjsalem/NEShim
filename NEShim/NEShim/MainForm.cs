using NEShim.Achievements;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.GameLoop;
using NEShim.Input;
using NEShim.Rendering;
using NEShim.Saves;
using NEShim.Steam;
using NEShim.UI;

namespace NEShim;

public partial class MainForm : Form
{
    // ---- Win32 for WM_ACTIVATEAPP ----
    private const int WM_ACTIVATEAPP = 0x001C;

    // ---- Core components ----
    private AppConfig?        _config;
    private EmulatorHost?     _host;
    private InputManager?     _input;
    private AudioPlayer?      _audio;
    private MainMenuMusic?    _mainMenuMusic;
    private SaveStateManager? _saveStates;
    private SaveRamManager?   _saveRam;
    private FrameBuffer?      _frameBuffer;
    private GamePanel?        _gamePanel;
    private MainMenuScreen?   _mainMenuScreen;
    private InGameMenu?       _menu;
    private EmulationThread?  _emulationThread;

    // Processor instances kept alive so they can be swapped without re-allocation.
    private readonly NesFilterProcessor     _nesFilterProcessor     = new();
    private readonly SoundScrubberProcessor _soundScrubberProcessor = new();

    // Scaler instances kept alive for zero-allocation runtime swaps.
    private readonly IGraphicsScaler _nearestScaler  = new NearestNeighborScaler();
    private readonly IGraphicsScaler _bilinearScaler = new BilinearScaler();

    private bool _isFullscreen = true;

    public MainForm()
    {
        InitializeComponent();
        Load += OnFormLoad;
        FormClosing += OnFormClosing;
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            InitializeEmulator();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start emulator:\n\n{ex.Message}",
                "NEShim — Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    private void InitializeEmulator()
    {
        // 1. Config
        _config = ConfigLoader.Load();

        // 2. Load ROM
        string romPath = Path.IsPathRooted(_config.RomPath)
            ? _config.RomPath
            : Path.Combine(AppContext.BaseDirectory, _config.RomPath);

        if (!File.Exists(romPath))
            throw new FileNotFoundException($"ROM not found: {romPath}");

        // 3. Emulator core
        _host = EmulatorHost.Load(romPath, _config);

        // 3a. Per-game achievement config (keyed by ROM SHA1 hash)
        AchievementManager? achievements = null;
        if (_host.MemoryDomains is not null)
        {
            var achConfig = AchievementConfigLoader.Load(_host.RomHash);
            if (achConfig is not null)
                achievements = new AchievementManager(
                    _host.MemoryDomains, achConfig,
                    () => SteamManager.StatsReady,
                    SteamManager.UnlockAchievement);
        }

        // 4. Save RAM (load before first frame)
        _saveRam = new SaveRamManager((BizHawk.Emulation.Common.ISaveRam)_host.SaveRam,
            Path.IsPathRooted(_config.SaveRamPath)
                ? _config.SaveRamPath
                : Path.Combine(AppContext.BaseDirectory, _config.SaveRamPath));
        _saveRam.LoadFromDisk();

        // 5. Save states
        string stateDir = Path.IsPathRooted(_config.SaveStateDirectory)
            ? _config.SaveStateDirectory
            : Path.Combine(AppContext.BaseDirectory, _config.SaveStateDirectory);
        _saveStates = new SaveStateManager(_host.States, stateDir);
        _saveStates.ActiveSlot = _config.ActiveSlot;

        // 6. Rendering
        _frameBuffer = new FrameBuffer();
        _gamePanel   = new GamePanel(_frameBuffer) { Dock = DockStyle.Fill };
        _gamePanel.SetScaler(_config.GraphicsSmoothingEnabled ? _bilinearScaler : _nearestScaler);
        _gamePanel.SetSidebars(
            LoadSidebarBitmap(_config.SidebarLeftPath),
            LoadSidebarBitmap(_config.SidebarRightPath));
        Controls.Add(_gamePanel);

        // 7. Input
        _input = new InputManager();
        _gamePanel.KeyDown += (_, e) => _input.OnKeyDown(e.KeyCode);
        _gamePanel.KeyUp   += (_, e) => _input.OnKeyUp(e.KeyCode);
        KeyDown += OnFormKeyDown;

        // 8. Audio
        IAudioProcessor startingProcessor = _config.SoundScrubberEnabled
            ? _soundScrubberProcessor
            : _nesFilterProcessor;
        _audio = new AudioPlayer(_config.AudioBufferFrames, startingProcessor);
        _audio.SetVolume(_config.Volume / 100f);

        // 9. Pre-game main menu
        _mainMenuScreen = new MainMenuScreen(
            saveStates:          _saveStates,
            config:              _config,
            bgImagePath:         _config.MainMenuBackgroundPath,
            onWindowModeToggle:  fullscreen => BeginInvoke(() => SetWindowMode(fullscreen)),
            onConfigSaved:       () => { /* config flushed to disk on exit */ },
            onVolumeChanged:     vol =>
            {
                _audio?.SetVolume(vol / 100f);
                _mainMenuMusic?.SetMasterVolume(vol / 100f);
            },
            onScrubberToggled:          on  => _audio?.SetProcessor(on ? _soundScrubberProcessor : _nesFilterProcessor),
            onMenuMusicToggled:         on  =>
            {
                if (on)
                {
                    if (_mainMenuMusic == null)
                    {
                        _mainMenuMusic = CreateMainMenuMusic(_config);
                        _mainMenuMusic?.SetMasterVolume(_config.Volume / 100f);
                    }
                    _mainMenuMusic?.FadeIn();
                }
                else
                {
                    _mainMenuMusic?.Stop();
                }
            },
            onGraphicsScalerToggled: on =>
            {
                _gamePanel?.SetScaler(on ? _bilinearScaler : _nearestScaler);
                ConfigLoader.Save(_config!);
            });

        _mainMenuScreen.NewGameChosen += () => BeginInvoke(() =>
        {
            _mainMenuMusic?.FadeOut();
            _emulationThread?.DismissMainMenu();
            _gamePanel?.Invalidate();
        });
        _mainMenuScreen.ResumeChosen += () => BeginInvoke(() =>
        {
            // Save was already loaded by MainMenuScreen.Activate() while thread was blocked
            _mainMenuMusic?.FadeOut();
            _emulationThread?.DismissMainMenu();
            _gamePanel?.Invalidate();
        });
        _mainMenuScreen.ExitChosen += () => BeginInvoke(() =>
        {
            _mainMenuMusic?.Stop();
            Application.Exit();
        });

        _gamePanel.SetMainMenu(_mainMenuScreen);

        // 9a. Main menu music — only created if the path is set and music is enabled
        if (_config.MainMenuMusicEnabled)
        {
            _mainMenuMusic = CreateMainMenuMusic(_config);
            _mainMenuMusic?.SetMasterVolume(_config.Volume / 100f);
        }

        // 10. In-game pause menu
        _menu = new InGameMenu(
            saveStates:          _saveStates,
            config:              _config,
            onExitToDesktop:     () => BeginInvoke(Application.Exit),
            onResetGame:         () => _emulationThread?.ResetGame(),
            onReturnToMainMenu:  () => BeginInvoke(ReturnToMainMenu),
            onWindowModeToggle:  fullscreen => BeginInvoke(() => SetWindowMode(fullscreen)),
            onConfigSaved:       () => { /* config flushed to disk on exit */ },
            onVolumeChanged:     vol =>
            {
                _audio?.SetVolume(vol / 100f);
                _mainMenuMusic?.SetMasterVolume(vol / 100f);
            },
            onScrubberToggled:          on => _audio?.SetProcessor(on ? _soundScrubberProcessor : _nesFilterProcessor),
            onGraphicsScalerToggled:    on =>
            {
                _gamePanel?.SetScaler(on ? _bilinearScaler : _nearestScaler);
                ConfigLoader.Save(_config!);
            });
        _gamePanel.SetMenu(_menu);

        // 11. Emulation thread
        _emulationThread = new EmulationThread(
            _host, _config, _input, _audio, _frameBuffer, _gamePanel, _saveStates, _menu,
            achievements);

        // Wire Steam overlay → pause
        SteamManager.Initialize(overlayActive =>
            _emulationThread.SetPauseReason(EmulationThread.PauseReasons.Overlay, overlayActive));

        // 12. Apply window mode
        SetWindowMode(_config.WindowMode.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase));

        // 13. Start audio and emulation — thread starts paused at main menu
        _audio.Start(_config.AudioDevice);
        _emulationThread.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        _gamePanel.Focus();
        _emulationThread.Start();

        // Show main menu immediately
        _gamePanel.Invalidate();
    }

    private void ReturnToMainMenu()
    {
        // Pause emulation under the MainMenu reason before showing the screen,
        // so the emulation thread blocks before the next frame is emulated.
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        SteamManager.ActivateMenuSet();
        _mainMenuScreen?.Show();
        _mainMenuMusic?.FadeIn();
        _gamePanel?.Invalidate();
    }

    private static Bitmap? LoadSidebarBitmap(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string? resolved = UI.MainMenuScreen.ResolveAssetPath(path);
        if (resolved == null) return null;
        try   { return new Bitmap(resolved); }
        catch { return null; }
    }

    private static MainMenuMusic? CreateMainMenuMusic(AppConfig config)
    {
        string path = config.MainMenuMusicPath;
        if (string.IsNullOrWhiteSpace(path)) return null;

        string? resolved = MainMenuScreen.ResolveAssetPath(path);
        if (resolved == null) return null;

        try   { return new MainMenuMusic(resolved); }
        catch { return null; /* bad file or audio device issue — degrade gracefully */ }
    }

    private void SetWindowMode(bool fullscreen)
    {
        _isFullscreen = fullscreen;
        if (fullscreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState     = FormWindowState.Maximized;
        }
        else
        {
            WindowState     = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize      = new Size(768, 672); // ~3× NES with aspect correction
            CenterToScreen();
        }
        _config!.WindowMode = fullscreen ? "Fullscreen" : "Windowed";
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        // Pre-game main menu has priority
        if (_mainMenuScreen?.IsVisible == true)
        {
            if (_mainMenuScreen.HandleKey(e.KeyCode))
            {
                e.Handled = true;
                _gamePanel?.Invalidate();
                return;
            }
        }

        if (_menu is null || _emulationThread is null) return;

        // Pass navigation keys to in-game menu when open
        if (_menu.IsOpen)
        {
            if (_menu.HandleKey(e.KeyCode))
            {
                e.Handled = true;
                _gamePanel?.Invalidate();
                return;
            }
        }

        // F11 window toggle — handled on UI thread
        if (e.KeyCode == Keys.F11)
        {
            SetWindowMode(!_isFullscreen);
            e.Handled = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Stop emulation thread first
        _emulationThread?.Stop();

        // Persist state — only auto-save if the game was actually running
        try
        {
            if (_mainMenuScreen is null || !_mainMenuScreen.IsVisible)
                _saveStates?.AutoSave();

            _saveRam?.SaveToDisk();

            if (_config is not null)
            {
                if (_saveStates is not null)
                    _config.ActiveSlot = _saveStates.ActiveSlot;
                ConfigLoader.Save(_config);
            }
        }
        catch { /* best-effort on shutdown */ }

        // Dispose resources
        _mainMenuMusic?.Dispose();
        _mainMenuScreen?.Dispose();
        _audio?.Dispose();
        _host?.Dispose();
        SteamManager.Shutdown();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ACTIVATEAPP && _emulationThread is not null)
        {
            bool active = m.WParam != IntPtr.Zero;
            _emulationThread.SetPauseReason(EmulationThread.PauseReasons.FocusLost, !active);
        }
        base.WndProc(ref m);
    }
}
