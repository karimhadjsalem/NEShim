using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SDL3;
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

[ExcludeFromCodeCoverage]
internal sealed class NEShimApp : Rendering.IMenuSceneProvider, UI.IMenuInputTarget
{
    // ---- Core components ----
    private AppConfig?        _config;
    private IEmulationCore?   _host;
    private IInputReader?     _input;
    private IGamepadDevice?   _gamepadDevice;
    private AudioPlayer?      _audio;
    private MainMenuMusic?    _mainMenuMusic;
    private ISaveManager?     _saves;
    private FrameBuffer?      _frameBuffer;
    private MainMenuScreen?   _mainMenuScreen;
    private InGameMenu?       _menu;
    private EmulationThread?  _emulationThread;

    // ---- Overlay renderer / frame renderer ----
    private Rendering.IOverlayRenderer? _overlayRenderer;
    private Rendering.IFrameRenderer?   _renderer;

    private IntPtr _sidebarLeft;
    private IntPtr _sidebarRight;

    // ---- Logo splash screen ----
    private LogoScreen?           _logoScreen;
    private Task?                 _preloadTask;
    private IntPtr                _preloadedMenuBackground;
    private MainMenuMusic?        _preloadedMusic;
    private AchievementManager?   _pendingAchievements;

    // ---- Steam tick ----
    private readonly Stopwatch _steamStopwatch = Stopwatch.StartNew();
    private const int SteamCallbackIntervalMs = 16;

    private bool _isFullscreen = true;
    private bool _gameHasStarted;

    private readonly SDL3WindowHost _sdlHost;
    private readonly Action<Action> _marshalToMainThread;

    public NEShimApp(SDL3WindowHost sdlHost)
    {
        _sdlHost             = sdlHost;
        _marshalToMainThread = sdlHost.MarshalToMainThread;
    }

    public void Run()
    {
        _sdlHost.HideCursor();
        try
        {
            InitializeEmulator();
        }
        catch (Exception ex)
        {
            SDL.ShowSimpleMessageBox(SDL.MessageBoxFlags.Error,
                "NEShim — Startup Error", $"Failed to start emulator:\n\n{ex.Message}", IntPtr.Zero);
            return;
        }
        _renderer?.MarkOverlayDirty();
        _renderer?.Tick(vsync: false);
        _sdlHost.Show();
        _sdlHost.RunLoop(OnIdle);
        Shutdown();
    }

    private void InitializeEmulator()
    {
        InitializeConfig();
        var achievements = InitializeEmulatorCore();
        InitializeSaveSystems();
        InitializeRendering();
        InitializeInput();
        InitializeAudio();
        InitializeWindowAndD3DHook();
        if (_config!.NoLogo)
            FinishInitialization(achievements);
        else
            ShowLogo(achievements);
    }

    private void InitializeWindowAndD3DHook()
    {
        SetWindowMode(_config!.WindowMode.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase));
        _overlayRenderer = Rendering.OverlayRendererFactory.Create(_config!.ForceRenderer, _sdlHost);
        _renderer = Rendering.RendererFactory.Create(_overlayRenderer, 256, 240, _config!.ForceRenderer);
        _renderer.DeviceLost += OnD3DDeviceLost;
        _renderer.SetSidebars(_sidebarLeft, _sidebarRight);
        _renderer.SetMenuSceneProvider(this);

        ApplyRenderingOptions();

        _sdlHost.Resized += (w, h) => _renderer?.Resize(w, h);
    }

    private void ApplyRenderingOptions()
    {
        var mode      = Rendering.VideoFilterModeParser.Parse(_config!.VideoFilter);
        var overscan  = Rendering.OverscanModeParser.Parse(_config.OverscanMode);
        var colorMode = Rendering.VideoColorFilterModeParser.Parse(_config.VideoColorFilter);
        var motionMode= Rendering.VideoMotionEffectModeParser.Parse(_config.VideoMotionEffect);

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
        {
            d3d.InitializeRenderingOptions(Rendering.Filters.D3D11FilterFactory.Create(mode), overscan, colorMode);
            d3d.SetMotionEffect(motionMode);
            d3d.SetPictureAdjust(_config!.VideoBrightness, _config.VideoContrast, _config.VideoSaturation, _config.VideoHue);
            var overlayMode = Rendering.VideoFilterModeParser.ParseOverlay(_config!.VideoFilterOverlay);
            d3d.SetOverlayFilter(overlayMode.HasValue
                ? Rendering.Filters.D3D11FilterFactory.Create(overlayMode.Value)
                : null);
        }
    }

    private void OnD3DDeviceLost(object? sender, EventArgs e)
    {
        Logger.Log("[Renderer] Recovering from device loss — pausing emulation and reinitialising.");
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.DeviceLost, true);

        _renderer?.Dispose();
        _renderer = null;
        _overlayRenderer?.Dispose();
        _overlayRenderer = null;

        _overlayRenderer = Rendering.OverlayRendererFactory.Create(_config!.ForceRenderer, _sdlHost);
        _renderer = Rendering.RendererFactory.Create(_overlayRenderer, 256, 240, _config!.ForceRenderer);
        _renderer.DeviceLost += OnD3DDeviceLost;
        _renderer.SetSidebars(_sidebarLeft, _sidebarRight);
        _renderer.SetMenuSceneProvider(this);
        ApplyRenderingOptions();

        _emulationThread?.UpdateRenderer(_renderer);

        Logger.Log($"[Renderer] Reinitialised — ownsFrameSurface={_renderer.OwnsFrameSurface}. Resuming emulation.");
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.DeviceLost, false);
    }

    private void ShowLogo(AchievementManager? achievements)
    {
        _pendingAchievements = achievements;
        using var stream = typeof(UI.LogoScreen).Assembly
            .GetManifestResourceStream("NEShim.Assets.logos.neshim-logo-splash.png");
        if (stream is null)
        {
            Logger.Log("[Logo] Embedded resource not found — skipping splash screen.");
            FinishInitialization(achievements);
            return;
        }
        _logoScreen = new UI.LogoScreen(Rendering.SdlSurfaceLoader.LoadFromStream(stream));
        _preloadTask = Task.Run(PreloadAssets);
    }

    private void SkipLogo()
    {
        _logoScreen?.Dispose();
        _logoScreen = null;
        FinishInitialization(_pendingAchievements);
        _pendingAchievements = null;
    }

    private void OnIdle()
    {
        if (_steamStopwatch.ElapsedMilliseconds >= SteamCallbackIntervalMs)
        {
            SteamManager.Tick();
            if (_emulationThread is null || _emulationThread.IsPaused)
                _renderer?.Tick(vsync: false);
            _steamStopwatch.Restart();
        }

        if (_logoScreen is not null)
        {
            _renderer?.MarkOverlayDirty();
            if (_input!.PollAnyControllerButton())
            {
                SkipLogo();
                return;
            }
            if (_logoScreen.IsComplete)
            {
                SkipLogo();
            }
        }
    }

    private void PreloadAssets()
    {
        if (!string.IsNullOrWhiteSpace(_config!.MainMenuBackgroundPath))
        {
            string? resolved = UI.MainMenuScreen.ResolveAssetPath(_config.MainMenuBackgroundPath);
            if (resolved != null)
            {
                _preloadedMenuBackground = Rendering.SdlSurfaceLoader.LoadFromFile(resolved);
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

    private void FinishInitialization(AchievementManager? achievements)
    {
        _logoScreen?.Dispose();
        _logoScreen = null;
        _preloadTask?.Wait();
        _preloadTask = null;
        var localization = InitializeSteamAndLocalization();
        InitializeMainMenu(localization);
        InitializeInGameMenu(localization);
        InitializeEmulationStartup(achievements);
    }

    private void InitializeConfig()
    {
        _config = ConfigLoader.Load();
        _sdlHost.SetTitle(_config.WindowTitle);

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

        byte[] rom = File.ReadAllBytes(romPath);
        _host = new BizHawkEmulationCore(_config!);
        _host.LoadRom(rom, Path.GetFileNameWithoutExtension(romPath));
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

        var achievements = new AchievementManager(
            _host.MemoryDomains, achConfig,
            () => SteamManager.StatsReady,
            id => _marshalToMainThread(() =>
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
        string sramPath = Path.IsPathRooted(_config!.SaveRamPath)
            ? _config.SaveRamPath
            : Path.Combine(AppContext.BaseDirectory, _config.SaveRamPath);

        string stateDir = Path.IsPathRooted(_config.SaveStateDirectory)
            ? _config.SaveStateDirectory
            : Path.Combine(AppContext.BaseDirectory, _config.SaveStateDirectory);

        _saves = new SaveManager(_host!, stateDir, sramPath, _config.ActiveSlot);
        _saves.Startup();
        Logger.Log($"[Init] Save state directory: {stateDir} (active slot: {_config.ActiveSlot + 1})");
    }

    private void InitializeRendering()
    {
        _frameBuffer  = new FrameBuffer();
        _sidebarLeft  = LoadSidebarSurface(_config!.SidebarLeftPath);
        _sidebarRight = LoadSidebarSurface(_config.SidebarRightPath);
    }

    private void InitializeInput()
    {
        _gamepadDevice = new SDL3GamepadDevice();
        _input = new InputManager(
            new Input.Sources.KeyboardInputSource(),
            new Input.Sources.SDL3GamepadSource(_gamepadDevice),
            new Input.Sources.SteamInputSource(),
            new Input.Mappers.KeyboardMapper(),
            new Input.Mappers.SDL3GamepadMapper(),
            new Input.Mappers.SteamInputMapper(),
            _gamepadDevice);
        _sdlHost.KeyDown += key => _input.OnKeyDown(key);
        _sdlHost.KeyUp   += key => _input.OnKeyUp(key);
        _sdlHost.KeyDown += OnKeyDown;
    }

    private void InitializeAudio()
    {
        var filterMode = AudioFilterModeParser.Parse(_config!.AudioFilter);
        _audio = new AudioPlayer(_config.AudioBufferFrames, CreateProcessor(filterMode));
        _audio.SetVolume(_config.Volume / 100f);
        _audio.SetEq(_config.AudioEqBass, _config.AudioEqMid, _config.AudioEqTreble);
        Logger.Log($"[Init] Audio: buffer={_config.AudioBufferFrames} frames, filter={filterMode}, volume={_config.Volume}%");
    }

    private static IAudioProcessor CreateProcessor(AudioFilterMode mode) => mode switch
    {
        AudioFilterMode.Warm          => new SoundScrubberProcessor(),
        AudioFilterMode.PseudoStereo  => new PseudoStereoProcessor(),
        AudioFilterMode.WarmStereo    => new WarmStereoProcessor(),
        AudioFilterMode.Compression   => new CompressionProcessor(),
        AudioFilterMode.BassBoost     => new BassBoostProcessor(),
        AudioFilterMode.Saturation    => new TapeSaturationProcessor(),
        AudioFilterMode.DmcStabilizer => new DmcStabilizerProcessor(),
        _                             => new NesFilterProcessor(),
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
            saveStates:          _saves!,
            config:              _config!,
            localization:        localization,
            bgImagePath:         _preloadedMenuBackground == IntPtr.Zero ? _config!.MainMenuBackgroundPath : null,
            bgImage:             _preloadedMenuBackground,
            onWindowModeToggle:  fullscreen => _marshalToMainThread(() => SetWindowMode(fullscreen)),
            onConfigSaved:       () => { },
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
                ConfigLoader.Save(_config);
            },
            onVideoFilterOverlayChanged: mode =>
            {
                _config!.VideoFilterOverlay = mode?.ToString() ?? "None";
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetOverlayFilter(mode.HasValue ? Rendering.Filters.D3D11FilterFactory.Create(mode.Value) : null);
                ConfigLoader.Save(_config);
            },
            onVideoColorFilterChanged: mode =>
            {
                _config!.VideoColorFilter = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetColorFilter(mode);
                ConfigLoader.Save(_config);
            },
            onVideoMotionEffectChanged: mode =>
            {
                _config!.VideoMotionEffect = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetMotionEffect(mode);
                ConfigLoader.Save(_config);
            },
            onOverscanModeChanged: overscan =>
            {
                _config!.OverscanMode = overscan.ToString();
                _renderer?.SetOverscanMode(overscan);
                ConfigLoader.Save(_config);
            },
            onLanguageChanged: lang => _marshalToMainThread(() => OnLanguageChanged(lang)),
            onPictureAdjustChanged: (brightness, contrast, saturation, hue) =>
            {
                if (_renderer is Rendering.D3D11Renderer d3dPic)
                    d3dPic.SetPictureAdjust(brightness, contrast, saturation, hue);
                ConfigLoader.Save(_config!);
            },
            onAudioEqChanged: (bass, mid, treble) =>
            {
                _audio?.SetEq(bass, mid, treble);
                ConfigLoader.Save(_config!);
            });

        _preloadedMenuBackground = IntPtr.Zero;

        _mainMenuScreen.NewGameChosen += () => _marshalToMainThread(() =>
        {
            _gameHasStarted = true;
            _mainMenuMusic?.FadeOut();
            SteamManager.ActivateGameplaySet();
            _emulationThread?.DismissMainMenu();
            _renderer?.MarkOverlayDirty();
        });
        _mainMenuScreen.ResumeChosen += () => _marshalToMainThread(() =>
        {
            _gameHasStarted = true;
            _mainMenuMusic?.FadeOut();
            SteamManager.ActivateGameplaySet();
            _emulationThread?.DismissMainMenu();
            _renderer?.MarkOverlayDirty();
        });
        _mainMenuScreen.ExitChosen += () => _marshalToMainThread(() =>
        {
            _mainMenuMusic?.Stop();
            _sdlHost.RequestQuit();
        });

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
            saveStates:          _saves!,
            config:              _config!,
            localization:        localization,
            onExitToDesktop:     () => _marshalToMainThread(_sdlHost.RequestQuit),
            onResetGame:         () => _emulationThread?.ResetGame(),
            onReturnToMainMenu:  () => _marshalToMainThread(ReturnToMainMenu),
            onWindowModeToggle:  fullscreen => _marshalToMainThread(() => SetWindowMode(fullscreen)),
            onConfigSaved:       () => { },
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
                ConfigLoader.Save(_config);
            },
            onVideoFilterOverlayChanged: mode =>
            {
                _config!.VideoFilterOverlay = mode?.ToString() ?? "None";
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetOverlayFilter(mode.HasValue ? Rendering.Filters.D3D11FilterFactory.Create(mode.Value) : null);
                ConfigLoader.Save(_config);
            },
            onVideoColorFilterChanged: mode =>
            {
                _config!.VideoColorFilter = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetColorFilter(mode);
                ConfigLoader.Save(_config);
            },
            onVideoMotionEffectChanged: mode =>
            {
                _config!.VideoMotionEffect = mode.ToString();
                if (_renderer is Rendering.D3D11Renderer d3d)
                    d3d.SetMotionEffect(mode);
                ConfigLoader.Save(_config);
            },
            onOverscanModeChanged: overscan =>
            {
                _config!.OverscanMode = overscan.ToString();
                _renderer?.SetOverscanMode(overscan);
                ConfigLoader.Save(_config);
            },
            onLanguageChanged: lang => _marshalToMainThread(() => OnLanguageChanged(lang)),
            onPictureAdjustChanged: (brightness, contrast, saturation, hue) =>
            {
                if (_renderer is Rendering.D3D11Renderer d3dPic)
                    d3dPic.SetPictureAdjust(brightness, contrast, saturation, hue);
                ConfigLoader.Save(_config!);
            },
            onAudioEqChanged: (bass, mid, treble) =>
            {
                _audio?.SetEq(bass, mid, treble);
                ConfigLoader.Save(_config!);
            });

        _menu.Opened += () => _marshalToMainThread(() => _renderer?.MarkOverlayDirty());
        _menu.Closed += () => _marshalToMainThread(() => _renderer?.MarkOverlayDirty());
    }

    private void InitializeEmulationStartup(AchievementManager? achievements)
    {
        _emulationThread = new EmulationThread(
            _host!, _config!, _input!, _audio!, _frameBuffer!,
            _marshalToMainThread,
            this,
            _saves!, _menu!,
            _renderer!,
            achievements,
            afterFramePresented: SteamManager.RunCallbacksAfterPresent,
            onInGameMenuOpened:  SteamManager.ActivateMenuSet,
            onInGameMenuClosed:  SteamManager.ActivateGameplaySet);

        _sdlHost.FocusChanged += active =>
            _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.FocusLost, !active);

        _audio!.Start(_config!.AudioDevice);
        _emulationThread.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        _emulationThread.Start();
        Logger.Log("[Init] Startup complete — showing main menu.");

        _renderer?.MarkOverlayDirty();
    }

    private void ReturnToMainMenu()
    {
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.MainMenu, true);
        SteamManager.ActivateMenuSet();
        _mainMenuScreen?.Show();
        _mainMenuMusic?.FadeIn();
        _renderer?.MarkOverlayDirty();
    }

    private static IntPtr LoadSidebarSurface(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return IntPtr.Zero;
        string? resolved = UI.MainMenuScreen.ResolveAssetPath(path);
        if (resolved == null) return IntPtr.Zero;
        return Rendering.SdlSurfaceLoader.LoadFromFile(resolved);
    }

    private static MainMenuMusic? CreateMainMenuMusic(AppConfig config)
    {
        string path = config.MainMenuMusicPath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        string? resolved = MainMenuScreen.ResolveAssetPath(path);
        if (resolved == null) return null;
        try   { return new MainMenuMusic(resolved); }
        catch { return null; }
    }

    private string ResolveLanguage()
    {
        if (_config is not null
            && !string.IsNullOrEmpty(_config.Language)
            && !_config.Language.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Log($"[Localization] Language explicitly configured: '{_config.Language}'.");
            return _config.Language;
        }

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
        _sdlHost.SetFullscreen(fullscreen);
        _config!.WindowMode = fullscreen ? "Fullscreen" : "Windowed";
        Logger.Log($"[Window] Mode set to {_config.WindowMode}.");
    }

    private void OnKeyDown(SDL.Keycode key)
    {
        if (_logoScreen is not null)
        {
            SkipLogo();
            return;
        }

        if (_mainMenuScreen?.IsVisible == true)
        {
            if (_mainMenuScreen.HandleKey(key))
            {
                _renderer?.MarkOverlayDirty();
                return;
            }
        }

        if (_menu is null || _emulationThread is null) return;

        if (_menu.IsOpen)
        {
            if (_menu.HandleKey(key))
            {
                _renderer?.MarkOverlayDirty();
                return;
            }
        }

        if (key == SDL.Keycode.F11)
            SetWindowMode(!_isFullscreen);
    }

    // ---- IMenuSceneProvider --------------------------------------------------

    Action<Rendering.SDL3PaintContext, SDL.Rect>? Rendering.IMenuSceneProvider.GetActiveScenePainter()
    {
        if (_logoScreen is not null)
            return (ctx, b) => LogoRenderer.Draw(ctx, b, _logoScreen.Image, _logoScreen.CurrentAlpha);

        if (_mainMenuScreen?.IsVisible == true)
            return (ctx, b) => MainMenuRenderer.Draw(ctx, b, _mainMenuScreen);

        if (_menu?.IsOpen == true)
            return (ctx, b) => MenuRenderer.Draw(ctx, b, _menu);

        return null;
    }

    // ---- IMenuInputTarget ----------------------------------------------------

    bool UI.IMenuInputTarget.IsWaitingForGamepadButton
        => _mainMenuScreen?.IsGamepadRebinding == true || _menu?.IsGamepadRebinding == true;

    void UI.IMenuInputTarget.HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (_mainMenuScreen?.IsVisible == true) _mainMenuScreen.HandleGamepadNav(nav);
        else if (_menu?.IsOpen == true)         _menu.HandleGamepadNav(nav);
        _renderer?.MarkOverlayDirty();
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
        if (_emulationThread?.IsPaused == true)
            _renderer?.Tick(vsync: false);
    }

    // ---- Shutdown ------------------------------------------------------------

    private void Shutdown()
    {
        _sdlHost.Hide();
        Logger.Log("[Shutdown] RunLoop exited — stopping emulation thread.");
        _emulationThread?.Stop();

        try
        {
            if (_gameHasStarted)
                _saves?.AutoSave();
            else
                Logger.Log("[Shutdown] Game was never started — skipping auto-save.");

            _saves?.Shutdown();

            if (_config is not null)
            {
                if (_saves is not null)
                    _config.ActiveSlot = _saves.ActiveSlot;
                ConfigLoader.Save(_config);
            }
        }
        catch (Exception ex) { Logger.Log($"[Shutdown] Persist error: {ex.Message}"); }

        Logger.Log("[Shutdown] Disposing resources.");
        _logoScreen?.Dispose();
        if (_preloadedMenuBackground != IntPtr.Zero) { SDL.DestroySurface(_preloadedMenuBackground); _preloadedMenuBackground = IntPtr.Zero; }
        _preloadedMusic?.Dispose();
        _renderer?.Dispose();
        _overlayRenderer?.Dispose();
        if (_sidebarLeft  != IntPtr.Zero) { SDL.DestroySurface(_sidebarLeft);  _sidebarLeft  = IntPtr.Zero; }
        if (_sidebarRight != IntPtr.Zero) { SDL.DestroySurface(_sidebarRight); _sidebarRight = IntPtr.Zero; }
        _mainMenuMusic?.Dispose();
        _mainMenuScreen?.Dispose();
        FlagImageLoader.Dispose();
        ControllerSprites.Dispose();
        _gamepadDevice?.Dispose();
        _audio?.Dispose();
        _host?.Dispose();
        SteamManager.Shutdown();
        Logger.Log("[Shutdown] Done.");
    }
}
