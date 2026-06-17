using System.Drawing;
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
    private readonly (string Label, string ConfigKey)[] _bindingActions;
    private readonly (string Label, string ConfigKey)[] _gamepadBindingActions;
    private readonly IReadOnlyDictionary<Screen, ScreenHandler> _handlers;
    private ResumeOption[] _resumeOptions = Array.Empty<ResumeOption>();

    // ---- Public state ----

    public Screen  CurrentScreen   { get; private set; } = Screen.Main;
    public bool    IsVisible       { get; private set; } = true;
    public int     SelectedIndex   { get; private set; }
    public Bitmap? Background      { get; }
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
    private readonly LocalizationData _localization;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<AudioFilterMode>           _onFilterChanged;
    private readonly Action<bool>                      _onMenuMusicToggled;
    private readonly Action<Rendering.VideoFilterMode>      _onVideoFilterChanged;
    private readonly Action<Rendering.VideoColorFilterMode> _onVideoColorFilterChanged;
    private readonly Action<Rendering.OverscanMode>         _onOverscanModeChanged;

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
        Action<Rendering.VideoFilterMode>      onVideoFilterChanged,
        Action<Rendering.VideoColorFilterMode> onVideoColorFilterChanged,
        Action<Rendering.OverscanMode>         onOverscanModeChanged,
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
        _onVideoFilterChanged      = onVideoFilterChanged;
        _onVideoColorFilterChanged = onVideoColorFilterChanged;
        _onOverscanModeChanged     = onOverscanModeChanged;

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
            [Screen.Video]            = new VideoHandler(this),
            [Screen.Sound]            = new SoundHandler(this),
            [Screen.AudioFilter]      = new AudioFilterHandler(this),
            [Screen.VideoFilter]      = new VideoFilterHandler(this),
            [Screen.VideoColorFilter] = new VideoColorFilterHandler(this),
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
        Screen.VideoFilter      => Screen.Video,
        Screen.VideoColorFilter => Screen.Video,
        _                       => Screen.Main,
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

    // ---- Rendering label helpers (used by binding handlers) ----

    private string KeyboardLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? "(none)" : "(none)";

    private string GetGamepadLabel(string configKey)
    {
        if (configKey == "OpenMenu")
            return _config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder");

        if (SteamInputManager.IsUsingNativeActions()
            && SteamInputManager.NesButtonToAction.TryGetValue(configKey, out var actionName))
            return SteamInputManager.GetNativeLabel(actionName);

        return _config.InputMappings.TryGetValue(configKey, out var b)
            ? b.GamepadButton ?? "(none)"
            : "(none)";
    }

    public void Dispose() => Background?.Dispose();

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
