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

public partial class MainForm : Form, Rendering.IMenuSceneProvider, UI.IMenuInputTarget
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
    // swap chain buffer. In D3D11 mode, GamePanel is hidden during gameplay so the swap
    // chain surface is visible. GamePanel is shown only for menus and overlays.
    private Rendering.D3DOverlayHook?  _d3dHook;

    // ---- Renderer (strategy) ----
    // Always non-null after InitializeWindowAndD3DHook completes.
    // RendererFactory selects D3D11Renderer or GdiRenderer based on available hardware.
    private Rendering.IFrameRenderer?  _renderer;

    // Sidebar bitmaps owned by MainForm — passed to the renderer on creation and after device loss recovery.
    private Bitmap? _sidebarLeft;
    private Bitmap? _sidebarRight;

    private const int SteamCallbackIntervalMs = 16; // ~60 ticks/s

    // ---- Logo splash screen ----
    private AchievementManager?         _pendingAchievements;
    private LogoScreen?                  _logoScreen;
    private System.Windows.Forms.Timer? _logoTimer;

    // ---- Assets preloaded during logo display ----
    private Task?          _preloadTask;
    private Bitmap?        _preloadedMenuBackground;
    private MainMenuMusic? _preloadedMusic;

    private bool _isFullscreen = true;
    private bool _gameHasStarted;

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
        Cursor.Hide();
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

        _renderer = Rendering.RendererFactory.Create(_d3dHook, _gamePanel!, 256, 240, _config!.ForceRenderer);
        _renderer.DeviceLost += OnD3DDeviceLost;
        _renderer.SetSidebars(_sidebarLeft, _sidebarRight);
        _renderer.SetMenuSceneProvider(this);

        ApplyRenderingOptions();

        // IFrameRenderer.Resize handles swap chain resize (D3D11) or hook resize (GDI+).
        Resize += (_, _) => _renderer?.Resize(Width, Height);
    }

    private void ApplyRenderingOptions()
    {
        var mode      = Rendering.VideoFilterModeParser.Parse(_config!.VideoFilter);
        var overscan  = Rendering.OverscanModeParser.Parse(_config.OverscanMode);
        var colorMode = Rendering.VideoColorFilterModeParser.Parse(_config.VideoColorFilter);

        var supported = Platform.PlatformDetector.IsD3D11Active
            ? Rendering.VideoFilterModeParser.D3D11Supported
            : Rendering.VideoFilterModeParser.GdiSupported;

        if (!supported.Contains(mode))
        {
            Logger.Log($"[Renderer] VideoFilter '{_config.VideoFilter}' is not supported in " +
                       $"{(Platform.PlatformDetector.IsD3D11Active ? "D3D11" : "GDI+")} mode; " +
                       $"falling back to PixelPerfect.");
            mode = Rendering.VideoFilterMode.PixelPerfect;
            _config.VideoFilter = mode.ToString();
            ConfigLoader.Save(_config);
        }

        if (_renderer is Rendering.D3D11Renderer d3d)
            d3d.InitializeRenderingOptions(Rendering.Filters.D3D11FilterFactory.Create(mode), overscan, colorMode);
        else if (_renderer is Rendering.GdiRenderer gdi)
            gdi.InitializeRenderingOptions(Rendering.Filters.GdiFilterFactory.Create(mode), overscan);
    }

    private void OnD3DDeviceLost(object? sender, EventArgs e)
    {
        // Device loss fires from Tick (DrawAndPresent) on the UI thread — already on UI thread.
        Logger.Log("[Renderer] Recovering from device loss — pausing emulation and reinitialising.");
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.DeviceLost, true);

        _renderer?.Dispose();
        _renderer = null;
        _d3dHook?.Dispose();
        _d3dHook = null;

        _d3dHook = new Rendering.D3DOverlayHook();
        _d3dHook.Initialize(Handle, Width, Height);

        _renderer = Rendering.RendererFactory.Create(_d3dHook, _gamePanel!, 256, 240, _config!.ForceRenderer);
        _renderer.DeviceLost += OnD3DDeviceLost;
        _renderer.SetSidebars(_sidebarLeft, _sidebarRight);
        _renderer.SetMenuSceneProvider(this);
        ApplyRenderingOptions();

        // Update EmulationThread's renderer reference while emulation is still paused.
        // EmulationThread.UpdateRenderer is safe here — ManualResetEventSlim provides the barrier.
        _emulationThread?.UpdateRenderer(_renderer);

        Logger.Log($"[Renderer] Reinitialised — ownsFrameSurface={_renderer.OwnsFrameSurface}. Resuming emulation.");
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.DeviceLost, false);
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
        _renderer?.MarkOverlayDirty();
        _gamePanel?.Invalidate();
        if (_logoScreen?.IsComplete != true) return;
        _logoTimer!.Stop();
        _logoTimer.Dispose();
        _logoTimer = null;
        if (_renderer?.OwnsFrameSurface != true)
            _gamePanel?.Refresh(); // GDI+ only — synchronous repaint for the alpha=0 final frame
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
                    _renderer?.ShowAchievementNotification(name);
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
        _frameBuffer  = new FrameBuffer();
        _gamePanel    = new GamePanel(_frameBuffer) { Dock = DockStyle.Fill };
        _sidebarLeft  = LoadSidebarBitmap(_config!.SidebarLeftPath);
        _sidebarRight = LoadSidebarBitmap(_config.SidebarRightPath);
        Controls.Add(_gamePanel);
    }

    private void InitializeInput()
    {
        _input = new InputManager();
        // KeyPreview = true (set in InitializeComponent) ensures these MainForm handlers
        // fire for all keys regardless of which child control has focus. This also handles
        // emulation input when GamePanel is hidden (D3D11 gameplay mode).
        KeyDown += (_, e) => _input.OnKeyDown(e.KeyCode);
        KeyUp   += (_, e) => _input.OnKeyUp(e.KeyCode);
        KeyDown += OnFormKeyDown;
    }

    private void InitializeAudio()
    {
        var filterMode = AudioFilterModeParser.Parse(_config!.AudioFilter);
        _audio = new AudioPlayer(_config.AudioBufferFrames, CreateProcessor(filterMode));
        _audio.SetVolume(_config.Volume / 100f);
        Logger.Log($"[Init] Audio: buffer={_config.AudioBufferFrames} frames, filter={filterMode}, volume={_config.Volume}%");
    }

    private static IAudioProcessor CreateProcessor(AudioFilterMode mode) => mode switch
    {
        AudioFilterMode.Warm         => new SoundScrubberProcessor(),
        AudioFilterMode.PseudoStereo => new PseudoStereoProcessor(),
        AudioFilterMode.WarmStereo   => new WarmStereoProcessor(),
        AudioFilterMode.Compression  => new CompressionProcessor(),
        AudioFilterMode.BassBoost    => new BassBoostProcessor(),
        AudioFilterMode.Saturation   => new TapeSaturationProcessor(),
        _                            => new NesFilterProcessor(),
    };

    private LocalizationData InitializeSteamAndLocalization()
    {
        SteamManager.Initialize(overlayActive =>
        {
            Logger.Log($"[Steam] Overlay toggle received — active={overlayActive}.");
            _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.Overlay, overlayActive);
            if (overlayActive)
                _mainMenuMusic?.Pause();
            else
                _mainMenuMusic?.Resume();
            UpdateGamePanelVisibility();
            Logger.Log($"[Steam] GamePanel.Visible is now {_gamePanel?.Visible}.");
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
            onFilterChanged: mode => _audio?.SetProcessor(CreateProcessor(mode)),
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
            onVideoFilterChanged: mode =>
            {
                _config!.VideoFilter = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetFilter(Rendering.Filters.D3D11FilterFactory.Create(mode));
                else if (_renderer is Rendering.GdiRenderer gdi)
                    gdi.SetFilter(Rendering.Filters.GdiFilterFactory.Create(mode));
                ConfigLoader.Save(_config);
            },
            onVideoColorFilterChanged: mode =>
            {
                _config!.VideoColorFilter = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetColorFilter(mode);
                ConfigLoader.Save(_config);
            },
            onOverscanModeChanged: overscan =>
            {
                _config!.OverscanMode = overscan.ToString();
                _renderer?.SetOverscanMode(overscan);
                ConfigLoader.Save(_config);
            },
            onLanguageChanged: lang => BeginInvoke(() => OnLanguageChanged(lang)));

        _preloadedMenuBackground = null; // ownership transferred to MainMenuScreen

        _mainMenuScreen.NewGameChosen += () => BeginInvoke(() =>
        {
            _gameHasStarted = true;
            _mainMenuMusic?.FadeOut();
            SteamManager.ActivateGameplaySet();
            _emulationThread?.DismissMainMenu();
            _renderer?.MarkOverlayDirty();
            _gamePanel?.Invalidate();
            UpdateGamePanelVisibility();
        });
        _mainMenuScreen.ResumeChosen += () => BeginInvoke(() =>
        {
            _gameHasStarted = true;
            // Save was already loaded by MainMenuScreen while thread was blocked.
            _mainMenuMusic?.FadeOut();
            SteamManager.ActivateGameplaySet();
            _emulationThread?.DismissMainMenu();
            _renderer?.MarkOverlayDirty();
            _gamePanel?.Invalidate();
            UpdateGamePanelVisibility();
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
            onFilterChanged: mode => _audio?.SetProcessor(CreateProcessor(mode)),
            onVideoFilterChanged: mode =>
            {
                _config!.VideoFilter = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetFilter(Rendering.Filters.D3D11FilterFactory.Create(mode));
                else if (_renderer is Rendering.GdiRenderer gdi)
                    gdi.SetFilter(Rendering.Filters.GdiFilterFactory.Create(mode));
                ConfigLoader.Save(_config);
            },
            onVideoColorFilterChanged: mode =>
            {
                _config!.VideoColorFilter = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetColorFilter(mode);
                ConfigLoader.Save(_config);
            },
            onOverscanModeChanged: overscan =>
            {
                _config!.OverscanMode = overscan.ToString();
                _renderer?.SetOverscanMode(overscan);
                ConfigLoader.Save(_config);
            },
            onLanguageChanged: lang => BeginInvoke(() => OnLanguageChanged(lang)));
        _menu.Opened += () => BeginInvoke(() =>
        {
            _renderer?.MarkOverlayDirty();
            _gamePanel?.Invalidate();
            UpdateGamePanelVisibility();
        });
        _menu.Closed += () => BeginInvoke(() =>
        {
            _renderer?.MarkOverlayDirty();
            _gamePanel?.Invalidate();
            UpdateGamePanelVisibility();
        });

        _gamePanel!.SetMenu(_menu);
    }

    private void InitializeEmulationStartup(AchievementManager? achievements)
    {
        _emulationThread = new EmulationThread(
            _host!, _config!, _input!, _audio!, _frameBuffer!,
            this,   // Control for BeginInvoke (MainForm is a Control)
            this,   // IMenuInputTarget (MainForm implements it)
            _saveStates!, _menu!,
            _renderer!,
            achievements,
            // Dispatch Steam callbacks immediately after each Present. On Steam Deck,
            // Gamescope can block vkQueuePresentKHR while its overlay is showing, which
            // starves WM_TIMER and prevents _steamTimer from calling RunCallbacks().
            // Calling it here ensures GameOverlayActivated_t fires as soon as
            // Present unblocks, so SetPauseReason(Overlay) runs before the next frame.
            afterFramePresented: SteamManager.RunCallbacksAfterPresent,
            onInGameMenuOpened:  SteamManager.ActivateMenuSet,
            onInGameMenuClosed:  SteamManager.ActivateGameplaySet);

        // Steam callbacks must be ticked on the same thread as SteamAPI.Init() (UI thread).
        // During gameplay, Present is driven by the UploadFrame BeginInvoke in EmulationThread
        // so it fires immediately after each frame is ready (tight coupling, no clock drift).
        // The timer drives Present only when the emulation loop is paused, keeping the Steam
        // overlay hook alive without a running emulation loop to supply BeginInvoke calls.
        _steamTimer = new System.Windows.Forms.Timer { Interval = SteamCallbackIntervalMs };
        _steamTimer.Tick += (_, _) =>
        {
            SteamManager.Tick();
            if (_emulationThread?.IsPaused == true)
                _renderer?.Tick(vsync: false);
        };
        _steamTimer.Start();

        _audio!.Start(_config!.AudioDevice);
        _emulationThread.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        Focus(); // MainForm handles all keys via KeyPreview; focus the form directly
        _emulationThread.Start();
        Logger.Log("[Init] Startup complete — showing main menu.");

        _renderer?.MarkOverlayDirty();
        _gamePanel?.Invalidate();
    }

    private void ReturnToMainMenu()
    {
        // Pause emulation under the MainMenu reason before showing the screen,
        // so the emulation thread blocks before the next frame is emulated.
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        SteamManager.ActivateMenuSet();
        _mainMenuScreen?.Show();
        _mainMenuMusic?.FadeIn();
        _renderer?.MarkOverlayDirty();
        _gamePanel?.Invalidate();
        UpdateGamePanelVisibility();
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
        // Explicit config takes priority — user's in-menu choice overrides Steam and OS.
        if (_config is not null
            && !string.IsNullOrEmpty(_config.Language)
            && !_config.Language.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Log($"[Localization] Language explicitly configured: '{_config.Language}'.");
            return _config.Language;
        }

        // Auto mode: Steam → OS culture → English.
        var resolver = new Localization.ChainedLanguageResolver([
            new Localization.SteamLanguageResolver(),
            new Localization.CultureInfoLanguageResolver(),
        ]);
        string? lang = resolver.Resolve();
        if (lang != null) return lang;

        Logger.Log("[Localization] All resolvers returned null — defaulting to 'english'.");
        return "english";
    }

    private void OnLanguageChanged(string _)
    {
        ConfigLoader.Save(_config!);
        var newData = LoadLocalization();
        _menu?.UpdateLocalization(newData);
        _mainMenuScreen?.UpdateLocalization(newData);
        _renderer?.MarkOverlayDirty();
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
            WindowState     = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            var screenBounds = Screen.FromHandle(Handle).Bounds;
            SetBounds(screenBounds.X, screenBounds.Y, screenBounds.Width, screenBounds.Height);
        }
        else
        {
            TopMost         = false;
            WindowState     = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize      = new Size(1024, 672); // wider than NES display aspect to leave room for sidebars
            CenterToScreen();
        }
        _config!.WindowMode = fullscreen ? "Fullscreen" : "Windowed";
        Logger.Log($"[Window] Mode set to {_config.WindowMode}.");
    }

    /// <summary>
    /// In D3D11 mode, keeps GamePanel permanently hidden — all rendering including menus
    /// goes through the swap chain. Steam's overlay hooks IDXGISwapChain::Present and
    /// composites itself directly into the swap chain buffer; a visible GDI child window
    /// (GamePanel) would sit above the swap chain in DWM's Z-order and cover the overlay.
    /// No-op in GDI+ fallback mode (GamePanel always visible).
    /// </summary>
    private void UpdateGamePanelVisibility()
    {
        if (_gamePanel is null || _renderer?.OwnsFrameSurface != true) return;
        _gamePanel.Visible = false;
        Logger.Log("[Renderer] GamePanel.Visible=False (D3D11 mode).");
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
                _renderer?.MarkOverlayDirty();
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
                _renderer?.MarkOverlayDirty();
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

    // ---- IMenuSceneProvider / IMenuInputTarget ------------------------------------

    Action<Graphics, Rectangle>? Rendering.IMenuSceneProvider.GetActiveScenePainter()
    {
        if (_logoScreen is not null)
            return (g, b) => LogoRenderer.Draw(g, b, _logoScreen.Image, _logoScreen.CurrentAlpha);

        if (_mainMenuScreen?.IsVisible == true)
            return (g, b) => MainMenuRenderer.Draw(g, b, _mainMenuScreen);

        if (_menu?.IsOpen == true)
            return (g, b) => MenuRenderer.Draw(g, b, _menu);

        return null;
    }

    bool UI.IMenuInputTarget.IsWaitingForGamepadButton
        => _mainMenuScreen?.IsGamepadRebinding == true || _menu?.IsGamepadRebinding == true;

    void UI.IMenuInputTarget.HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (_mainMenuScreen?.IsVisible == true) _mainMenuScreen.HandleGamepadNav(nav);
        else if (_menu?.IsOpen == true)         _menu.HandleGamepadNav(nav);
        _renderer?.MarkOverlayDirty();
        _gamePanel?.Invalidate();
        // Render immediately rather than waiting for the next steam timer tick.
        // On Wine/Proton, WM_TIMER can fire 20-30ms late; presenting here drops
        // the visual response latency from "up to one timer interval" to near zero.
        if (_emulationThread?.IsPaused == true)
            _renderer?.Tick(vsync: false);
    }

    void UI.IMenuInputTarget.HandleGamepadButtonPress(string buttonName)
    {
        string? toast = _mainMenuScreen?.IsVisible == true
            ? _mainMenuScreen.HandleGamepadButtonPress(buttonName)
            : _menu?.HandleGamepadButtonPress(buttonName);
        if (toast is not null) _renderer?.ShowToast(toast);
        _renderer?.MarkOverlayDirty();
        _gamePanel?.Invalidate();
        if (_emulationThread?.IsPaused == true)
            _renderer?.Tick(vsync: false);
    }

    // ---- Form lifecycle -----------------------------------------------------------

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        Logger.Log("[Shutdown] Form closing — stopping emulation thread.");
        _emulationThread?.Stop();

        // Persist state — only auto-save if the game was actually running
        try
        {
            if (_gameHasStarted)
                _saveStates?.AutoSave();
            else
                Logger.Log("[Shutdown] Game was never started — skipping auto-save.");

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
        _renderer?.Dispose(); // must be before _d3dHook — renderer does not own device/swap chain
        _d3dHook?.Dispose();
        _sidebarLeft?.Dispose();
        _sidebarRight?.Dispose();
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
