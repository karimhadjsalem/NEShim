using NEShim.Achievements;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.GameLoop;
using NEShim.Input;
using NEShim.Platform;
using NEShim.Rendering;
using NEShim.Saves;
using NEShim.Steam;
using NEShim.Localization;
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

    // ---- Steam ----
    // Steam callbacks (RunCallbacks) must be dispatched on the same thread that
    // called SteamAPI.Init(). We initialise on the UI thread, so we also tick
    // on the UI thread via this timer rather than from the emulation thread.
    private System.Windows.Forms.Timer? _steamTimer;

    // ---- D3D11 overlay hook ----
    // A minimal swap chain on the main window HWND. Steam's GameOverlayRenderer64.dll
    // hooks IDXGISwapChain::Present to enable the overlay and render its UI into the
    // swap chain buffer. GamePanel is hidden while the overlay is active so DWM
    // exposes the swap chain surface rather than compositing GDI+ content above it.
    private Rendering.D3DOverlayHook? _d3dHook;

    private const int SteamCallbackIntervalMs = 16; // ~60 ticks/s

    // Processor instances kept alive so they can be swapped without re-allocation.
    private readonly NesFilterProcessor     _nesFilterProcessor     = new();
    private readonly SoundScrubberProcessor _soundScrubberProcessor = new();

    // ---- Logo splash screen ----
    private AchievementManager?         _pendingAchievements;
    private LogoScreen?                  _logoScreen;
    private System.Windows.Forms.Timer? _logoTimer;

    // ---- Assets preloaded during logo display ----
    private Task?          _preloadTask;
    private Bitmap?        _preloadedMenuBackground;
    private MainMenuMusic? _preloadedMusic;

    // Scaler instances kept alive for zero-allocation runtime swaps.
    private readonly IGraphicsScaler _nearestScaler  = new NearestNeighborScaler();
    private readonly IGraphicsScaler _bilinearScaler = new BilinearScaler();

    private bool _isFullscreen = true;

    public MainForm()
    {
        InitializeComponent();

        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("NEShim.icon.ico");
        if (stream is not null)
            Icon = new Icon(stream);

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
        InitializeConfig();
        _pendingAchievements = InitializeEmulatorCore();
        InitializeSaveSystems();
        InitializeRendering();
        InitializeInput();
        InitializeAudio();
        InitializeWindowAndD3DHook();
        if (_config!.NoLogo)
            FinishInitialization();
        else
            ShowLogo();
    }

    private void InitializeWindowAndD3DHook()
    {
        SetWindowMode(_config!.WindowMode.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase));
        _d3dHook = new Rendering.D3DOverlayHook();
        _d3dHook.Initialize(Handle, Width, Height);
        Logger.Log($"[Init] D3D overlay hook initialised ({Width}×{Height}).");
        Resize += (_, _) => _d3dHook?.Resize(Width, Height);
    }

    private void ShowLogo()
    {
        using var stream = typeof(UI.LogoScreen).Assembly
            .GetManifestResourceStream("NEShim.logos.neshim-logo-splash.png");
        if (stream is null)
        {
            Logger.Log("[Logo] Embedded resource not found — skipping splash screen.");
            FinishInitialization();
            return;
        }
        _logoScreen = new UI.LogoScreen(new System.Drawing.Bitmap(stream));
        _gamePanel!.SetLogoScreen(_logoScreen);
        _logoTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _logoTimer.Tick += OnLogoTick;
        _logoTimer.Start();
        _preloadTask = Task.Run(PreloadAssets);
    }

    private void PreloadAssets()
    {
        if (!string.IsNullOrWhiteSpace(_config!.MainMenuBackgroundPath))
        {
            string? resolved = UI.MainMenuScreen.ResolveAssetPath(_config.MainMenuBackgroundPath);
            if (resolved != null)
            {
                try { _preloadedMenuBackground = new Bitmap(resolved); }
                catch { }
            }
        }

        if (_config.MainMenuMusicEnabled && !string.IsNullOrWhiteSpace(_config.MainMenuMusicPath))
        {
            string? resolved = UI.MainMenuScreen.ResolveAssetPath(_config.MainMenuMusicPath);
            if (resolved != null)
            {
                try { _preloadedMusic = new MainMenuMusic(resolved, autoStart: false); }
                catch { }
            }
        }
    }

    private void OnLogoTick(object? sender, EventArgs e)
    {
        _gamePanel?.Invalidate();
        if (_logoScreen?.IsComplete != true) return;
        _logoTimer!.Stop();
        _logoTimer.Dispose();
        _logoTimer = null;
        _gamePanel?.Refresh(); // synchronous repaint — ensures the alpha=0 frame is visible before clearing
        FinishInitialization();
    }

    private void SkipLogo()
    {
        _logoTimer?.Stop();
        _logoTimer?.Dispose();
        _logoTimer = null;
        FinishInitialization();
    }

    private void FinishInitialization()
    {
        _gamePanel!.SetLogoScreen(null);
        _logoScreen?.Dispose();
        _logoScreen = null;
        _preloadTask?.Wait();
        _preloadTask = null;
        var localization = InitializeSteamAndLocalization();
        InitializeMainMenu(localization);
        InitializeInGameMenu(localization);
        InitializeEmulationStartup(_pendingAchievements);
    }

    private void InitializeConfig()
    {
        _config = ConfigLoader.Load();
        this.Text = _config.WindowTitle;

        if (_config.EnableLogging)
            Logger.Enable();

        Logger.Log($"[Init] Config loaded — ROM: {_config.RomPath}, window: {_config.WindowTitle}, language: {_config.Language}.");
    }

    private AchievementManager? InitializeEmulatorCore()
    {
        string romPath = Path.IsPathRooted(_config!.RomPath)
            ? _config.RomPath
            : Path.Combine(AppContext.BaseDirectory, _config.RomPath);

        if (!File.Exists(romPath))
            throw new FileNotFoundException($"ROM not found: {romPath}");

        Logger.Log($"[Init] ROM found: {romPath}.");

        _host = EmulatorHost.Load(romPath, _config);
        Logger.Log($"[Init] Emulator core loaded — ROM hash: {_host.RomHash}.");

        if (_host.MemoryDomains is null)
        {
            Logger.Log("[Achievements] MemoryDomains unavailable — achievement manager not created.");
            return null;
        }

        var achConfig = AchievementConfigLoader.Load(_host.RomHash, _config.AchievementPublicKey);
        if (achConfig is null)
        {
            Logger.Log("[Achievements] No valid config loaded — achievement manager not created.");
            return null;
        }

        // Marshal achievement unlocks to the UI thread — all Steam API calls must be on
        // the same thread that called SteamAPI.Init().
        var achievements = new AchievementManager(
            _host.MemoryDomains, achConfig,
            () => SteamManager.StatsReady,
            id => BeginInvoke(() =>
            {
                if (SteamManager.UnlockAchievement(id))
                {
                    string name = SteamManager.GetAchievementDisplayName(id) ?? id;
                    _gamePanel?.ShowAchievementNotification(name);
                }
            }));
        Logger.Log("[Achievements] Manager created — triggers active.");
        return achievements;
    }

    private void InitializeSaveSystems()
    {
        _saveRam = new SaveRamManager((BizHawk.Emulation.Common.ISaveRam)_host!.SaveRam,
            Path.IsPathRooted(_config!.SaveRamPath)
                ? _config.SaveRamPath
                : Path.Combine(AppContext.BaseDirectory, _config.SaveRamPath));
        _saveRam.LoadFromDisk();

        string stateDir = Path.IsPathRooted(_config.SaveStateDirectory)
            ? _config.SaveStateDirectory
            : Path.Combine(AppContext.BaseDirectory, _config.SaveStateDirectory);
        _saveStates = new SaveStateManager(_host.States, stateDir);
        _saveStates.ActiveSlot = _config.ActiveSlot;
        Logger.Log($"[Init] Save state directory: {stateDir} (active slot: {_config.ActiveSlot + 1})");
    }

    private void InitializeRendering()
    {
        _frameBuffer = new FrameBuffer();
        _gamePanel   = new GamePanel(_frameBuffer) { Dock = DockStyle.Fill };
        _gamePanel.SetScaler(_config!.GraphicsSmoothingEnabled ? _bilinearScaler : _nearestScaler);
        _gamePanel.SetSidebars(
            LoadSidebarBitmap(_config.SidebarLeftPath),
            LoadSidebarBitmap(_config.SidebarRightPath));
        Controls.Add(_gamePanel);
    }

    private void InitializeInput()
    {
        _input = new InputManager();
        _gamePanel!.KeyDown += (_, e) => _input.OnKeyDown(e.KeyCode);
        _gamePanel.KeyUp    += (_, e) => _input.OnKeyUp(e.KeyCode);
        KeyDown += OnFormKeyDown;
    }

    private void InitializeAudio()
    {
        IAudioProcessor startingProcessor = _config!.SoundScrubberEnabled
            ? _soundScrubberProcessor
            : _nesFilterProcessor;
        Logger.Log($"[Init] Audio: buffer={_config.AudioBufferFrames} frames, processor={startingProcessor.GetType().Name}, volume={_config.Volume}%");
        _audio = new AudioPlayer(_config.AudioBufferFrames, startingProcessor);
        _audio.SetVolume(_config.Volume / 100f);
    }

    private LocalizationData InitializeSteamAndLocalization()
    {
        SteamManager.Initialize(overlayActive =>
        {
            Logger.Log($"[Steam] Overlay toggle received — active={overlayActive}. Setting GamePanel.Visible={!overlayActive}.");
            _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.Overlay, overlayActive);
            if (_gamePanel is not null)
            {
                _gamePanel.Visible = !overlayActive;
                Logger.Log($"[Steam] GamePanel.Visible is now {_gamePanel.Visible}.");
            }
            else
            {
                Logger.Log("[Steam] GamePanel is null — overlay visibility toggle skipped.");
            }
        });
        if (PlatformDetector.IsWine)
            Logger.Log("[Platform] Wine/Proton detected.");
        if (PlatformDetector.IsSteamDeck)
            Logger.Log("[Platform] Steam Deck hardware detected.");
        return LoadLocalization();
    }

    private void InitializeMainMenu(LocalizationData localization)
    {
        _mainMenuScreen = new MainMenuScreen(
            saveStates:          _saveStates!,
            config:              _config!,
            localization:        localization,
            bgImagePath:         _preloadedMenuBackground is null ? _config!.MainMenuBackgroundPath : null,
            bgImage:             _preloadedMenuBackground,
            onWindowModeToggle:  fullscreen => BeginInvoke(() => SetWindowMode(fullscreen)),
            onConfigSaved:       () => { /* config flushed to disk on exit */ },
            onVolumeChanged:     vol =>
            {
                _audio?.SetVolume(vol / 100f);
                _mainMenuMusic?.SetMasterVolume(vol / 100f);
            },
            onScrubberToggled: on => _audio?.SetProcessor(on ? _soundScrubberProcessor : _nesFilterProcessor),
            onMenuMusicToggled: on =>
            {
                if (on)
                {
                    if (_mainMenuMusic == null)
                    {
                        _mainMenuMusic = CreateMainMenuMusic(_config!);
                        _mainMenuMusic?.SetMasterVolume(_config!.Volume / 100f);
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

        _preloadedMenuBackground = null; // ownership transferred to MainMenuScreen

        _mainMenuScreen.NewGameChosen += () => BeginInvoke(() =>
        {
            _mainMenuMusic?.FadeOut();
            _emulationThread?.DismissMainMenu();
            _gamePanel?.Invalidate();
        });
        _mainMenuScreen.ResumeChosen += () => BeginInvoke(() =>
        {
            // Save was already loaded by MainMenuScreen while thread was blocked
            _mainMenuMusic?.FadeOut();
            _emulationThread?.DismissMainMenu();
            _gamePanel?.Invalidate();
        });
        _mainMenuScreen.ExitChosen += () => BeginInvoke(() =>
        {
            _mainMenuMusic?.Stop();
            Application.Exit();
        });

        _gamePanel!.SetMainMenu(_mainMenuScreen);

        if (_config!.MainMenuMusicEnabled)
        {
            if (_preloadedMusic is not null)
            {
                _mainMenuMusic = _preloadedMusic;
                _preloadedMusic = null;
                _mainMenuMusic.SetMasterVolume(_config.Volume / 100f);
                _mainMenuMusic.FadeIn();
            }
            else
            {
                _mainMenuMusic = CreateMainMenuMusic(_config);
                _mainMenuMusic?.SetMasterVolume(_config.Volume / 100f);
            }
        }
    }

    private void InitializeInGameMenu(LocalizationData localization)
    {
        _menu = new InGameMenu(
            saveStates:          _saveStates!,
            config:              _config!,
            localization:        localization,
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
            onScrubberToggled:       on => _audio?.SetProcessor(on ? _soundScrubberProcessor : _nesFilterProcessor),
            onGraphicsScalerToggled: on =>
            {
                _gamePanel?.SetScaler(on ? _bilinearScaler : _nearestScaler);
                ConfigLoader.Save(_config!);
            });
        _gamePanel!.SetMenu(_menu);
    }

    private void InitializeEmulationStartup(AchievementManager? achievements)
    {
        _emulationThread = new EmulationThread(
            _host!, _config!, _input!, _audio!, _frameBuffer!, _gamePanel!, _saveStates!, _menu!,
            achievements);

        // Tick Steam callbacks on the UI thread (~60fps). Steam requires RunCallbacks()
        // to be called on the same thread as SteamAPI.Init().
        _steamTimer = new System.Windows.Forms.Timer { Interval = SteamCallbackIntervalMs };
        _steamTimer.Tick += (_, _) => { SteamManager.Tick(); _d3dHook?.Present(); };
        _steamTimer.Start();

        _audio!.Start(_config!.AudioDevice);
        _emulationThread.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        _gamePanel!.Focus();
        _emulationThread.Start();
        Logger.Log("[Init] Startup complete — showing main menu.");

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

    private string ResolveLanguage()
    {
        string? steamLang = SteamManager.GameLanguage;
        if (!string.IsNullOrEmpty(steamLang))
        {
            Logger.Log($"[Localization] Language resolved from Steam: '{steamLang}'.");
            return steamLang;
        }

        if (SteamManager.IsAvailable)
            Logger.Log("[Localization] Steam is available but returned an empty language — falling through to config.");
        else
            Logger.Log("[Localization] Steam not available — checking config.Language.");

        if (_config is not null
            && !string.IsNullOrEmpty(_config.Language)
            && !_config.Language.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Log($"[Localization] Language resolved from config: '{_config.Language}'.");
            return _config.Language;
        }

        Logger.Log($"[Localization] config.Language is '{_config?.Language ?? "(null)"}' — defaulting to 'english'.");
        return "english";
    }

    private LocalizationData LoadLocalization()
    {
        string language = ResolveLanguage();
        string langDir  = Path.Combine(AppContext.BaseDirectory, "lang");
        Logger.Log($"[Localization] Loading language file from: {langDir}.");
        var data = LocalizationLoader.Load(langDir, language);
        Logger.Log($"[Localization] Loaded — fontFamily='{data.FontFamily}'.");
        return data;
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
        Logger.Log($"[Window] Mode set to {_config.WindowMode}.");
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_logoScreen is not null)
        {
            SkipLogo();
            e.Handled = true;
            return;
        }

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
        Logger.Log("[Shutdown] Form closing — stopping emulation thread.");
        _emulationThread?.Stop();

        // Persist state — only auto-save if the game was actually running
        try
        {
            if (_mainMenuScreen is null || !_mainMenuScreen.IsVisible)
                _saveStates?.AutoSave();
            else
                Logger.Log("[Shutdown] Main menu was visible — skipping auto-save.");

            _saveRam?.SaveToDisk();

            if (_config is not null)
            {
                if (_saveStates is not null)
                    _config.ActiveSlot = _saveStates.ActiveSlot;
                ConfigLoader.Save(_config);
            }
        }
        catch (Exception ex) { Logger.Log($"[Shutdown] Persist error: {ex.Message}"); }

        // Dispose resources
        Logger.Log("[Shutdown] Disposing resources.");
        _logoTimer?.Dispose();
        _logoScreen?.Dispose();
        _preloadedMenuBackground?.Dispose();
        _preloadedMusic?.Dispose();
        _steamTimer?.Dispose();
        _d3dHook?.Dispose();
        _mainMenuMusic?.Dispose();
        _mainMenuScreen?.Dispose();
        _audio?.Dispose();
        _host?.Dispose();
        SteamManager.Shutdown();
        Logger.Log("[Shutdown] Done.");
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
