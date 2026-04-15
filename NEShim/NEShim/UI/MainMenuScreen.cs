using System.Drawing;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu.
/// Handles Main, ResumeSlots, Settings, KeyBindings, Video, and Sound screens.
/// The emulation thread stays paused until the user picks New Game or loads a save.
/// </summary>
internal sealed class MainMenuScreen : IDisposable
{
    public enum Screen { Main, ResumeSlots, Settings, KeyboardBindings, GamepadBindings, Video, Sound }

    // ---- Shared binding-action table ----
    private static readonly (string Label, string ConfigKey)[] BindingActions =
    {
        ("Up",     "P1 Up"),
        ("Down",   "P1 Down"),
        ("Left",   "P1 Left"),
        ("Right",  "P1 Right"),
        ("A",      "P1 A"),
        ("B",      "P1 B"),
        ("Start",  "P1 Start"),
        ("Select", "P1 Select"),
        ("← Back", ""),
    };

    // ---- Settings: 4 items ----
    private const int SettingsItemKeyboardBindings = 0;
    private const int SettingsItemGamepadBindings  = 1;
    private const int SettingsItemVideo            = 2;
    private const int SettingsItemSound            = 3;
    private const int SettingsItemCount            = 4;

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

    // ---- State ----
    public Screen  CurrentScreen   { get; private set; } = Screen.Main;
    public bool    IsVisible       { get; private set; } = true;
    public int     SelectedIndex   { get; private set; } = 0;
    public Bitmap? Background      { get; }
    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding        => GamepadRebindingAction != null;
    public bool    IsGamepadOverriddenBySteam => Steam.SteamInputManager.HasConnectedController;

    /// <summary>Current main menu panel position, read from config.</summary>
    public string MenuPosition => _config.MainMenuPosition;

    public bool CanResume => _saveStates.HasAutoSave
        || Enumerable.Range(0, SaveStateManager.SlotCount).Any(_saveStates.SlotExists);

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
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

    // ---- Main menu items ----
    private static readonly string[] MainItemLabels = { "New Game", "Resume Game", "Settings", "Exit" };
    private const int MainItemResume = 1;

    public MainMenuScreen(
        SaveStateManager saveStates,
        AppConfig        config,
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
        _onWindowModeToggle      = onWindowModeToggle;
        _onConfigSaved           = onConfigSaved;
        _onVolumeChanged         = onVolumeChanged;
        _onScrubberToggled       = onScrubberToggled;
        _onMenuMusicToggled      = onMenuMusicToggled;
        _onGraphicsScalerToggled = onGraphicsScalerToggled;

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
                if (CurrentScreen == Screen.Main)
                {
                    IsVisible = false;
                    ExitChosen?.Invoke();
                }
                else
                    NavigateTo(Screen.Main);
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
    /// Processes edge-triggered gamepad menu navigation.
    /// Must be called on the UI thread (via BeginInvoke from the emulation thread).
    /// </summary>
    /// <summary>
    /// Called (on the UI thread) when a gamepad button is pressed during gamepad rebind mode.
    /// B or Back cancels; any other button sets the binding.
    /// </summary>
    public void HandleGamepadButtonPress(string buttonName)
    {
        if (GamepadRebindingAction == null) return;
        if (buttonName == "B" || buttonName == "Back")
        {
            GamepadRebindingAction = null;
            return;
        }
        SetGamepadBinding(GamepadRebindingAction, buttonName);
        _onConfigSaved();
        GamepadRebindingAction = null;
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
            if (CurrentScreen == Screen.Main)
            {
                IsVisible = false;
                ExitChosen?.Invoke();
            }
            else
                NavigateTo(Screen.Main);
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
                }
                break;

            case Screen.Video:
                switch (SelectedIndex)
                {
                    case VideoItemWindowMode:
                        _onWindowModeToggle(_config.WindowMode != "Fullscreen");
                        break;
                    case VideoItemFps:
                        _config.Developer.ShowFps = !_config.Developer.ShowFps;
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
                var (_, configKey) = BindingActions[SelectedIndex];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    RebindingAction = configKey;
                break;
            }

            case Screen.GamepadBindings:
            {
                var (_, configKey) = BindingActions[SelectedIndex];
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
            list.Add(new("Auto Save", () => _saveStates.AutoLoad()));

        for (int i = 0; i < SaveStateManager.SlotCount; i++)
        {
            if (_saveStates.SlotExists(i))
            {
                int slot = i;
                string label = _saveStates.SlotLabel(slot);
                list.Add(new(label, () => _saveStates.LoadSlot(slot)));
            }
        }

        list.Add(new("← Back", null));
        _resumeOptions = list.ToArray();
    }

    // ---- Rendering helpers ----

    public string GetTitle() => CurrentScreen switch
    {
        Screen.Main        => "MAIN MENU",
        Screen.ResumeSlots => "LOAD GAME",
        Screen.Settings    => "SETTINGS",
        Screen.KeyboardBindings => RebindingAction != null
            ? $"PRESS KEY FOR  {BindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper()}"
            : "KEYBOARD CONTROLS",

        Screen.GamepadBindings => GamepadRebindingAction != null
            ? $"PRESS BUTTON FOR  {BindingActions.First(b => b.ConfigKey == GamepadRebindingAction).Label.ToUpper()}"
            : "GAMEPAD CONTROLS",
        Screen.Video       => "VIDEO",
        Screen.Sound       => "SOUND",
        _ => ""
    };

    public string[] GetCurrentItems() => CurrentScreen switch
    {
        Screen.Main        => MainItemLabels,
        Screen.ResumeSlots => _resumeOptions.Select(o => o.Label).ToArray(),
        Screen.Settings    => new[]
        {
            "Keyboard Controls",
            "Gamepad Controls",
            "Video",
            "Sound",
        },
        Screen.Video => new[]
        {
            $"Window Mode: {(_config.WindowMode == "Fullscreen" ? "Fullscreen" : "Windowed")}",
            $"Graphics: {(_config.GraphicsSmoothingEnabled ? "Smooth" : "Original")}",
            $"FPS Overlay: {(_config.Developer.ShowFps ? "On" : "Off")}",
            "← Back",
        },
        Screen.KeyboardBindings => BindingActions
            .Select(b => b.ConfigKey == ""
                ? "← Back"
                : $"{b.Label,-8}  {KeyboardLabel(b.ConfigKey)}")
            .ToArray(),

        Screen.GamepadBindings => BindingActions
            .Select(b => b.ConfigKey == ""
                ? "← Back"
                : $"{b.Label,-8}  {GamepadLabel(b.ConfigKey)}")
            .ToArray(),
        Screen.Sound => new[]
        {
            $"◀  Volume: {_config.Volume}  ▶",
            $"Sound Scrubber: {(_config.SoundScrubberEnabled ? "On" : "Off")}",
            $"Menu Music: {(_config.MainMenuMusicEnabled ? "On" : "Off")}",
            "← Back",
        },
        _ => Array.Empty<string>()
    };

    public bool IsItemEnabled(int idx)
    {
        if (CurrentScreen == Screen.Main && idx == MainItemResume && !CanResume)
            return false;
        return true;
    }

    private int ItemCount() => CurrentScreen switch
    {
        Screen.Main               => MainItemLabels.Length,
        Screen.ResumeSlots        => _resumeOptions.Length,
        Screen.Settings           => SettingsItemCount,
        Screen.KeyboardBindings   => BindingActions.Length,
        Screen.GamepadBindings    => BindingActions.Length,
        Screen.Video              => VideoItemCount,
        Screen.Sound              => SoundItemCount,
        _ => 0
    };

    private string KeyboardLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? "(none)" : "(none)";

    private string GamepadLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.GamepadButton ?? "(none)" : "(none)";

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
