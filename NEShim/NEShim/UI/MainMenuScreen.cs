using System.Drawing;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Localization;
using NEShim.Saves;
using NEShim.Steam;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu.
/// Handles Main, ResumeSlots, Settings, KeyBindings, Video, and Sound screens.
/// The emulation thread stays paused until the user picks New Game or loads a save.
/// </summary>
internal sealed class MainMenuScreen : IDisposable
{
    public enum Screen { Main, ResumeSlots, Settings, KeyboardBindings, GamepadBindings, Video, Sound }

    // Binding-action table — labels are localised at construction time.
    private readonly (string Label, string ConfigKey)[] _bindingActions;

    // ---- Settings: 5 items ----
    private const int SettingsItemKeyboardBindings = 0;
    private const int SettingsItemGamepadBindings  = 1;
    private const int SettingsItemVideo            = 2;
    private const int SettingsItemSound            = 3;
    private const int SettingsItemBack             = 4;
    private const int SettingsItemCount            = 5;

    // ---- Video screen: 4 items ----
    private const int VideoItemWindowMode = 0;
    private const int VideoItemGraphics   = 1;
    private const int VideoItemFps        = 2;
    private const int VideoItemBack       = 3;
    private const int VideoItemCount      = 4;

    // ---- Sound screen: 4 items ----
    private const int SoundItemVolume    = 0;
    private const int SoundItemScrubber  = 1;
    private const int SoundItemMenuMusic = 2;
    private const int SoundItemBack      = 3;
    private const int SoundItemCount     = 4;

    private const int MainItemCount  = 4;
    private const int MainItemResume = 1;

    // ---- State ----
    public Screen  CurrentScreen   { get; private set; } = Screen.Main;
    public bool    IsVisible       { get; private set; } = true;
    public int     SelectedIndex   { get; private set; } = 0;
    public Bitmap? Background      { get; }
    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding        => GamepadRebindingAction != null;

    /// <summary>Current main menu panel position, read from config.</summary>
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
    private readonly Action<bool>     _onScrubberToggled;
    private readonly Action<bool>     _onMenuMusicToggled;
    private readonly Action<bool>     _onGraphicsScalerToggled;

    private ResumeOption[] _resumeOptions = Array.Empty<ResumeOption>();

    // ---- Events ----
    public event Action? NewGameChosen;
    /// <summary>Fires after the chosen save state has already been loaded.</summary>
    public event Action? ResumeChosen;
    public event Action? ExitChosen;

    public MainMenuScreen(
        SaveStateManager saveStates,
        AppConfig        config,
        LocalizationData localization,
        string?          bgImagePath,
        Action<bool>     onWindowModeToggle,
        Action           onConfigSaved,
        Action<int>      onVolumeChanged,
        Action<bool>     onScrubberToggled,
        Action<bool>     onMenuMusicToggled,
        Action<bool>     onGraphicsScalerToggled)
    {
        _saveStates              = saveStates;
        _config                  = config;
        _localization            = localization;
        _onWindowModeToggle      = onWindowModeToggle;
        _onConfigSaved           = onConfigSaved;
        _onVolumeChanged         = onVolumeChanged;
        _onScrubberToggled       = onScrubberToggled;
        _onMenuMusicToggled      = onMenuMusicToggled;
        _onGraphicsScalerToggled = onGraphicsScalerToggled;

        _bindingActions = new (string, string)[]
        {
            (localization.BindUp,     "P1 Up"),
            (localization.BindDown,   "P1 Down"),
            (localization.BindLeft,   "P1 Left"),
            (localization.BindRight,  "P1 Right"),
            (localization.BindA,      "P1 A"),
            (localization.BindB,      "P1 B"),
            (localization.BindStart,  "P1 Start"),
            (localization.BindSelect, "P1 Select"),
            (localization.Back,       ""),
        };

        if (!string.IsNullOrWhiteSpace(bgImagePath))
        {
            string? resolved = ResolveAssetPath(bgImagePath);
            if (resolved != null)
            {
                try { Background = new Bitmap(resolved); }
                catch { }
            }
        }
    }

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
                SetBinding(RebindingAction, key.ToString());
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

        // Left/Right: volume on Sound screen
        if (CurrentScreen == Screen.Sound && SelectedIndex == SoundItemVolume)
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
                Activate();
                return true;
        }
        return false;
    }

    // ---- Gamepad input ----

    /// <summary>
    /// Called when a gamepad button is pressed during rebind mode.
    /// Returns a toast message to display, or null if no message is needed.
    /// Start cancels and returns an explanatory message; anything else binds.
    /// </summary>
    public string? HandleGamepadButtonPress(string buttonName)
    {
        if (GamepadRebindingAction == null) return null;
        if (buttonName == "Start")
        {
            GamepadRebindingAction = null;
            return _localization.InGameRebindStartReserved;
        }
        SetGamepadBinding(GamepadRebindingAction, buttonName);
        _onConfigSaved();
        GamepadRebindingAction = null;
        return null;
    }

    public void HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (!IsVisible || !nav.Any) return;
        if (RebindingAction != null || GamepadRebindingAction != null) return;

        if (CurrentScreen == Screen.Sound && SelectedIndex == SoundItemVolume)
        {
            if (nav.Left)  { AdjustVolume(-5); return; }
            if (nav.Right) { AdjustVolume( 5); return; }
        }

        if (nav.Up)   NavigateCursor(-1);
        if (nav.Down) NavigateCursor(1);

        if (nav.Confirm)
            Activate();

        if (nav.Back)
        {
            if (CurrentScreen != Screen.Main)
                NavigateTo(ParentScreen(CurrentScreen));
        }
    }

    // ---- Mouse input ----

    /// <summary>Highlights the item under the cursor. Returns true if repaint needed.</summary>
    public bool HandleMouseMove(System.Drawing.Point p, System.Drawing.Rectangle bounds)
    {
        if (!IsVisible || RebindingAction != null) return false;
        int hit = MainMenuRenderer.HitTestItem(p, bounds, this);
        if (hit >= 0 && IsItemEnabled(hit) && hit != SelectedIndex)
        {
            SelectedIndex = hit;
            return true;
        }
        return false;
    }

    /// <summary>Activates the item under the cursor. Returns true if repaint needed.</summary>
    public bool HandleMouseClick(System.Drawing.Point p, System.Drawing.Rectangle bounds)
    {
        if (!IsVisible) return false;
        if (RebindingAction != null) return true;
        int hit = MainMenuRenderer.HitTestItem(p, bounds, this);
        if (hit >= 0 && IsItemEnabled(hit))
        {
            SelectedIndex = hit;
            Activate();
            return true;
        }
        return false;
    }

    // ---- Internal helpers ----

    private void AdjustVolume(int delta)
    {
        int next = Math.Clamp(_config.Volume + delta, 0, 100);
        if (next == _config.Volume) return;
        _config.Volume = next;
        _onVolumeChanged(next);
    }

    private void NavigateCursor(int dir)
    {
        int count = ItemCount();
        int next  = SelectedIndex;
        for (int attempt = 0; attempt < count; attempt++)
        {
            next = ((next + dir) % count + count) % count;
            if (IsItemEnabled(next))
            {
                SelectedIndex = next;
                return;
            }
        }
    }

    private void Activate()
    {
        if (!IsItemEnabled(SelectedIndex)) return;

        switch (CurrentScreen)
        {
            case Screen.Main:
                switch (SelectedIndex)
                {
                    case 0:
                        IsVisible = false;
                        NewGameChosen?.Invoke();
                        break;
                    case 1:
                        BuildResumeOptions();
                        NavigateTo(Screen.ResumeSlots);
                        break;
                    case 2:
                        NavigateTo(Screen.Settings);
                        break;
                    case 3:
                        IsVisible = false;
                        ExitChosen?.Invoke();
                        break;
                }
                break;

            case Screen.ResumeSlots:
            {
                var opt = _resumeOptions[SelectedIndex];
                if (opt.Load == null)
                    NavigateTo(Screen.Main);
                else
                {
                    opt.Load();
                    IsVisible = false;
                    ResumeChosen?.Invoke();
                }
                break;
            }

            case Screen.Settings:
                switch (SelectedIndex)
                {
                    case SettingsItemKeyboardBindings: NavigateTo(Screen.KeyboardBindings); break;
                    case SettingsItemGamepadBindings:  NavigateTo(Screen.GamepadBindings);  break;
                    case SettingsItemVideo:            NavigateTo(Screen.Video);            break;
                    case SettingsItemSound:            NavigateTo(Screen.Sound);            break;
                    case SettingsItemBack:             NavigateTo(Screen.Main);             break;
                }
                break;

            case Screen.Video:
                switch (SelectedIndex)
                {
                    case VideoItemWindowMode:
                        _onWindowModeToggle(_config.WindowMode != "Fullscreen");
                        break;
                    case VideoItemFps:
                        _config.ShowFps = !_config.ShowFps;
                        _onConfigSaved();
                        break;
                    case VideoItemGraphics:
                        bool smoothOn = !_config.GraphicsSmoothingEnabled;
                        _config.GraphicsSmoothingEnabled = smoothOn;
                        _onGraphicsScalerToggled(smoothOn);
                        break;
                    case VideoItemBack:
                        NavigateTo(Screen.Settings);
                        break;
                }
                break;

            case Screen.KeyboardBindings:
            {
                var (_, configKey) = _bindingActions[SelectedIndex];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    RebindingAction = configKey;
                break;
            }

            case Screen.GamepadBindings:
            {
                var (_, configKey) = _bindingActions[SelectedIndex];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    GamepadRebindingAction = configKey;
                break;
            }

            case Screen.Sound:
                switch (SelectedIndex)
                {
                    case SoundItemScrubber:
                        bool scrubOn = !_config.SoundScrubberEnabled;
                        _config.SoundScrubberEnabled = scrubOn;
                        _onScrubberToggled(scrubOn);
                        break;
                    case SoundItemMenuMusic:
                        bool musicOn = !_config.MainMenuMusicEnabled;
                        _config.MainMenuMusicEnabled = musicOn;
                        _onMenuMusicToggled(musicOn);
                        break;
                    case SoundItemBack:
                        NavigateTo(Screen.Settings);
                        break;
                }
                break;
        }
    }

    private static Screen ParentScreen(Screen screen) => screen switch
    {
        Screen.ResumeSlots      => Screen.Main,
        Screen.Settings         => Screen.Main,
        Screen.KeyboardBindings => Screen.Settings,
        Screen.GamepadBindings  => Screen.Settings,
        Screen.Video            => Screen.Settings,
        Screen.Sound            => Screen.Settings,
        _                       => Screen.Main,
    };

    private void NavigateTo(Screen screen)
    {
        CurrentScreen          = screen;
        SelectedIndex          = 0;
        RebindingAction        = null;
        GamepadRebindingAction = null;

        if (!IsItemEnabled(0))
            NavigateCursor(1);
    }

    // ---- Key binding helpers ----

    private void SetBinding(string action, string keyName)
    {
        foreach (var kvp in _config.InputMappings)
        {
            if (kvp.Key != action && kvp.Value.Key == keyName)
                kvp.Value.Key = null;
        }

        if (_config.InputMappings.TryGetValue(action, out var binding))
            binding.Key = keyName;
        else
            _config.InputMappings[action] = new InputBinding(keyName, null);
    }

    private void SetGamepadBinding(string action, string buttonName)
    {
        foreach (var kvp in _config.InputMappings)
        {
            if (kvp.Key != action && kvp.Value.GamepadButton == buttonName)
                kvp.Value.GamepadButton = null;
        }

        if (_config.InputMappings.TryGetValue(action, out var binding))
            binding.GamepadButton = buttonName;
        else
            _config.InputMappings[action] = new InputBinding(null, buttonName);
    }

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

    // ---- Rendering helpers ----

    public string GetTitle() => CurrentScreen switch
    {
        Screen.Main        => _localization.MainMenuTitle,
        Screen.ResumeSlots => _localization.MainMenuLoadTitle,
        Screen.Settings    => _localization.SettingsTitle,
        Screen.KeyboardBindings => RebindingAction != null
            ? string.Format(_localization.PressKeyTitle,
                _bindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper())
            : _localization.SettingsKeyboard.ToUpper(),
        Screen.GamepadBindings => GamepadRebindingAction != null
            ? string.Format(_localization.PressButtonTitle,
                _bindingActions.First(b => b.ConfigKey == GamepadRebindingAction).Label.ToUpper())
            : _localization.SettingsGamepad.ToUpper(),
        Screen.Video       => _localization.VideoTitle,
        Screen.Sound       => _localization.SoundTitle,
        _ => ""
    };

    public string[] GetCurrentItems() => CurrentScreen switch
    {
        Screen.Main => new[]
        {
            _localization.MainMenuNewGame,
            _localization.MainMenuResumeGame,
            _localization.MainMenuSettings,
            _localization.MainMenuExit,
        },
        Screen.ResumeSlots => _resumeOptions.Select(o => o.Label).ToArray(),
        Screen.Settings => new[]
        {
            _localization.SettingsKeyboard,
            _localization.SettingsGamepad,
            _localization.SettingsVideo,
            _localization.SettingsSound,
            _localization.Back,
        },
        Screen.Video => new[]
        {
            _config.WindowMode == "Fullscreen" ? _localization.VideoWindowFullscreen : _localization.VideoWindowWindowed,
            _config.GraphicsSmoothingEnabled   ? _localization.VideoGraphicsSmooth   : _localization.VideoGraphicsOriginal,
            _config.ShowFps                    ? _localization.VideoFpsOn            : _localization.VideoFpsOff,
            _localization.Back,
        },
        Screen.KeyboardBindings => _bindingActions
            .Select(b => b.ConfigKey == ""
                ? _localization.Back
                : $"{b.Label,-8}  {KeyboardLabel(b.ConfigKey)}")
            .ToArray(),
        Screen.GamepadBindings => _bindingActions
            .Select(b => b.ConfigKey == ""
                ? _localization.Back
                : $"{b.Label,-8}  {GetGamepadLabel(b.ConfigKey)}")
            .ToArray(),
        Screen.Sound => new[]
        {
            string.Format(_localization.SoundVolume, _config.Volume),
            _config.SoundScrubberEnabled     ? _localization.SoundScrubberOn : _localization.SoundScrubberOff,
            _config.MainMenuMusicEnabled     ? _localization.SoundMusicOn    : _localization.SoundMusicOff,
            _localization.Back,
        },
        _ => Array.Empty<string>()
    };

    public bool IsItemEnabled(int idx)
    {
        if (CurrentScreen == Screen.Main && idx == MainItemResume && !CanResume)
            return false;

        // When a native Steam controller is active, in-game rebinding is not possible —
        // the user remaps via Steam's controller configurator. Show labels read-only.
        if (CurrentScreen == Screen.GamepadBindings
            && SteamInputManager.IsUsingNativeActions()
            && _bindingActions[idx].ConfigKey != "")
            return false;

        return true;
    }

    private int ItemCount() => CurrentScreen switch
    {
        Screen.Main               => MainItemCount,
        Screen.ResumeSlots        => _resumeOptions.Length,
        Screen.Settings           => SettingsItemCount,
        Screen.KeyboardBindings   => _bindingActions.Length,
        Screen.GamepadBindings    => _bindingActions.Length,
        Screen.Video              => VideoItemCount,
        Screen.Sound              => SoundItemCount,
        _ => 0
    };

    private string KeyboardLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? "(none)" : "(none)";

    private string GetGamepadLabel(string nesButton)
    {
        if (SteamInputManager.IsUsingNativeActions()
            && SteamInputManager.NesButtonToAction.TryGetValue(nesButton, out var actionName))
            return SteamInputManager.GetNativeLabel(actionName);

        return _config.InputMappings.TryGetValue(nesButton, out var b)
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

    private record ResumeOption(string Label, Action? Load);
}
