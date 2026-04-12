using System.Drawing;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Saves;

namespace NEShim.UI;

/// <summary>
/// State machine for the in-game pause menu.
/// Rendering is delegated to MenuRenderer; this class owns navigation and actions.
/// </summary>
internal sealed class InGameMenu
{
    public enum Screen { Root, SaveSlotSelect, Settings, KeyBindings }

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action<bool>     _onWindowModeToggle; // true = fullscreen
    private readonly Action           _onConfigSaved;

    public bool IsOpen      { get; private set; }
    public Screen Current   { get; private set; } = Screen.Root;
    public int SelectedItem { get; private set; } = 0;

    // When non-null we are waiting for the user to press a key to rebind this action
    public string? RebindingAction { get; private set; }

    // Frozen frame shown as background when menu is open
    public int[]? FrozenFrame { get; private set; }

    // Callbacks wired by EmulationThread
    public event Action? Opened;
    public event Action? Closed;

    // ---- Root menu ----
    private static readonly string[] RootItems =
        { "Resume", "Reset Game", "Select Save Slot", "Save Game", "Load Game", "Settings", "Exit" };

    private const int RootItemLoadGame = 4;

    // ---- Settings menu ----
    private static readonly string[] SettingsBaseItems =
        { "Key Bindings", "Fullscreen", "Windowed" };

    // ---- Key-binding action order (display name, config key) ----
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
        ("Back",   ""),    // sentinel — "Back" item at end
    };

    public InGameMenu(
        SaveStateManager saveStates,
        AppConfig        config,
        Action           onExitToDesktop,
        Action           onResetGame,
        Action<bool>     onWindowModeToggle,
        Action           onConfigSaved)
    {
        _saveStates         = saveStates;
        _config             = config;
        _onExitToDesktop    = onExitToDesktop;
        _onResetGame        = onResetGame;
        _onWindowModeToggle = onWindowModeToggle;
        _onConfigSaved      = onConfigSaved;
    }

    // ---- Open / Close ----

    public void Open(int[] frozenFrame)
    {
        if (IsOpen) return;
        FrozenFrame  = frozenFrame;
        IsOpen       = true;
        Current      = Screen.Root;
        SelectedItem = 0;
        RebindingAction = null;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        RebindingAction = null;
        Closed?.Invoke();
    }

    // ---- Input (called on UI thread from KeyDown) ----

    /// <summary>
    /// Handle a key press while the menu is open.
    /// Returns true if the menu consumed the key (suppress game input).
    /// </summary>
    public bool HandleKey(Keys key)
    {
        if (!IsOpen) return false;

        // While waiting for a rebind key, any key (except Escape) sets the binding
        if (RebindingAction != null)
        {
            if (key == Keys.Escape)
            {
                RebindingAction = null; // cancel rebind
            }
            else
            {
                // Record the new key for this action
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
                if (Current == Screen.Root)
                    Close();
                else
                    NavigateTo(Screen.Root);
                return true;

            case Keys.Up:
                MoveCursor(-1);
                return true;

            case Keys.Down:
                MoveCursor(1);
                return true;

            case Keys.Return:
            case Keys.Z:
            case Keys.Space:
                if (IsItemEnabled(SelectedItem))
                    Activate();
                return true;
        }
        return false;
    }

    private void MoveCursor(int direction)
    {
        int count = ItemCount();
        int next  = SelectedItem;
        for (int attempt = 0; attempt < count; attempt++)
        {
            next = Math.Clamp(next + direction, 0, count - 1);
            if (IsItemEnabled(next))
            {
                SelectedItem = next;
                return;
            }
            // If we've hit a wall (top or bottom), stop rather than wrapping
            if (next == 0 || next == count - 1) break;
        }
    }

    private int ItemCount() => Current switch
    {
        Screen.Root          => RootItems.Length,
        Screen.SaveSlotSelect => SaveStateManager.SlotCount,
        Screen.Settings      => SettingsBaseItems.Length + 1, // +1 for FPS toggle
        Screen.KeyBindings   => BindingActions.Length,        // includes Back sentinel
        _ => 1
    };

    private void NavigateTo(Screen screen)
    {
        Current      = screen;
        SelectedItem = 0;
    }

    private void Activate()
    {
        switch (Current)
        {
            case Screen.Root:
                switch (SelectedItem)
                {
                    case 0: // Resume
                        Close();
                        break;
                    case 1: // Reset Game
                        _onResetGame();
                        Close();
                        break;
                    case 2: // Select Save Slot
                        NavigateTo(Screen.SaveSlotSelect);
                        break;
                    case 3: // Save Game
                        _saveStates.SaveToActiveSlot();
                        Close();
                        break;
                    case 4: // Load Game
                        _saveStates.LoadFromActiveSlot();
                        Close();
                        break;
                    case 5: // Settings
                        NavigateTo(Screen.Settings);
                        break;
                    case 6: // Exit
                        _onExitToDesktop();
                        break;
                }
                break;

            case Screen.SaveSlotSelect:
                _saveStates.ActiveSlot = SelectedItem;
                _config.ActiveSlot     = SelectedItem;
                NavigateTo(Screen.Root);
                break;

            case Screen.Settings:
                switch (SelectedItem)
                {
                    case 0: // Key Bindings
                        NavigateTo(Screen.KeyBindings);
                        break;
                    case 1: // Fullscreen
                        _onWindowModeToggle(true);
                        NavigateTo(Screen.Root);
                        break;
                    case 2: // Windowed
                        _onWindowModeToggle(false);
                        NavigateTo(Screen.Root);
                        break;
                    case 3: // FPS Overlay toggle
                        _config.Developer.ShowFps = !_config.Developer.ShowFps;
                        _onConfigSaved();
                        break;
                }
                break;

            case Screen.KeyBindings:
                var (_, configKey) = BindingActions[SelectedItem];
                if (configKey == "") // Back item
                {
                    NavigateTo(Screen.Settings);
                }
                else
                {
                    // Enter rebind mode for this action
                    RebindingAction = configKey;
                }
                break;
        }
    }

    // ---- Enabled state ----

    /// <summary>
    /// Returns false for items that are currently unavailable.
    /// Load Game is disabled when the active slot has no saved state.
    /// </summary>
    public bool IsItemEnabled(int index)
    {
        if (Current == Screen.Root && index == RootItemLoadGame)
            return _saveStates.SlotExists(_saveStates.ActiveSlot);
        return true;
    }

    // ---- Rendering info accessors ----

    public string[] GetCurrentItems()
    {
        return Current switch
        {
            Screen.Root => RootItems,

            Screen.SaveSlotSelect => Enumerable.Range(0, SaveStateManager.SlotCount)
                .Select(i => $"Slot {i + 1}{(i == _saveStates.ActiveSlot ? "  ◀ active" : "")}")
                .ToArray(),

            Screen.Settings => new[]
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
    }

    private string KeyLabel(string configKey)
    {
        if (_config.InputMappings.TryGetValue(configKey, out var binding))
            return binding.Key ?? "(none)";
        return "(none)";
    }

    public string GetTitle() => Current switch
    {
        Screen.Root           => "PAUSED",
        Screen.SaveSlotSelect => $"SELECT SLOT  (active: {_saveStates.ActiveSlot + 1})",
        Screen.Settings       => "SETTINGS",
        Screen.KeyBindings    => RebindingAction != null
            ? $"PRESS KEY FOR  {BindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper()}"
            : "KEY BINDINGS",
        _ => ""
    };

    /// <summary>Renders the menu overlay onto the given Graphics context.</summary>
    public void Render(Graphics g, Rectangle bounds)
    {
        MenuRenderer.Draw(g, bounds, this);
    }
}
