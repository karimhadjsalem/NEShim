using System.Drawing;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the pre-game main menu.
/// Handles Main, ResumeSlots, Settings, and KeyBindings screens.
/// The emulation thread stays paused until the user picks New Game or loads a save.
/// </summary>
internal sealed class MainMenuScreen : IDisposable
{
    public enum Screen { Main, ResumeSlots, Settings, KeyBindings }

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
        Action           onConfigSaved)
    {
        _saveStates         = saveStates;
        _config             = config;
        _onWindowModeToggle = onWindowModeToggle;
        _onConfigSaved      = onConfigSaved;

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
                    case 0: // Key Bindings
                        NavigateTo(Screen.KeyBindings);
                        break;
                    case 1: // Fullscreen
                        _onWindowModeToggle(true);
                        break;
                    case 2: // Windowed
                        _onWindowModeToggle(false);
                        break;
                    case 3: // FPS Overlay toggle
                        _config.Developer.ShowFps = !_config.Developer.ShowFps;
                        _onConfigSaved();
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
        _ => ""
    };

    public string[] GetCurrentItems() => CurrentScreen switch
    {
        Screen.Main        => MainItemLabels,
        Screen.ResumeSlots => _resumeOptions.Select(o => o.Label).ToArray(),
        Screen.Settings    => new[]
        {
            "Key Bindings",
            "Fullscreen",
            "Windowed",
            $"FPS Overlay: {(_config.Developer.ShowFps ? "On" : "Off")}",
        },
        Screen.KeyBindings => BindingActions
            .Select(b => b.ConfigKey == ""
                ? "← Back"
                : $"{b.Label,-8}  {KeyLabel(b.ConfigKey)}")
            .ToArray(),
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
        Screen.Settings    => 4,
        Screen.KeyBindings => BindingActions.Length,
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
