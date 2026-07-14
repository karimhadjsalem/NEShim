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
    private GameContext?      _game;
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

    // ---- Multi-game carousel ----
    private GameCarouselScreen? _carousel;

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
        if (MultiGameMode.IsActive) InitializeEmulatorMultiGame();
        else                        InitializeEmulatorSingleGame();
    }

    private void InitializeEmulatorSingleGame()
    {
        var achievements = LoadGameContent(ctx: null);
        _frameBuffer = new FrameBuffer(); // engine-level half of the old InitializeRendering()
        InitializeInput();
        InitializeAudio();
        InitializeSteam();
        InitializeWindowAndD3DHook();
        if (_config!.NoLogo)
            FinishInitialization(achievements);
        else
            ShowLogo(achievements);
    }

    private void InitializeEmulatorMultiGame()
    {
        // Shell settings (window mode, language, NoLogo, ...) for the pre-selection carousel
        // period. Most AppConfig fields (RomPath, achievements, ...) are unused here and get
        // reloaded per-game by LoadGame once a game is chosen. LoadFrom's "auto-create defaults
        // if missing" branch is never reached since MultiGameMode.IsActive already confirmed
        // the manifest file exists.
        _config = ConfigLoader.LoadFrom(MultiGameMode.ManifestPath);
        _sdlHost.SetTitle(_config.WindowTitle);
        if (_config.EnableLogging) Logger.Enable();

        _frameBuffer = new FrameBuffer();
        InitializeInput();
        InitializeSteam(); // must run before the carousel can query DLC ownership
        InitializeWindowAndD3DHook(); // renderer/device created once for the process lifetime
        if (_config.NoLogo)
            InitializeCarousel();
        else
            ShowLogo(null); // reuses ShowLogo unchanged; achievements genuinely unknown yet
    }

    /// <summary>
    /// Shared Template Method: loads one game's config, ROM, saves, and sidebar art. Used both
    /// by the once-per-process single-game boot (<paramref name="ctx"/> null — today's
    /// exe-relative paths, unchanged) and by every multi-game carousel selection
    /// (<paramref name="ctx"/> set — that game's own folder). See <see cref="LoadGame"/> for the
    /// second half (presentation) of this split.
    /// </summary>
    private AchievementManager? LoadGameContent(GameContext? ctx)
    {
        UnloadCurrentGame(); // safe no-op if nothing was loaded yet (first boot, or first carousel pick)
        _game = ctx;

        _config = ConfigLoader.Load(_game);
        _sdlHost.SetTitle(DisplayTitle(_config));
        if (_config.EnableLogging) Logger.Enable();
        Logger.Log($"[Init] Config loaded — ROM: {_config.RomPath}, window: {_config.WindowTitle}, language: {_config.Language}.");

        var achievements = InitializeEmulatorCore();
        InitializeSaveSystems();
        LoadSidebarsForCurrentGame();
        return achievements;
    }

    private static string DisplayTitle(AppConfig config) =>
        string.IsNullOrWhiteSpace(config.GameDisplayTitle) ? config.WindowTitle : config.GameDisplayTitle;

    /// <summary>Scans <c>games/</c>, filters by Steam DLC ownership, and shows the carousel.</summary>
    private void InitializeCarousel()
    {
        // Reload the shell config every time the carousel is (re)shown — cheap and idempotent
        // on first boot (already fresh from InitializeEmulatorMultiGame), but necessary after
        // "Change Game" (ChangeGame -> UnloadCurrentGame nulls _config), so CarouselBackgroundPath
        // and friends always reflect games/multigame.json rather than the just-exited game's
        // own config.json.
        // Overlays the carousel's own persisted window-mode preference (ShellUserConfigPath) on
        // top of the publisher shell manifest. This is a discrete, carousel-only setting,
        // independent of any individual game's own WindowMode — applying it here (rather than
        // leaving whatever mode the just-exited game left the window in) is what makes the
        // carousel's window mode stick across game transitions and process relaunches.
        _config = ConfigLoader.Load(MultiGameMode.ManifestPath, MultiGameMode.ShellUserConfigPath);
        ApplyConfiguredWindowMode();

        // ResolveLanguage() only needs _config.Language, already populated from the shell
        // manifest above — safe to resolve here, before any game is chosen, exactly like
        // LoadGame() resolves it per-game later.
        var localization = LoadLocalization();
        var games = GameScanner.Scan(MultiGameMode.GamesRoot)
                                .Where(g => SteamDlcManager.IsOwned(g.SteamDlcAppId))
                                .ToList();
        _carousel?.Dispose();
        _carousel = new GameCarouselScreen(games, MultiGameMode.GamesRoot, _config.CarouselBackgroundPath, localization);
        _carousel.GameChosen += g => _marshalToMainThread(() =>
            LoadGame(GameContext.ForGame(MultiGameMode.GamesRoot, g.GameId)));
        // The carousel is the top-level screen in multi-game mode — there's no parent menu to
        // back out to, so Escape/Back exits the app (mirrors MainMenuScreen.ExitChosen's handler).
        _carousel.QuitRequested += () => _marshalToMainThread(_sdlHost.RequestQuit);
        _renderer?.MarkOverlayDirty();
    }

    /// <summary>
    /// The presentation half of the game-load Template Method: re-applies render options for
    /// the new game, restarts audio, and rebuilds the menus/emulation session on top of
    /// <see cref="LoadGameContent"/>. Called for every carousel selection, including
    /// re-selections after "Change Game".
    /// </summary>
    private void LoadGame(GameContext game)
    {
        var achievements = LoadGameContent(game);
        _carousel?.Dispose();
        _carousel = null;

        // The carousel period (or the previously loaded game) may have left the window in a
        // different mode than this game's own config.json wants — re-apply it now rather than
        // carrying over whatever mode was last set.
        ApplyConfiguredWindowMode();

        ApplyRenderingOptions();
        _renderer?.SetSidebars(_sidebarLeft, _sidebarRight);
        InitializeAudio();

        var localization = LoadLocalization();
        InitializeMainMenu(localization);
        InitializeInGameMenu(localization);
        InitializeEmulationStartup(achievements);

        _renderer?.MarkOverlayDirty();
    }

    /// <summary>
    /// Tears down the currently loaded game (if any) so a new one can be loaded in its place.
    /// Mirrors <see cref="Shutdown"/>'s persist-then-dispose ordering. Safe to call when nothing
    /// is loaded yet (first boot). Called from <see cref="LoadGameContent"/> (every load path)
    /// and again from "Change Game" for the eager-teardown case.
    /// </summary>
    private void UnloadCurrentGame()
    {
        if (_host is null) return; // nothing loaded yet
        _emulationThread?.Stop();
        if (_gameHasStarted) _saves?.AutoSave();
        _saves?.Shutdown();
        if (_config is not null)
        {
            if (_saves is not null) _config.ActiveSlot = _saves.ActiveSlot;
            ConfigLoader.Save(_config, _game); // persists the OUTGOING game's final state to ITS OWN user.json
        }
        _mainMenuMusic?.Dispose();
        _mainMenuScreen?.Dispose();
        if (_sidebarLeft  != IntPtr.Zero) { SDL.DestroySurface(_sidebarLeft);  _sidebarLeft  = IntPtr.Zero; }
        if (_sidebarRight != IntPtr.Zero) { SDL.DestroySurface(_sidebarRight); _sidebarRight = IntPtr.Zero; }
        _audio?.Dispose();
        _host?.Dispose();

        _emulationThread = null; _saves = null; _audio = null; _mainMenuScreen = null;
        _mainMenuMusic = null; _menu = null; _host = null; _config = null; _game = null;
        _gameHasStarted = false;
    }

    /// <summary>
    /// In-game "Change Game" — eagerly tears down the current game (frees ROM/save/audio state
    /// immediately) and returns to the carousel with a fresh scan and DLC ownership re-check.
    /// Only reachable when <see cref="MultiGameMode.IsActive"/>. Distinct from
    /// <see cref="ReturnToMainMenu"/>, which stays within the same game.
    /// </summary>
    private void ChangeGame()
    {
        _menu?.Close();
        UnloadCurrentGame();
        InitializeCarousel();
    }

    private void InitializeWindowAndD3DHook()
    {
        ApplyConfiguredWindowMode();
        _overlayRenderer = Rendering.OverlayRendererFactory.Create(_sdlHost);
        _renderer = Rendering.RendererFactory.Create(_overlayRenderer, 256, 240, _sdlHost);
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

        var supported = Rendering.VideoFilterModeParser.D3D11Supported;

        if (!supported.Contains(mode))
        {
            Logger.Log($"[Renderer] VideoFilter '{_config.VideoFilter}' is not supported by the active renderer; " +
                       $"falling back to PixelPerfect.");
            mode = Rendering.VideoFilterMode.PixelPerfect;
            _config.VideoFilter = mode.ToString();
            ConfigLoader.Save(_config, _game);
        }

        var overlayMode = Rendering.VideoFilterModeParser.ParseOverlay(_config!.VideoFilterOverlay);
        _renderer?.InitializeRenderingOptions(Rendering.Filters.D3D11FilterFactory.Create(mode), overscan, colorMode);
        _renderer?.SetMotionEffect(motionMode);
        _renderer?.SetPictureAdjust(_config!.VideoBrightness, _config.VideoContrast, _config.VideoSaturation, _config.VideoHue);
        _renderer?.SetOverlayFilter(overlayMode.HasValue
            ? Rendering.Filters.D3D11FilterFactory.Create(overlayMode.Value)
            : null);
    }

    private void OnD3DDeviceLost(object? sender, EventArgs e)
    {
        Logger.Log("[Renderer] Recovering from device loss — pausing emulation and reinitialising.");
        _emulationThread?.SetPauseReason(EmulationThread.PauseReasons.DeviceLost, true);

        _renderer?.Dispose();
        _renderer = null;
        _overlayRenderer?.Dispose();
        _overlayRenderer = null;

        _overlayRenderer = Rendering.OverlayRendererFactory.Create(_sdlHost);
        _renderer = Rendering.RendererFactory.Create(_overlayRenderer, 256, 240, _sdlHost);
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
        if (MultiGameMode.IsActive)
            InitializeCarousel();
        else
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

        // The carousel's background can be an animated GIF, and its selection slide / flip-to-
        // description transitions are wall-clock-driven — all three need the overlay repainted
        // every idle tick, not just on input, or they visibly freeze between keypresses (mirrors
        // the _logoScreen fade-animation case above). No EmulationThread/InputProcessor exists
        // yet to drive gamepad polling (that only starts once a game is chosen), so both menu nav
        // and hotkeys (e.g. ToggleWindow) are polled directly here instead — nav dispatched
        // through the same IMenuInputTarget.HandleGamepadNav path every other menu screen already
        // uses; hotkeys through the same HotkeyFired delegate EmulationThread's loop uses while a
        // game is active. The two periods never overlap (no EmulationThread while the carousel is
        // up), so polling the same _input instance from either the UI thread here or the
        // emulation thread there is safe — never both at once.
        if (_carousel is not null)
        {
            _renderer?.MarkOverlayDirty();
            var nav = _input!.PollMenuNav(_config!);
            if (nav.Any)
                ((UI.IMenuInputTarget)this).HandleGamepadNav(nav);
            _input.AdvanceHotkeyState(_config!);
        }
    }

    private void PreloadAssets()
    {
        if (!string.IsNullOrWhiteSpace(_config!.MainMenuBackgroundPath))
        {
            string? resolved = UI.MainMenuScreen.ResolveAssetPath(_config.MainMenuBackgroundPath, _game);
            if (resolved != null)
            {
                _preloadedMenuBackground = Rendering.SdlSurfaceLoader.LoadFromFile(resolved);
            }
        }

        if (_config.MainMenuMusicEnabled && !string.IsNullOrWhiteSpace(_config.MainMenuMusicPath))
        {
            string? resolved = UI.MainMenuScreen.ResolveAssetPath(_config.MainMenuMusicPath, _game);
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
        var localization = LoadLocalization();
        InitializeMainMenu(localization);
        InitializeInGameMenu(localization);
        InitializeEmulationStartup(achievements);
    }

    private AchievementManager? InitializeEmulatorCore()
    {
        string romPath = GameContext.ResolvePath(_config!.RomPath, _game);

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

        var achConfig = AchievementConfigLoader.Load(_host.RomHash, _config.AchievementPublicKey, _game);
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
        string sramPath = GameContext.ResolvePath(_config!.SaveRamPath, _game);
        string stateDir = GameContext.ResolvePath(_config.SaveStateDirectory, _game);

        _saves = new SaveManager(_host!, stateDir, sramPath, _config.ActiveSlot);
        _saves.Startup();
        Logger.Log($"[Init] Save state directory: {stateDir} (active slot: {_config.ActiveSlot + 1})");
    }

    private void LoadSidebarsForCurrentGame()
    {
        _sidebarLeft  = LoadSidebarSurface(_config!.SidebarLeftPath, _game);
        _sidebarRight = LoadSidebarSurface(_config.SidebarRightPath, _game);
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

        // Handled here rather than in EmulationThread.HandleHotkeyAction: that subscriber
        // suppresses hotkeys while a menu is open or paused at the main menu, but window mode
        // should toggle everywhere (carousel, main menu, in-game menu, gameplay) — same reasoning
        // as every other cross-screen concern, dispatched through a delegate rather than a
        // screen-specific key check. Always marshals: this can fire from either the emulation
        // thread (AdvanceHotkeyState polled from EmulationThread's loop while a game is active)
        // or the UI thread (polled from OnIdle while the carousel is up).
        _input.HotkeyFired += action =>
        {
            if (action == "ToggleWindow")
                _marshalToMainThread(() => SetWindowMode(!_isFullscreen));
        };
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

    private void InitializeSteam()
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
                _renderer?.SetFilter(Rendering.Filters.D3D11FilterFactory.Create(mode));
                ConfigLoader.Save(_config, _game);
            },
            onVideoFilterOverlayChanged: mode =>
            {
                _config!.VideoFilterOverlay = mode?.ToString() ?? "None";
                _renderer?.SetOverlayFilter(mode.HasValue ? Rendering.Filters.D3D11FilterFactory.Create(mode.Value) : null);
                ConfigLoader.Save(_config, _game);
            },
            onVideoColorFilterChanged: mode =>
            {
                _config!.VideoColorFilter = mode.ToString();
                _renderer?.SetColorFilter(mode);
                ConfigLoader.Save(_config, _game);
            },
            onVideoMotionEffectChanged: mode =>
            {
                _config!.VideoMotionEffect = mode.ToString();
                _renderer?.SetMotionEffect(mode);
                ConfigLoader.Save(_config, _game);
            },
            onOverscanModeChanged: overscan =>
            {
                _config!.OverscanMode = overscan.ToString();
                _renderer?.SetOverscanMode(overscan);
                ConfigLoader.Save(_config, _game);
            },
            onLanguageChanged: lang => _marshalToMainThread(() => OnLanguageChanged(lang)),
            onPictureAdjustChanged: (brightness, contrast, saturation, hue) =>
            {
                _renderer?.SetPictureAdjust(brightness, contrast, saturation, hue);
                ConfigLoader.Save(_config!, _game);
            },
            onAudioEqChanged: (bass, mid, treble) =>
            {
                _audio?.SetEq(bass, mid, treble);
                ConfigLoader.Save(_config!, _game);
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
        // Only reachable in multi-game mode; ChangeGame() already tears down the current
        // game (including main menu music) via UnloadCurrentGame before showing the carousel.
        _mainMenuScreen.ChangeGameChosen += () => _marshalToMainThread(ChangeGame);

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
            onChangeGame:        () => _marshalToMainThread(ChangeGame),
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
                _renderer?.SetFilter(Rendering.Filters.D3D11FilterFactory.Create(mode));
                ConfigLoader.Save(_config, _game);
            },
            onVideoFilterOverlayChanged: mode =>
            {
                _config!.VideoFilterOverlay = mode?.ToString() ?? "None";
                _renderer?.SetOverlayFilter(mode.HasValue ? Rendering.Filters.D3D11FilterFactory.Create(mode.Value) : null);
                ConfigLoader.Save(_config, _game);
            },
            onVideoColorFilterChanged: mode =>
            {
                _config!.VideoColorFilter = mode.ToString();
                _renderer?.SetColorFilter(mode);
                ConfigLoader.Save(_config, _game);
            },
            onVideoMotionEffectChanged: mode =>
            {
                _config!.VideoMotionEffect = mode.ToString();
                _renderer?.SetMotionEffect(mode);
                ConfigLoader.Save(_config, _game);
            },
            onOverscanModeChanged: overscan =>
            {
                _config!.OverscanMode = overscan.ToString();
                _renderer?.SetOverscanMode(overscan);
                ConfigLoader.Save(_config, _game);
            },
            onLanguageChanged: lang => _marshalToMainThread(() => OnLanguageChanged(lang)),
            onPictureAdjustChanged: (brightness, contrast, saturation, hue) =>
            {
                _renderer?.SetPictureAdjust(brightness, contrast, saturation, hue);
                ConfigLoader.Save(_config!, _game);
            },
            onAudioEqChanged: (bass, mid, treble) =>
            {
                _audio?.SetEq(bass, mid, treble);
                ConfigLoader.Save(_config!, _game);
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

    private static IntPtr LoadSidebarSurface(string path, GameContext? ctx = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return IntPtr.Zero;
        string? resolved = UI.MainMenuScreen.ResolveAssetPath(path, ctx);
        if (resolved == null) return IntPtr.Zero;
        return Rendering.SdlSurfaceLoader.LoadFromFile(resolved);
    }

    private MainMenuMusic? CreateMainMenuMusic(AppConfig config)
    {
        string path = config.MainMenuMusicPath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        string? resolved = MainMenuScreen.ResolveAssetPath(path, _game);
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
        ConfigLoader.Save(_config!, _game);
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

        // The carousel has its own discrete, persisted window-mode preference (see
        // MultiGameMode.ShellUserConfigPath), independent of any game's own WindowMode. Save it
        // immediately — unlike per-game config, which defers persistence to UnloadCurrentGame /
        // Shutdown — so a toggle made on the carousel survives both a later game selection and a
        // full process relaunch. _carousel is only non-null while the carousel is the active
        // screen, so this is a no-op during gameplay or the initial startup apply.
        if (MultiGameMode.IsActive && _carousel is not null)
            ConfigLoader.SaveUserTo(_config, MultiGameMode.ShellUserConfigPath);
    }

    /// <summary>Applies whatever WindowMode the current _config specifies. Called at true startup
    /// (InitializeWindowAndD3DHook), when loading a chosen game (LoadGame), and when
    /// (re-)entering the carousel (InitializeCarousel) — each is an explicit "this config says X"
    /// moment, where _config has already been loaded with the appropriate source's own persisted
    /// preference (the game's user.json, or ShellUserConfigPath for the carousel).</summary>
    private void ApplyConfiguredWindowMode() =>
        SetWindowMode(_config!.WindowMode.Equals("Fullscreen", StringComparison.OrdinalIgnoreCase));

    private void OnKeyDown(SDL.Keycode key)
    {
        if (_logoScreen is not null)
        {
            SkipLogo();
            return;
        }

        // F11 (ToggleWindow) is not handled here — it's a HotkeyMappings-driven action, detected
        // by AdvanceHotkeyState's edge-triggered poll and dispatched via the HotkeyFired delegate
        // (see InitializeInput), the same mechanism every other hotkey (save/load slots, open
        // menu, ...) uses. That keeps every key press flowing through one of two paths: a
        // screen's own HandleKey for screen-specific navigation, or a config-mapped hotkey
        // delegate for cross-screen actions — never a raw keycode check in this dispatcher.

        if (_carousel is not null)
        {
            if (_carousel.HandleKey(key))
                _renderer?.MarkOverlayDirty();
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
                _renderer?.MarkOverlayDirty();
        }
    }

    // ---- IMenuSceneProvider --------------------------------------------------

    Action<Rendering.SDL3PaintContext, SDL.Rect>? Rendering.IMenuSceneProvider.GetActiveScenePainter()
    {
        if (_logoScreen is not null)
            return (ctx, b) => LogoRenderer.Draw(ctx, b, _logoScreen.Image, _logoScreen.CurrentAlpha);

        if (_carousel is not null)
            return (ctx, b) => GameCarouselRenderer.Draw(ctx, b, _carousel);

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
        if (_carousel is not null)          _carousel.HandleGamepadNav(nav);
        else if (_mainMenuScreen?.IsVisible == true) _mainMenuScreen.HandleGamepadNav(nav);
        else if (_menu?.IsOpen == true)              _menu.HandleGamepadNav(nav);
        _renderer?.MarkOverlayDirty();
        if (_emulationThread?.IsPaused == true)
            _renderer?.Tick(vsync: false);
    }

    void UI.IMenuInputTarget.HandleGamepadButtonPress(string buttonName)
    {
        // The carousel has no gamepad-rebinding prompts, so there's no toast to surface here.
        string? toast = _carousel is not null ? null
            : _mainMenuScreen?.IsVisible == true
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
                ConfigLoader.Save(_config, _game);
            }
        }
        catch (Exception ex) { Logger.Log($"[Shutdown] Persist error: {ex.Message}"); }

        Logger.Log("[Shutdown] Disposing resources.");
        _logoScreen?.Dispose();
        _carousel?.Dispose();
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
