using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Localization;
using NEShim.Saves;
using NEShim.Steam;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu.
/// Handles Main, ResumeSlots, Settings, KeyBindings, Video, and Sound screens.
/// The emulation thread stays paused until the user picks New Game or loads a save.
/// Per-screen title, items, enabled state, and activation logic live in nested
/// ScreenHandler classes — one per Screen enum value.
/// </summary>
internal sealed partial class MainMenuScreen : IDisposable
{
    private IReadOnlyDictionary<Screen, ScreenHandler>  _handlers;
    private (string Label, string ConfigKey)[]          _bindingActions;
    private (string Label, string ConfigKey)[]          _gamepadBindingActions;
    private ResumeOption[] _resumeOptions = Array.Empty<ResumeOption>();

    // ---- Public state ----

    public Screen  CurrentScreen   { get; private set; } = Screen.Main;
    public bool    IsVisible       { get; private set; } = true;
    public int     SelectedIndex   { get; private set; }
    public Bitmap? Background      { get; }

    // Pre-scaled background bitmap cache — rebuilt only when bounds change.
    // Avoids per-frame HighQualityBicubic scaling in DrawBackground.
    private Bitmap? _scaledBackground;
    private Size    _scaledBoundsSize;
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
                return IsNesButtonKey(rebinding) ? rebinding : null;

            if (CurrentScreen == Screen.KeyboardBindings)
            {
                var key = _bindingActions[SelectedIndex].ConfigKey;
                return IsNesButtonKey(key) ? key : null;
            }
            if (CurrentScreen == Screen.GamepadBindings)
            {
                var key = _gamepadBindingActions[SelectedIndex].ConfigKey;
                return IsNesButtonKey(key) ? key : null;
            }
            return null;
        }
    }

    private static bool IsNesButtonKey(string key) =>
        key is "P1 Up" or "P1 Down" or "P1 Left" or "P1 Right"
             or "P1 A"  or "P1 B"   or "P1 Start" or "P1 Select";

    public string MenuPosition => _config.MainMenuPosition;

    public bool CanResume => _saveStates.HasAutoSave
        || Enumerable.Range(0, SaveStateManager.SlotCount).Any(_saveStates.SlotExists);

    /// <summary>Exposes the loaded localization so stateless renderers can read strings and font family.</summary>
    public LocalizationData Localization => _localization;

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private          LocalizationData _localization;
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
    private readonly Action<int, int, int>                  _onPictureAdjustChanged;
    private readonly Action<int, int, int>                  _onAudioEqChanged;

    // ---- Events ----
    public event Action? NewGameChosen;
    /// <summary>Fires after the chosen save state has already been loaded.</summary>
    public event Action? ResumeChosen;
    public event Action? ExitChosen;

    // ---- Constructor ----

    public MainMenuScreen(
        SaveStateManager saveStates,
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
        Action<int, int, int>                  onPictureAdjustChanged,
        Action<int, int, int>                  onAudioEqChanged,
        Bitmap?          bgImage = null)
    {
        _saveStates                = saveStates;
        _config                    = config;
        _localization              = localization;
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

        _bindingActions        = MenuBindingHelpers.BuildBindingActions(localization);
        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(localization, config, _bindingActions);
        _handlers              = BuildHandlers();

        if (bgImage is not null)
        {
            Background = bgImage;
        }
        else if (!string.IsNullOrWhiteSpace(bgImagePath))
        {
            string? resolved = ResolveAssetPath(bgImagePath);
            if (resolved != null)
            {
                try { Background = new Bitmap(resolved); }
                catch { }
            }
        }
    }

    private IReadOnlyDictionary<Screen, ScreenHandler> BuildHandlers() =>
        new Dictionary<Screen, ScreenHandler>
        {
            [Screen.Main]             = new MainHandler(this),
            [Screen.ResumeSlots]      = new ResumeSlotsHandler(this),
            [Screen.Settings]         = new SettingsHandler(this),
            [Screen.KeyboardBindings] = new KeyboardBindingsHandler(this),
            [Screen.GamepadBindings]  = new GamepadBindingsHandler(this),
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

    public bool HandleKey(Keys key)
    {
        if (!IsVisible) return false;

        if (RebindingAction != null)
        {
            if (key == Keys.Escape)
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
            if (key == Keys.Escape) GamepadRebindingAction = null;
            return true;
        }

        if (CurrentScreen == Screen.Sound && SelectedIndex == SoundHandler.VolumeIndex)
        {
            if (key == Keys.Left)  { AdjustVolume(-5); return true; }
            if (key == Keys.Right) { AdjustVolume( 5); return true; }
        }

        if (CurrentScreen == Screen.VideoPicture && VideoPictureHandler.IsSliderIndex(SelectedIndex))
        {
            if (key == Keys.Left)  { AdjustPicture(SelectedIndex, -1); return true; }
            if (key == Keys.Right) { AdjustPicture(SelectedIndex,  1); return true; }
        }

        if (CurrentScreen == Screen.AudioEq && AudioEqHandler.IsSliderIndex(SelectedIndex))
        {
            if (key == Keys.Left)  { AdjustEq(SelectedIndex, -1); return true; }
            if (key == Keys.Right) { AdjustEq(SelectedIndex,  1); return true; }
        }

        switch (key)
        {
            case Keys.Escape:
                if (CurrentScreen != Screen.Main)
                    NavigateTo(ParentScreen(CurrentScreen));
                return true;

            case Keys.Up:
                NavigateCursor(-1);
                return true;

            case Keys.Down:
                NavigateCursor(1);
                return true;

            case Keys.Return:
            case Keys.Z:
            case Keys.Space:
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
        }
        ClearPreset();
        _onPictureAdjustChanged(_config.VideoBrightness, _config.VideoContrast, _config.VideoSaturation);
    }

    internal void ResetPicture()
    {
        ClearPreset();
        _config.VideoBrightness  = 0;
        _config.VideoContrast    = 0;
        _config.VideoSaturation  = 0;
        _config.VideoColorFilter = Rendering.VideoColorFilterMode.None.ToString();
        _onPictureAdjustChanged(0, 0, 0);
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
        _config.VideoPreset        = preset.Name;

        _onVideoFilterChanged(preset.Filter);
        _onVideoFilterOverlayChanged(preset.Overlay);
        _onVideoColorFilterChanged(preset.ColorFilter);
        _onVideoMotionEffectChanged(preset.MotionEffect);
        _onOverscanModeChanged(preset.Overscan);
        _onPictureAdjustChanged(preset.Brightness, preset.Contrast, preset.Saturation);
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

    private static Screen ParentScreen(Screen screen) => screen switch
    {
        Screen.ResumeSlots      => Screen.Main,
        Screen.Settings         => Screen.Main,
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
        _                        => Screen.Main,
    };

    // ---- Resume-slot list ----

    private void BuildResumeOptions()
    {
        var list = new List<ResumeOption>();

        if (_saveStates.HasAutoSave)
            list.Add(new(_localization.SlotAutoSave, () => _saveStates.AutoLoad()));

        for (int i = 0; i < SaveStateManager.SlotCount; i++)
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

    public Bitmap? GetCurrentItemIcon(int index) =>
        _handlers.TryGetValue(CurrentScreen, out var handler) ? handler.GetItemIcon(index) : null;

    public void UpdateLocalization(LocalizationData data)
    {
        _localization          = data;
        _bindingActions        = MenuBindingHelpers.BuildBindingActions(data);
        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(data, _config, _bindingActions);
        _handlers              = BuildHandlers();
    }

    // ---- Rendering label helpers (used by binding handlers) ----

    private string KeyboardLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? _localization.BindNone : _localization.BindNone;

    private string GetGamepadLabel(string configKey)
    {
        if (configKey == "OpenMenu")
            return _config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder");

        if (SteamInputManager.IsUsingNativeActions()
            && SteamInputManager.NesButtonToAction.TryGetValue(configKey, out var actionName))
            return SteamInputManager.GetNativeLabel(actionName);

        return _config.InputMappings.TryGetValue(configKey, out var b)
            ? b.GamepadButton ?? _localization.BindNone
            : _localization.BindNone;
    }

    private string AudioFilterDisplayName(AudioFilterMode mode) => mode switch
    {
        AudioFilterMode.Default       => _localization.AudioFilterDefault,
        AudioFilterMode.Warm          => _localization.AudioFilterWarm,
        AudioFilterMode.PseudoStereo  => _localization.AudioFilterPseudoStereo,
        AudioFilterMode.WarmStereo    => _localization.AudioFilterWarmStereo,
        AudioFilterMode.Compression   => _localization.AudioFilterCompression,
        AudioFilterMode.BassBoost     => _localization.AudioFilterBassBoost,
        AudioFilterMode.Saturation    => _localization.AudioFilterSaturation,
        AudioFilterMode.DmcStabilizer => _localization.AudioFilterDmcStabilizer,
        _                             => mode.ToString(),
    };

    // Returns a pre-scaled Bitmap at bounds.Size, rebuilding only when the bounds change.
    // The caller must not dispose the returned bitmap — it is owned by this instance.
    internal Bitmap? GetScaledBackground(Rectangle bounds)
    {
        if (Background == null) return null;
        if (_scaledBackground != null && _scaledBoundsSize == bounds.Size)
            return _scaledBackground;

        _scaledBackground?.Dispose();
        _scaledBackground = null;

        float imgAspect    = (float)Background.Width / Background.Height;
        float boundsAspect = (float)bounds.Width / bounds.Height;
        Rectangle dest;
        if (boundsAspect > imgAspect)
        {
            int h = (int)(bounds.Width / imgAspect);
            dest = new Rectangle(0, (bounds.Height - h) / 2, bounds.Width, h);
        }
        else
        {
            int w = (int)(bounds.Height * imgAspect);
            dest = new Rectangle((bounds.Width - w) / 2, 0, w, bounds.Height);
        }

        var cached = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var cg = Graphics.FromImage(cached);
        cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
        cg.CompositingMode   = CompositingMode.SourceCopy;
        using var black = new SolidBrush(Color.Black);
        cg.FillRectangle(black, 0, 0, bounds.Width, bounds.Height);
        cg.CompositingMode = CompositingMode.SourceOver;
        cg.DrawImage(Background, dest);

        _scaledBackground = cached;
        _scaledBoundsSize = bounds.Size;
        return cached;
    }

    public void Dispose()
    {
        _scaledBackground?.Dispose();
        Background?.Dispose();
    }

    internal static string? ResolveAssetPath(string path)
    {
        if (Path.IsPathRooted(path))
            return File.Exists(path) ? path : null;

        string nextToExe = Path.Combine(AppContext.BaseDirectory, path);
        if (File.Exists(nextToExe)) return nextToExe;

        string inCwd = Path.GetFullPath(path);
        if (File.Exists(inCwd)) return inCwd;

        return null;
    }
}
