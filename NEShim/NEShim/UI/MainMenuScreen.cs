using SDL3;
using NEShim.Audio;
using NEShim.Rendering;
using NEShim.Config;
using NEShim.Input;
using NEShim.Localization;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu.
/// Handles Main, ResumeSlots, Settings, KeyBindings, Video, and Sound screens.
/// The emulation thread stays paused until the user picks New Game or loads a save.
/// Per-screen title, items, enabled state, and activation logic live in nested
/// ScreenHandler classes — one per Screen enum value.
/// </summary>
internal sealed partial class MainMenuScreen : IDisposable, IMenuHost
{
    private IReadOnlyDictionary<Screen, IScreenHandler> _handlers;
    // Player 1's gamepad table only — kept solely so OpenMenuBindingIndex can locate the OpenMenu
    // row for rendering (see its doc comment). GamepadBindingsHandler/KeyboardBindingsHandler
    // build their own per-player tables locally instead of reading this field, for every player.
    private (string Label, string ConfigKey)[]          _gamepadBindingActions;
    private ResumeOption[] _resumeOptions = Array.Empty<ResumeOption>();

    // ---- Public state ----

    public Screen  CurrentScreen   { get; private set; } = Screen.Main;
    public bool    IsVisible       { get; private set; } = true;
    public int     SelectedIndex   { get; private set; }
    public IntPtr  Background      { get; }

    // Pre-scaled background surface cache — rebuilt only when bounds change.
    private IntPtr        _scaledBackground;
    private (int W, int H) _scaledBoundsSize;
    private readonly Action<IntPtr>? _onSurfaceDisposing;
    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding             => GamepadRebindingAction != null;
    public bool    OverrideStartBindingProtection => _config.OverrideStartBindingProtection;
    public int     OpenMenuBindingIndex           => Array.FindIndex(_gamepadBindingActions, b => b.ConfigKey == "OpenMenu");
    public string  CurrentOpenMenuBinding         => _config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder");

    /// <summary>
    /// Returns the NES button config key for the currently selected binding row or active
    /// rebind so the controller diagram can highlight the relevant button.
    /// </summary>
    public string? ActiveNesButton
    {
        get
        {
            string? rebinding = RebindingAction ?? GamepadRebindingAction;
            if (rebinding != null)
                return MenuBindingHelpers.IsNesButtonKey(rebinding) ? rebinding : null;

            return _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.GetActiveNesButton(SelectedIndex) : null;
        }
    }

    public string MenuPosition => _config.MainMenuPosition;

    public bool CanResume => _saveStates.HasAutoSave
        || Enumerable.Range(0, _saveStates.SlotCount).Any(_saveStates.SlotExists);

    /// <summary>Exposes the loaded localization so stateless renderers can read strings and font family.</summary>
    public LocalizationData Localization => _localization;

    private readonly ISaveManager _saveStates;
    private readonly AppConfig        _config;
    private          LocalizationData _localization;
    // One resolver per player (index 0 = player 1, ...) — see InGameMenu's identical field for
    // the full rationale.
    private readonly IReadOnlyList<IGamepadGlyphResolver> _glyphResolvers;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<AudioFilterMode>           _onFilterChanged;
    private readonly Action<bool>                      _onMenuMusicToggled;
    private readonly Action<Rendering.VideoFilterMode>       _onVideoFilterChanged;
    private readonly Action<Rendering.VideoFilterMode?>      _onVideoFilterOverlayChanged;
    private readonly Action<Rendering.VideoColorFilterMode>  _onVideoColorFilterChanged;
    private readonly Action<Rendering.VideoMotionEffectMode> _onVideoMotionEffectChanged;
    private readonly Action<Rendering.OverscanMode>         _onOverscanModeChanged;
    private readonly Action<string>                         _onLanguageChanged;
    private readonly Action<int, int, int, int>              _onPictureAdjustChanged;
    private readonly Action<int, int, int>                  _onAudioEqChanged;

    // ---- Events ----
    public event Action? NewGameChosen;
    /// <summary>Fires after the chosen save state has already been loaded.</summary>
    public event Action? ResumeChosen;
    public event Action? ExitChosen;
    /// <summary>Only reachable in multi-game mode — see MultiGameMode.IsActive.</summary>
    public event Action? ChangeGameChosen;

    // ---- Constructor ----

    public MainMenuScreen(
        ISaveManager saveStates,
        AppConfig        config,
        LocalizationData localization,
        string?          bgImagePath,
        Action<bool>     onWindowModeToggle,
        Action           onConfigSaved,
        Action<int>             onVolumeChanged,
        Action<AudioFilterMode> onFilterChanged,
        Action<bool>            onMenuMusicToggled,
        Action<Rendering.VideoFilterMode>       onVideoFilterChanged,
        Action<Rendering.VideoFilterMode?>      onVideoFilterOverlayChanged,
        Action<Rendering.VideoColorFilterMode>  onVideoColorFilterChanged,
        Action<Rendering.VideoMotionEffectMode> onVideoMotionEffectChanged,
        Action<Rendering.OverscanMode>          onOverscanModeChanged,
        Action<string>                         onLanguageChanged,
        Action<int, int, int, int>             onPictureAdjustChanged,
        Action<int, int, int>                  onAudioEqChanged,
        IntPtr           bgImage = default,
        Action<IntPtr>?  onSurfaceDisposing = null,
        IReadOnlyList<IGamepadGlyphResolver>? glyphResolvers = null)
    {
        _onSurfaceDisposing        = onSurfaceDisposing;
        _saveStates                = saveStates;
        _config                    = config;
        _localization              = localization;
        _glyphResolvers             = glyphResolvers ?? new IGamepadGlyphResolver[] { NullGamepadGlyphResolver.Instance };
        _onWindowModeToggle        = onWindowModeToggle;
        _onConfigSaved             = onConfigSaved;
        _onVolumeChanged           = onVolumeChanged;
        _onFilterChanged           = onFilterChanged;
        _onMenuMusicToggled        = onMenuMusicToggled;
        _onVideoFilterChanged        = onVideoFilterChanged;
        _onVideoFilterOverlayChanged = onVideoFilterOverlayChanged;
        _onVideoColorFilterChanged   = onVideoColorFilterChanged;
        _onVideoMotionEffectChanged  = onVideoMotionEffectChanged;
        _onOverscanModeChanged      = onOverscanModeChanged;
        _onLanguageChanged         = onLanguageChanged;
        _onPictureAdjustChanged    = onPictureAdjustChanged;
        _onAudioEqChanged          = onAudioEqChanged;

        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(localization, config);
        _handlers              = BuildHandlers();

        if (bgImage != default)
        {
            Background = bgImage;
        }
        else if (!string.IsNullOrWhiteSpace(bgImagePath))
        {
            string? resolved = ResolveAssetPath(bgImagePath);
            if (resolved != null)
                Background = SdlSurfaceLoader.LoadFromFile(resolved);
        }
    }

    // ---- IMenuHost (explicit — for SharedScreenHandler-derived handlers only; the rest of this
    // class's own code keeps using the private/internal members directly, unaffected) ----

    AppConfig         IMenuHost.Config       => _config;
    LocalizationData  IMenuHost.Localization => _localization;
    Screen            IMenuHost.RootScreen   => Screen.Main;

    string? IMenuHost.RebindingAction        { get => RebindingAction;        set => RebindingAction = value; }
    string? IMenuHost.GamepadRebindingAction { get => GamepadRebindingAction; set => GamepadRebindingAction = value; }

    void   IMenuHost.NavigateTo(Screen screen)               => NavigateTo(screen);
    void   IMenuHost.ClearPreset()                           => ClearPreset();
    void   IMenuHost.ApplyPreset(Rendering.VideoPreset preset) => ApplyPreset(preset);
    void   IMenuHost.ResetPicture()                          => ResetPicture();
    void   IMenuHost.ResetEq()                                => ResetEq();
    string IMenuHost.GetGamepadLabel(string configKey)        => GetGamepadLabel(configKey);
    IntPtr IMenuHost.GetGamepadGlyph(string configKey)         => GetGamepadGlyph(configKey);
    string IMenuHost.KeyboardLabel(string configKey)           => KeyboardLabel(configKey);

    Action<bool>                            IMenuHost.OnWindowModeToggle         => _onWindowModeToggle;
    Action                                  IMenuHost.OnConfigSaved              => _onConfigSaved;
    Action<AudioFilterMode>                 IMenuHost.OnFilterChanged            => _onFilterChanged;
    Action<Rendering.VideoFilterMode>       IMenuHost.OnVideoFilterChanged       => _onVideoFilterChanged;
    Action<Rendering.VideoFilterMode?>      IMenuHost.OnVideoFilterOverlayChanged => _onVideoFilterOverlayChanged;
    Action<Rendering.VideoColorFilterMode>  IMenuHost.OnVideoColorFilterChanged  => _onVideoColorFilterChanged;
    Action<Rendering.VideoMotionEffectMode> IMenuHost.OnVideoMotionEffectChanged => _onVideoMotionEffectChanged;
    Action<Rendering.OverscanMode>          IMenuHost.OnOverscanModeChanged      => _onOverscanModeChanged;
    Action<string>                          IMenuHost.OnLanguageChanged          => _onLanguageChanged;

    private IReadOnlyDictionary<Screen, IScreenHandler> BuildHandlers() =>
        new Dictionary<Screen, IScreenHandler>
        {
            [Screen.Main]             = new MainHandler(this),
            [Screen.ResumeSlots]      = new ResumeSlotsHandler(this),
            [Screen.Settings]         = new SettingsHandler(this),
            [Screen.KeyboardBindings] = new KeyboardBindingsHandler(this),
            [Screen.GamepadBindings]  = new GamepadBindingsHandler(this),
            [Screen.PlayerSelect]           = new PlayerSelectHandler(this),
            [Screen.GamepadBindingsP2]      = new GamepadBindingsHandler(this, player: 2),
            [Screen.GamepadBindingsP3]      = new GamepadBindingsHandler(this, player: 3),
            [Screen.GamepadBindingsP4]      = new GamepadBindingsHandler(this, player: 4),
            [Screen.KeyboardBindingsP2]     = new KeyboardBindingsHandler(this, player: 2),
            [Screen.KeyboardBindingsP3]     = new KeyboardBindingsHandler(this, player: 3),
            [Screen.KeyboardBindingsP4]     = new KeyboardBindingsHandler(this, player: 4),
            [Screen.Video]             = new VideoHandler(this),
            [Screen.Sound]             = new SoundHandler(this),
            [Screen.AudioFilter]       = new AudioFilterHandler(this),
            [Screen.AudioEq]           = new AudioEqHandler(this),
            [Screen.VideoFilter]       = new VideoFilterHandler(this),
            [Screen.VideoMotionEffect] = new VideoMotionEffectHandler(this),
            [Screen.VideoPicture]      = new VideoPictureHandler(this),
            [Screen.VideoPresets]      = new VideoPresetsHandler(this),
            [Screen.Language]          = new LanguageHandler(this),
        };

    // ---- Show (re-entry from in-game) ----

    public void Show()
    {
        CurrentScreen          = Screen.Main;
        SelectedIndex          = 0;
        RebindingAction        = null;
        GamepadRebindingAction = null;
        IsVisible              = true;

        if (!IsItemEnabled(0))
            NavigateCursor(1);
    }

    // ---- Keyboard input ----

    public bool HandleKey(SDL.Keycode key)
    {
        if (!IsVisible) return false;

        if (RebindingAction != null)
        {
            if (key == SDL.Keycode.Escape)
                RebindingAction = null;
            else
            {
                MenuBindingHelpers.SetBinding(_config, RebindingAction, key.ToString());
                _onConfigSaved();
                RebindingAction = null;
            }
            return true;
        }

        if (GamepadRebindingAction != null)
        {
            if (key == SDL.Keycode.Escape) GamepadRebindingAction = null;
            return true;
        }

        if (CurrentScreen == Screen.Sound && SelectedIndex == SoundHandler.VolumeIndex)
        {
            if (key == SDL.Keycode.Left)  { AdjustVolume(-5); return true; }
            if (key == SDL.Keycode.Right) { AdjustVolume( 5); return true; }
        }

        if (CurrentScreen == Screen.VideoPicture && VideoPictureHandler.IsSliderIndex(SelectedIndex))
        {
            if (key == SDL.Keycode.Left)  { AdjustPicture(SelectedIndex, -1); return true; }
            if (key == SDL.Keycode.Right) { AdjustPicture(SelectedIndex,  1); return true; }
        }

        if (CurrentScreen == Screen.AudioEq && AudioEqHandler.IsSliderIndex(SelectedIndex))
        {
            if (key == SDL.Keycode.Left)  { AdjustEq(SelectedIndex, -1); return true; }
            if (key == SDL.Keycode.Right) { AdjustEq(SelectedIndex,  1); return true; }
        }

        switch (key)
        {
            case SDL.Keycode.Escape:
                if (CurrentScreen != Screen.Main)
                    NavigateTo(ParentScreen(CurrentScreen));
                return true;

            case SDL.Keycode.Up:
                NavigateCursor(-1);
                return true;

            case SDL.Keycode.Down:
                NavigateCursor(1);
                return true;

            case SDL.Keycode.Return:
            case SDL.Keycode.Z:
            case SDL.Keycode.Space:
                ActivateCurrent();
                return true;
        }
        return false;
    }

    // ---- Gamepad input ----

    /// <summary>
    /// Called when a gamepad button is pressed during rebind mode.
    /// Returns a toast message to display, or null.
    /// Start cancels with a message when override is off; binds normally when override is on.
    /// </summary>
    public string? HandleGamepadButtonPress(string buttonName)
    {
        if (GamepadRebindingAction == null) return null;

        if (GamepadRebindingAction == "OpenMenu")
        {
            _config.GamepadHotkeyMappings["OpenMenu"] = buttonName;
            _onConfigSaved();
            GamepadRebindingAction = null;
            return null;
        }

        if (buttonName == "Start" && !_config.OverrideStartBindingProtection)
        {
            GamepadRebindingAction = null;
            return _localization.InGameRebindStartReserved;
        }
        MenuBindingHelpers.SetGamepadBinding(_config, GamepadRebindingAction, buttonName);
        _onConfigSaved();
        GamepadRebindingAction = null;
        return null;
    }

    public void HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (!IsVisible || !nav.Any) return;
        if (RebindingAction != null || GamepadRebindingAction != null) return;

        if (CurrentScreen == Screen.Sound && SelectedIndex == SoundHandler.VolumeIndex)
        {
            if (nav.Left)  { AdjustVolume(-5); return; }
            if (nav.Right) { AdjustVolume( 5); return; }
        }

        if (CurrentScreen == Screen.VideoPicture && VideoPictureHandler.IsSliderIndex(SelectedIndex))
        {
            if (nav.Left)  { AdjustPicture(SelectedIndex, -1); return; }
            if (nav.Right) { AdjustPicture(SelectedIndex,  1); return; }
        }

        if (CurrentScreen == Screen.AudioEq && AudioEqHandler.IsSliderIndex(SelectedIndex))
        {
            if (nav.Left)  { AdjustEq(SelectedIndex, -1); return; }
            if (nav.Right) { AdjustEq(SelectedIndex,  1); return; }
        }

        if (nav.Up)   NavigateCursor(-1);
        if (nav.Down) NavigateCursor(1);

        if (nav.Confirm)
            ActivateCurrent();

        if (nav.Back)
        {
            if (CurrentScreen != Screen.Main)
                NavigateTo(ParentScreen(CurrentScreen));
        }
    }

    // ---- Internal helpers ----

    private void AdjustVolume(int delta)
    {
        int next = Math.Clamp(_config.Volume + delta, 0, 100);
        if (next == _config.Volume) return;
        _config.Volume = next;
        _onVolumeChanged(next);
    }

    private void AdjustPicture(int sliderIndex, int delta)
    {
        switch (sliderIndex)
        {
            case VideoPictureHandler.BrightnessIndex:
                _config.VideoBrightness = Math.Clamp(_config.VideoBrightness + delta, -100, 100);
                break;
            case VideoPictureHandler.ContrastIndex:
                _config.VideoContrast = Math.Clamp(_config.VideoContrast + delta, -100, 100);
                break;
            case VideoPictureHandler.SaturationIndex:
                _config.VideoSaturation = Math.Clamp(_config.VideoSaturation + delta, -100, 100);
                break;
            case VideoPictureHandler.HueIndex:
                _config.VideoHue = Math.Clamp(_config.VideoHue + delta, -100, 100);
                break;
        }
        ClearPreset();
        _onPictureAdjustChanged(_config.VideoBrightness, _config.VideoContrast, _config.VideoSaturation, _config.VideoHue);
    }

    internal void ResetPicture()
    {
        ClearPreset();
        _config.VideoBrightness  = 0;
        _config.VideoContrast    = 0;
        _config.VideoSaturation  = 0;
        _config.VideoHue         = 0;
        _config.VideoColorFilter = Rendering.VideoColorFilterMode.None.ToString();
        _onPictureAdjustChanged(0, 0, 0, 0);
        _onVideoColorFilterChanged(Rendering.VideoColorFilterMode.None);
    }

    private void AdjustEq(int sliderIndex, int delta)
    {
        switch (sliderIndex)
        {
            case AudioEqHandler.BassIndex:
                _config.AudioEqBass = Math.Clamp(_config.AudioEqBass + delta, -12, 12);
                break;
            case AudioEqHandler.MidIndex:
                _config.AudioEqMid = Math.Clamp(_config.AudioEqMid + delta, -12, 12);
                break;
            case AudioEqHandler.TrebleIndex:
                _config.AudioEqTreble = Math.Clamp(_config.AudioEqTreble + delta, -12, 12);
                break;
        }
        _onAudioEqChanged(_config.AudioEqBass, _config.AudioEqMid, _config.AudioEqTreble);
    }

    internal void ResetEq()
    {
        _config.AudioEqBass   = 0;
        _config.AudioEqMid    = 0;
        _config.AudioEqTreble = 0;
        _onAudioEqChanged(0, 0, 0);
    }

    internal void ApplyPreset(Rendering.VideoPreset preset)
    {
        _config.VideoFilter        = preset.Filter.ToString();
        _config.VideoFilterOverlay = preset.Overlay.HasValue ? preset.Overlay.Value.ToString() : "None";
        _config.VideoColorFilter   = preset.ColorFilter.ToString();
        _config.VideoMotionEffect  = preset.MotionEffect.ToString();
        _config.OverscanMode       = preset.Overscan.ToString();
        _config.VideoBrightness    = preset.Brightness;
        _config.VideoContrast      = preset.Contrast;
        _config.VideoSaturation    = preset.Saturation;
        _config.VideoHue           = preset.Hue;
        _config.VideoPreset        = preset.Name;

        _onVideoFilterChanged(preset.Filter);
        _onVideoFilterOverlayChanged(preset.Overlay);
        _onVideoColorFilterChanged(preset.ColorFilter);
        _onVideoMotionEffectChanged(preset.MotionEffect);
        _onOverscanModeChanged(preset.Overscan);
        _onPictureAdjustChanged(preset.Brightness, preset.Contrast, preset.Saturation, preset.Hue);
        _onConfigSaved();
    }

    internal void ClearPreset()
    {
        _config.VideoPreset = "None";
    }

    private void NavigateCursor(int direction)
    {
        int count = ItemCount();
        int next  = SelectedIndex;
        for (int attempt = 0; attempt < count; attempt++)
        {
            next = ((next + direction) % count + count) % count;
            if (IsItemEnabled(next))
            {
                SelectedIndex = next;
                return;
            }
        }
    }

    private void NavigateTo(Screen screen)
    {
        CurrentScreen          = screen;
        SelectedIndex          = 0;
        RebindingAction        = null;
        GamepadRebindingAction = null;

        if (!IsItemEnabled(0))
            NavigateCursor(1);
    }

    private void ActivateCurrent()
    {
        if (!IsItemEnabled(SelectedIndex)) return;
        _handlers[CurrentScreen].Activate(SelectedIndex);
    }

    private int ItemCount() => _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.ItemCount : 0;

    // Instance method, not static — see InGameMenu.ParentScreen's identical doc comment for why
    // KeyboardBindings/GamepadBindings' parent depends on PlayerCount.
    private Screen ParentScreen(Screen screen) => screen switch
    {
        Screen.ResumeSlots      => Screen.Main,
        Screen.Settings         => Screen.Main,
        Screen.KeyboardBindings or Screen.GamepadBindings
            => _config.PlayerCount > 1 ? Screen.PlayerSelect : Screen.Settings,
        Screen.PlayerSelect     => Screen.Settings,
        Screen.GamepadBindingsP2 or Screen.GamepadBindingsP3 or Screen.GamepadBindingsP4
            or Screen.KeyboardBindingsP2 or Screen.KeyboardBindingsP3 or Screen.KeyboardBindingsP4
            => Screen.PlayerSelect,
        Screen.Video            => Screen.Settings,
        Screen.Sound            => Screen.Settings,
        Screen.AudioFilter      => Screen.Sound,
        Screen.AudioEq          => Screen.Sound,
        Screen.VideoFilter       => Screen.Video,
        Screen.VideoMotionEffect => Screen.Video,
        Screen.VideoPicture      => Screen.Video,
        Screen.VideoPresets      => Screen.Video,
        Screen.Language          => Screen.Settings,
        _                        => Screen.Main,
    };

    // ---- Resume-slot list ----

    private void BuildResumeOptions()
    {
        var list = new List<ResumeOption>();

        if (_saveStates.HasAutoSave)
            list.Add(new(_localization.SlotAutoSave, () => _saveStates.AutoLoad()));

        for (int i = 0; i < _saveStates.SlotCount; i++)
        {
            if (_saveStates.SlotExists(i))
            {
                int slot = i;
                string label = string.Format(_localization.SlotLabel, slot + 1);
                var meta = _saveStates.GetSlotMeta(slot);
                if (meta is not null)
                    label += $"  {meta.Timestamp.ToLocalTime():MM/dd HH:mm}";
                list.Add(new(label, () => _saveStates.LoadSlot(slot)));
            }
        }

        list.Add(new(_localization.Back, null));
        _resumeOptions = list.ToArray();
    }

    // ---- Handler dispatch (public — called by renderer and tests) ----

    public bool IsItemEnabled(int index) =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.IsItemEnabled(index) : true;

    public string GetTitle() =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.Title : "";

    public string[] GetCurrentItems() =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.GetItems() : Array.Empty<string>();

    public IntPtr GetCurrentItemIcon(int index) =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.GetItemIcon(index) : IntPtr.Zero;

    public IntPtr GetCurrentItemValueIcon(int index) =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.GetItemValueIcon(index) : IntPtr.Zero;

    public SliderItemData? GetCurrentSliderData(int index) =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.GetSliderData(index) : null;

    public bool ShowsControllerDiagram =>
        _handlers.TryGetValue(CurrentScreen, out var handler) && handler.ShowsControllerDiagram;

    public int GetCurrentSeparatorIndex() =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.SeparatorIndex : -1;

    public string? GetCurrentSeparatorLabel() =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.SeparatorLabel : null;

    public void UpdateLocalization(LocalizationData data)
    {
        _localization          = data;
        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(data, _config);
        _handlers              = BuildHandlers();
    }

    // ---- Rendering label helpers (used by binding handlers) ----

    private string KeyboardLabel(string configKey)
        => MenuBindingHelpers.KeyboardLabel(configKey, _config, _localization);

    private string GetGamepadLabel(string configKey)
        => MenuBindingHelpers.GetGamepadLabel(configKey, _config, _localization);

    private IntPtr GetGamepadGlyph(string configKey)
    {
        int player = MenuBindingHelpers.PlayerFromConfigKey(configKey);
        int index  = Math.Clamp(player - 1, 0, _glyphResolvers.Count - 1);
        return MenuBindingHelpers.GetGamepadGlyph(configKey, _config, _localization, _glyphResolvers[index]);
    }

    // Returns a pre-scaled SDL surface at bounds dimensions, rebuilding only when the bounds change.
    // The caller must not destroy the returned surface — it is owned by this instance.
    internal unsafe IntPtr GetScaledBackground(SDL.Rect bounds)
    {
        if (Background == IntPtr.Zero) return IntPtr.Zero;
        if (_scaledBackground != IntPtr.Zero && _scaledBoundsSize == (bounds.W, bounds.H))
            return _scaledBackground;

        if (_scaledBackground != IntPtr.Zero)
        {
            _onSurfaceDisposing?.Invoke(_scaledBackground);
            SDL.DestroySurface(_scaledBackground);
            _scaledBackground = IntPtr.Zero;
        }

        SDL.Surface* bgSurf = (SDL.Surface*)Background;
        int imgW = bgSurf->Width;
        int imgH = bgSurf->Height;
        if (imgW <= 0 || imgH <= 0) return IntPtr.Zero;

        float imgAspect    = (float)imgW / imgH;
        float boundsAspect = (float)bounds.W / bounds.H;
        SDL.Rect srcRect;
        if (boundsAspect > imgAspect)
        {
            int h = (int)(bounds.W / imgAspect);
            srcRect = new SDL.Rect { X = 0, Y = (bounds.H - h) / 2, W = bounds.W, H = h };
        }
        else
        {
            int w = (int)(bounds.H * imgAspect);
            srcRect = new SDL.Rect { X = (bounds.W - w) / 2, Y = 0, W = w, H = bounds.H };
        }

        IntPtr cached = SDL.CreateSurface(bounds.W, bounds.H, SDL.PixelFormat.ARGB8888);
        if (cached == IntPtr.Zero) return IntPtr.Zero;

        uint black = SDL.MapSurfaceRGB(cached, 0, 0, 0);
        SDL.FillSurfaceRect(cached, IntPtr.Zero, black);
        SDL.BlitSurfaceScaled(Background, IntPtr.Zero, cached, in srcRect, SDL.ScaleMode.Linear);

        _scaledBackground = cached;
        _scaledBoundsSize = (bounds.W, bounds.H);
        return cached;
    }

    public void Dispose()
    {
        if (_scaledBackground != IntPtr.Zero)
        {
            _onSurfaceDisposing?.Invoke(_scaledBackground);
            SDL.DestroySurface(_scaledBackground);
            _scaledBackground = IntPtr.Zero;
        }
        if (Background != IntPtr.Zero)
        {
            _onSurfaceDisposing?.Invoke(Background);
            SDL.DestroySurface(Background);
        }
    }

    /// <summary>
    /// <paramref name="ctx"/> is null for single-game mode (unchanged: exe-relative, then CWD
    /// fallback) or the active game's <see cref="GameContext"/> in multi-game mode (that
    /// game's own folder — no CWD fallback, since that fallback only ever made sense for the
    /// single-game legacy path).
    /// </summary>
    internal static string? ResolveAssetPath(string path, GameContext? ctx = null)
    {
        if (Path.IsPathRooted(path))
            return File.Exists(path) ? path : null;

        string nextToRoot = GameContext.ResolvePath(path, ctx);
        if (File.Exists(nextToRoot)) return nextToRoot;

        if (ctx is null)
        {
            string inCwd = Path.GetFullPath(path);
            if (File.Exists(inCwd)) return inCwd;
        }

        return null;
    }
}
