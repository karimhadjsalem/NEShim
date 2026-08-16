using SDL3;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Input;
using NEShim.Localization;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the in-game pause menu.
/// Rendering is delegated to MenuRenderer; this class owns navigation and actions.
/// Per-screen title, items, enabled state, and activation logic live in nested
/// ScreenHandler classes — one per Screen enum value.
/// </summary>
internal sealed partial class InGameMenu : IMenuHost
{
    private readonly ISaveManager _saveStates;
    private readonly AppConfig        _config;
    private          LocalizationData _localization;
    private readonly IGamepadGlyphResolver _glyphResolver;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action           _onReturnToMainMenu;
    private readonly Action           _onChangeGame;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<AudioFilterMode>                  _onFilterChanged;
    private readonly Action<Rendering.VideoFilterMode>        _onVideoFilterChanged;
    private readonly Action<Rendering.VideoFilterMode?>       _onVideoFilterOverlayChanged;
    private readonly Action<Rendering.VideoColorFilterMode>   _onVideoColorFilterChanged;
    private readonly Action<Rendering.VideoMotionEffectMode>  _onVideoMotionEffectChanged;
    private readonly Action<Rendering.OverscanMode>           _onOverscanModeChanged;
    private readonly Action<string>                           _onLanguageChanged;
    private readonly Action<int, int, int, int>                _onPictureAdjustChanged;
    private readonly Action<int, int, int>                    _onAudioEqChanged;

    private (string Label, string ConfigKey)[]          _bindingActions;
    private (string Label, string ConfigKey)[]          _gamepadBindingActions;
    private IReadOnlyDictionary<Screen, IScreenHandler> _handlers;

    // ---- Public state ----

    public bool   IsOpen      { get; private set; }
    public Screen Current     { get; private set; } = Screen.Root;
    public int    SelectedItem { get; private set; }

    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding             => GamepadRebindingAction != null;
    public bool    OverrideStartBindingProtection => _config.OverrideStartBindingProtection;
    public int     OpenMenuBindingIndex           => Array.FindIndex(_gamepadBindingActions, b => b.ConfigKey == "OpenMenu");
    public string  CurrentOpenMenuBinding         => _config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder");

    /// <summary>
    /// Returns the NES button config key for the currently active selection or rebind
    /// so the controller diagram can highlight the relevant button.
    /// </summary>
    public string? ActiveNesButton
    {
        get
        {
            string? rebinding = RebindingAction ?? GamepadRebindingAction;
            if (rebinding != null)
                return IsNesButtonKey(rebinding) ? rebinding : null;

            if (Current == Screen.KeyboardBindings)
            {
                var key = _bindingActions[SelectedItem].ConfigKey;
                return IsNesButtonKey(key) ? key : null;
            }
            if (Current == Screen.GamepadBindings)
            {
                var key = _gamepadBindingActions[SelectedItem].ConfigKey;
                return IsNesButtonKey(key) ? key : null;
            }
            return null;
        }
    }

    private static bool IsNesButtonKey(string key) =>
        key is "P1 Up" or "P1 Down" or "P1 Left" or "P1 Right"
             or "P1 A"  or "P1 B"   or "P1 Start" or "P1 Select";

    /// <summary>Exposes the loaded localization so stateless renderers can read strings and font family.</summary>
    public LocalizationData Localization => _localization;

    public event Action? Opened;
    public event Action? Closed;

    private const int RootItemLoadGame     = 4;
    private const int RootItemReturnToMain = 6;

    // ---- Constructor ----

    public InGameMenu(
        ISaveManager saveStates,
        AppConfig        config,
        LocalizationData localization,
        Action           onExitToDesktop,
        Action           onResetGame,
        Action           onReturnToMainMenu,
        Action<bool>     onWindowModeToggle,
        Action           onConfigSaved,
        Action<int>             onVolumeChanged,
        Action<AudioFilterMode> onFilterChanged,
        Action<Rendering.VideoFilterMode>       onVideoFilterChanged,
        Action<Rendering.VideoFilterMode?>      onVideoFilterOverlayChanged,
        Action<Rendering.VideoColorFilterMode>  onVideoColorFilterChanged,
        Action<Rendering.VideoMotionEffectMode> onVideoMotionEffectChanged,
        Action<Rendering.OverscanMode>          onOverscanModeChanged,
        Action<string>                          onLanguageChanged,
        Action<int, int, int, int>              onPictureAdjustChanged,
        Action<int, int, int>                   onAudioEqChanged,
        Action?                                 onChangeGame = null,
        IGamepadGlyphResolver?                  glyphResolver = null)
    {
        _saveStates                 = saveStates;
        _config                     = config;
        _localization               = localization;
        _glyphResolver               = glyphResolver ?? NullGamepadGlyphResolver.Instance;
        _onExitToDesktop            = onExitToDesktop;
        _onResetGame                = onResetGame;
        _onReturnToMainMenu         = onReturnToMainMenu;
        _onChangeGame               = onChangeGame ?? (() => { });
        _onWindowModeToggle         = onWindowModeToggle;
        _onConfigSaved              = onConfigSaved;
        _onVolumeChanged            = onVolumeChanged;
        _onFilterChanged            = onFilterChanged;
        _onVideoFilterChanged        = onVideoFilterChanged;
        _onVideoFilterOverlayChanged = onVideoFilterOverlayChanged;
        _onVideoColorFilterChanged   = onVideoColorFilterChanged;
        _onVideoMotionEffectChanged  = onVideoMotionEffectChanged;
        _onOverscanModeChanged       = onOverscanModeChanged;
        _onLanguageChanged          = onLanguageChanged;
        _onPictureAdjustChanged     = onPictureAdjustChanged;
        _onAudioEqChanged           = onAudioEqChanged;

        _bindingActions        = MenuBindingHelpers.BuildBindingActions(localization);
        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(localization, config, _bindingActions);
        _handlers              = BuildHandlers();
    }

    // ---- IMenuHost (explicit — for SharedScreenHandler-derived handlers only; the rest of this
    // class's own code keeps using the private/internal members directly, unaffected) ----

    AppConfig         IMenuHost.Config       => _config;
    LocalizationData  IMenuHost.Localization => _localization;
    Screen            IMenuHost.RootScreen   => Screen.Root;

    (string Label, string ConfigKey)[] IMenuHost.BindingActions        => _bindingActions;
    (string Label, string ConfigKey)[] IMenuHost.GamepadBindingActions => _gamepadBindingActions;

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
            [Screen.Root]                   = new RootHandler(this),
            [Screen.SaveSlotSelect]         = new SaveSlotSelectHandler(this),
            [Screen.Settings]               = new SettingsHandler(this),
            [Screen.KeyboardBindings]       = new KeyboardBindingsHandler(this),
            [Screen.GamepadBindings]        = new GamepadBindingsHandler(this),
            [Screen.Video]             = new VideoHandler(this),
            [Screen.Sound]             = new SoundHandler(this),
            [Screen.AudioFilter]       = new AudioFilterHandler(this),
            [Screen.AudioEq]           = new AudioEqHandler(this),
            [Screen.VideoFilter]       = new VideoFilterHandler(this),
            [Screen.VideoMotionEffect] = new VideoMotionEffectHandler(this),
            [Screen.VideoPicture]      = new VideoPictureHandler(this),
            [Screen.VideoPresets]      = new VideoPresetsHandler(this),
            [Screen.Language]          = new LanguageHandler(this),
            [Screen.ConfirmLoad]            = new ConfirmHandler(this,
                _localization.InGameLoadTitle,   _localization.InGameConfirmYesLoad,
                () => { _saveStates.LoadFromActiveSlot(); Close(); }, isWarningStyle: false),
            [Screen.ConfirmMainMenu]        = new ConfirmHandler(this,
                _localization.InGameReturnTitle, _localization.InGameConfirmYesReturn,
                () => { Close(); _onReturnToMainMenu(); }, isWarningStyle: true),
            [Screen.ConfirmExit]            = new ConfirmHandler(this,
                _localization.InGameExitTitle,   _localization.InGameConfirmYesExit,
                () => { Close(); _onExitToDesktop(); }, isWarningStyle: true),
            [Screen.ConfirmChangeGame]      = new ConfirmHandler(this,
                _localization.InGameChangeGameTitle, _localization.InGameConfirmYesChangeGame,
                () => { Close(); _onChangeGame(); }, isWarningStyle: false),
            [Screen.ControllerDisconnected] = new ControllerDisconnectedHandler(this),
        };

    // ---- Open / Close ----

    public void Open(Screen startScreen = Screen.Root)
    {
        if (IsOpen) return;
        IsOpen          = true;
        Current         = startScreen;
        SelectedItem    = 0;
        RebindingAction = null;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen                 = false;
        RebindingAction        = null;
        GamepadRebindingAction = null;
        Closed?.Invoke();
    }

    // ---- Keyboard input ----

    public bool HandleKey(SDL.Keycode key)
    {
        if (!IsOpen) return false;
        if (Current == Screen.ControllerDisconnected) return false;

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
            return true; // block all nav keys while waiting for a gamepad button
        }

        if (Current == Screen.Sound && SelectedItem == SoundHandler.VolumeIndex)
        {
            if (key == SDL.Keycode.Left)  { AdjustVolume(-5); return true; }
            if (key == SDL.Keycode.Right) { AdjustVolume( 5); return true; }
        }

        if (Current == Screen.VideoPicture && VideoPictureHandler.IsSliderIndex(SelectedItem))
        {
            if (key == SDL.Keycode.Left)  { AdjustPicture(SelectedItem, -1); return true; }
            if (key == SDL.Keycode.Right) { AdjustPicture(SelectedItem,  1); return true; }
        }

        if (Current == Screen.AudioEq && AudioEqHandler.IsSliderIndex(SelectedItem))
        {
            if (key == SDL.Keycode.Left)  { AdjustEq(SelectedItem, -1); return true; }
            if (key == SDL.Keycode.Right) { AdjustEq(SelectedItem,  1); return true; }
        }

        switch (key)
        {
            case SDL.Keycode.Escape:
                if (Current == Screen.Root)
                    Close();
                else
                    NavigateTo(ParentScreen(Current));
                return true;

            case SDL.Keycode.Up:
                MoveCursor(-1);
                return true;

            case SDL.Keycode.Down:
                MoveCursor(1);
                return true;

            case SDL.Keycode.Return:
            case SDL.Keycode.Z:
            case SDL.Keycode.Space:
                if (IsItemEnabled(SelectedItem))
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
        if (!IsOpen || !nav.Any) return;
        if (Current == Screen.ControllerDisconnected) return;
        if (RebindingAction != null || GamepadRebindingAction != null) return;

        if (Current == Screen.Sound && SelectedItem == SoundHandler.VolumeIndex)
        {
            if (nav.Left)  { AdjustVolume(-5); return; }
            if (nav.Right) { AdjustVolume( 5); return; }
        }

        if (Current == Screen.VideoPicture && VideoPictureHandler.IsSliderIndex(SelectedItem))
        {
            if (nav.Left)  { AdjustPicture(SelectedItem, -1); return; }
            if (nav.Right) { AdjustPicture(SelectedItem,  1); return; }
        }

        if (Current == Screen.AudioEq && AudioEqHandler.IsSliderIndex(SelectedItem))
        {
            if (nav.Left)  { AdjustEq(SelectedItem, -1); return; }
            if (nav.Right) { AdjustEq(SelectedItem,  1); return; }
        }

        if (nav.Up)   MoveCursor(-1);
        if (nav.Down) MoveCursor(1);

        if (nav.Confirm && IsItemEnabled(SelectedItem))
            ActivateCurrent();

        if (nav.Back)
        {
            if (Current == Screen.Root)
                Close();
            else
                NavigateTo(ParentScreen(Current));
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

    private void MoveCursor(int direction)
    {
        int count = ItemCount();
        int next  = SelectedItem;
        for (int attempt = 0; attempt < count; attempt++)
        {
            next = ((next + direction) % count + count) % count;
            if (IsItemEnabled(next))
            {
                SelectedItem = next;
                return;
            }
        }
    }

    private void NavigateTo(Screen screen)
    {
        Current      = screen;
        SelectedItem = 0;
        if (!IsItemEnabled(0))
            MoveCursor(1);
    }

    private void ActivateCurrent() => _handlers[Current].Activate(SelectedItem);

    private int ItemCount() => _handlers.TryGetValue(Current, out var handler) ? handler.ItemCount : 1;

    private static Screen ParentScreen(Screen screen) => screen switch
    {
        Screen.SaveSlotSelect   => Screen.Root,
        Screen.Settings         => Screen.Root,
        Screen.ConfirmLoad      => Screen.Root,
        Screen.ConfirmMainMenu  => Screen.Root,
        Screen.ConfirmExit      => Screen.Root,
        Screen.ConfirmChangeGame => Screen.Root,
        Screen.KeyboardBindings => Screen.Settings,
        Screen.GamepadBindings  => Screen.Settings,
        Screen.Video            => Screen.Settings,
        Screen.Sound            => Screen.Settings,
        Screen.AudioFilter      => Screen.Sound,
        Screen.AudioEq          => Screen.Sound,
        Screen.VideoFilter       => Screen.Video,
        Screen.VideoMotionEffect => Screen.Video,
        Screen.VideoPicture      => Screen.Video,
        Screen.VideoPresets      => Screen.Video,
        Screen.Language          => Screen.Settings,
        _                        => Screen.Root,
    };

    // ---- Handler dispatch (public — called by renderer and tests) ----

    public bool IsItemEnabled(int index) =>
        _handlers.TryGetValue(Current, out var handler) ? handler.IsItemEnabled(index) : true;

    public string[] GetCurrentItems() =>
        _handlers.TryGetValue(Current, out var handler) ? handler.GetItems() : Array.Empty<string>();

    public string GetTitle() =>
        _handlers.TryGetValue(Current, out var handler) ? handler.Title : "";

    public IntPtr GetCurrentItemIcon(int index) =>
        _handlers.TryGetValue(Current, out var handler) ? handler.GetItemIcon(index) : IntPtr.Zero;

    public IntPtr GetCurrentItemValueIcon(int index) =>
        _handlers.TryGetValue(Current, out var handler) ? handler.GetItemValueIcon(index) : IntPtr.Zero;

    public SliderItemData? GetCurrentSliderData(int index) =>
        _handlers.TryGetValue(Current, out var handler) ? handler.GetSliderData(index) : null;

    public bool IsConfirmStyle =>
        _handlers.TryGetValue(Current, out var handler) && handler.IsConfirmStyle;

    public bool ShowsControllerDiagram =>
        _handlers.TryGetValue(Current, out var handler) && handler.ShowsControllerDiagram;

    public void UpdateLocalization(LocalizationData data)
    {
        _localization          = data;
        _bindingActions        = MenuBindingHelpers.BuildBindingActions(data);
        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(data, _config, _bindingActions);
        _handlers              = BuildHandlers();
    }

    // ---- Rendering label helpers (used by binding handlers) ----

    private string KeyboardLabel(string configKey)
        => MenuBindingHelpers.KeyboardLabel(configKey, _config, _localization);

    private string GetGamepadLabel(string configKey)
        => MenuBindingHelpers.GetGamepadLabel(configKey, _config, _localization);

    private IntPtr GetGamepadGlyph(string configKey)
        => MenuBindingHelpers.GetGamepadGlyph(configKey, _config, _localization, _glyphResolver);
}
