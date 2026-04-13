using System.Drawing;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu.
/// Handles Main, ResumeSlots, Settings, KeyBindings, and Sound screens.
/// The emulation thread stays paused until the user picks New Game or loads a save.
/// </summary>
internal sealed class MainMenuScreen : IDisposable
{
    public enum Screen { Main, ResumeSlots, Settings, KeyBindings, Sound }

    // ---- Shared binding-action table (mirrors InGameMenu) ----
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

    // ---- Settings item indices ----
    private const int SettingsItemKeyBindings = 0;
    private const int SettingsItemWindowMode  = 1;
    private const int SettingsItemFps         = 2;
    private const int SettingsItemSound       = 3;
    private const int SettingsItemCount       = 4;

    // ---- Sound screen item indices ----
    private const int SoundItemVolume     = 0;
    private const int SoundItemScrubber   = 1;
    private const int SoundItemMenuMusic  = 2;
    private const int SoundItemBack       = 3;
    private const int SoundItemCount      = 4;

    // ---- State ----
    public Screen  CurrentScreen   { get; private set; } = Screen.Main;
    public bool    IsVisible       { get; private set; } = true;
    public int     SelectedIndex   { get; private set; } = 0;
    public Bitmap? Background      { get; }
    public string? RebindingAction { get; private set; }

    // Computed each time so it reflects saves created during a play session
    public bool CanResume => _saveStates.HasAutoSave
        || Enumerable.Range(0, SaveStateManager.SlotCount).Any(_saveStates.SlotExists);

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;   // 0–100
    private readonly Action<bool>     _onScrubberToggled; // true = scrubber on
    private readonly Action<bool>     _onMenuMusicToggled; // true = music on

    // Built lazily when the player enters the ResumeSlots screen
    private ResumeOption[] _resumeOptions = Array.Empty<ResumeOption>();

    // ---- Events ----
    public event Action? NewGameChosen;
    /// <summary>Fires after the chosen save state has already been loaded.</summary>
    public event Action? ResumeChosen;
    public event Action? ExitChosen;

    // ---- Main menu item labels / enabled state ----
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
        Action<bool>     onMenuMusicToggled)
    {
        _saveStates          = saveStates;
        _config              = config;
        _onWindowModeToggle  = onWindowModeToggle;
        _onConfigSaved       = onConfigSaved;
        _onVolumeChanged     = onVolumeChanged;
        _onScrubberToggled   = onScrubberToggled;
        _onMenuMusicToggled  = onMenuMusicToggled;

        if (!string.IsNullOrWhiteSpace(bgImagePath))
        {
            string? resolved = ResolveAssetPath(bgImagePath);
            if (resolved != null)
            {
                try { Background = new Bitmap(resolved); }
                catch { /* unsupported format or corrupt file — leave null */ }
            }
        }
    }

    // ---- Show (re-entry from in-game) ----

    /// <summary>
    /// Re-displays the main menu, returning to the Main screen.
    /// Called when the player chooses "Return to Main Menu" from in-game.
    /// CanResume is re-evaluated automatically since it is now a computed property.
    /// </summary>
    public void Show()
    {
        CurrentScreen   = Screen.Main;
        SelectedIndex   = 0;
        RebindingAction = null;
        IsVisible       = true;

        // Skip Resume if no saves exist
        if (!IsItemEnabled(0))
            NavigateCursor(1);
    }

    // ---- Input ----

    public bool HandleKey(Keys key)
    {
        if (!IsVisible) return false;

        // Capture any key while waiting for a rebind
        if (RebindingAction != null)
        {
            if (key == Keys.Escape)
            {
                RebindingAction = null;
            }
            else
            {
                string keyName = key.ToString();
                if (_config.InputMappings.TryGetValue(RebindingAction, out var binding))
                    binding.Key = keyName;
                else
                    _config.InputMappings[RebindingAction] = new InputBinding(keyName, null);
                _onConfigSaved();
                RebindingAction = null;
            }
            return true;
        }

        // Left/Right adjust volume when on Sound screen at the Volume item
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
                    case 0: // New Game
                        IsVisible = false;
                        NewGameChosen?.Invoke();
                        break;
                    case 1: // Resume Game
                        BuildResumeOptions();
                        NavigateTo(Screen.ResumeSlots);
                        break;
                    case 2: // Settings
                        NavigateTo(Screen.Settings);
                        break;
                    case 3: // Exit
                        IsVisible = false;
                        ExitChosen?.Invoke();
                        break;
                }
                break;

            case Screen.ResumeSlots:
            {
                var opt = _resumeOptions[SelectedIndex];
                if (opt.Load == null)
                {
                    NavigateTo(Screen.Main); // Back
                }
                else
                {
                    opt.Load(); // load the chosen save while thread is still blocked
                    IsVisible = false;
                    ResumeChosen?.Invoke();
                }
                break;
            }

            case Screen.Settings:
                switch (SelectedIndex)
                {
                    case SettingsItemKeyBindings:
                        NavigateTo(Screen.KeyBindings);
                        break;
                    case SettingsItemWindowMode:
                        _onWindowModeToggle(_config.WindowMode != "Fullscreen");
                        break;
                    case SettingsItemFps:
                        _config.Developer.ShowFps = !_config.Developer.ShowFps;
                        _onConfigSaved();
                        break;
                    case SettingsItemSound:
                        NavigateTo(Screen.Sound);
                        break;
                }
                break;

            case Screen.KeyBindings:
            {
                var (_, configKey) = BindingActions[SelectedIndex];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    RebindingAction = configKey;
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
                    // SoundItemVolume handled by Left/Right in HandleKey, not Return
                }
                break;
        }
    }

    private void NavigateTo(Screen screen)
    {
        CurrentScreen = screen;
        SelectedIndex = 0;
        RebindingAction = null;

        // Skip disabled items at position 0 (e.g., Resume when unavailable)
        if (!IsItemEnabled(0))
            NavigateCursor(1);
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
        Screen.KeyBindings => RebindingAction != null
            ? $"PRESS KEY FOR  {BindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper()}"
            : "KEY BINDINGS",
        Screen.Sound       => "SOUND",
        _ => ""
    };

    public string[] GetCurrentItems() => CurrentScreen switch
    {
        Screen.Main        => MainItemLabels,
        Screen.ResumeSlots => _resumeOptions.Select(o => o.Label).ToArray(),
        Screen.Settings    => new[]
        {
            "Key Bindings",
            $"Window Mode: {(_config.WindowMode == "Fullscreen" ? "Fullscreen" : "Windowed")}",
            $"FPS Overlay: {(_config.Developer.ShowFps ? "On" : "Off")}",
            "Sound",
        },
        Screen.KeyBindings => BindingActions
            .Select(b => b.ConfigKey == ""
                ? "← Back"
                : $"{b.Label,-8}  {KeyLabel(b.ConfigKey)}")
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
        Screen.Main        => MainItemLabels.Length,
        Screen.ResumeSlots => _resumeOptions.Length,
        Screen.Settings    => SettingsItemCount,
        Screen.KeyBindings => BindingActions.Length,
        Screen.Sound       => SoundItemCount,
        _ => 0
    };

    private string KeyLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? "(none)" : "(none)";

    public void Dispose() => Background?.Dispose();

    /// <summary>
    /// Resolves a relative asset path by checking the executable directory first,
    /// then the working directory. Returns null if the file cannot be found.
    /// </summary>
    internal static string? ResolveAssetPath(string path)
    {
        if (Path.IsPathRooted(path))
            return File.Exists(path) ? path : null;

        // Try next to the executable (the correct location in a deployed build)
        string nextToExe = Path.Combine(AppContext.BaseDirectory, path);
        if (File.Exists(nextToExe)) return nextToExe;

        // Fall back to working directory (useful when running from within VS/Rider)
        string inCwd = Path.GetFullPath(path);
        if (File.Exists(inCwd)) return inCwd;

        return null;
    }

    // ---- Inner type ----
    private record ResumeOption(string Label, Action? Load);
}
