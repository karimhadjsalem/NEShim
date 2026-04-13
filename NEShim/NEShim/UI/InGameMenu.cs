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
    public enum Screen { Root, SaveSlotSelect, Settings, KeyBindings, Sound, ConfirmMainMenu, ConfirmExit }

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action           _onReturnToMainMenu;
    private readonly Action<bool>     _onWindowModeToggle; // true = fullscreen
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;    // 0–100
    private readonly Action<bool>     _onScrubberToggled;  // true = scrubber on

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
        { "Resume", "Reset Game", "Select Save Slot", "Save Game", "Load Game", "Settings", "Return to Main Menu", "Exit" };

    private const int RootItemLoadGame      = 4;
    private const int RootItemReturnToMain  = 6;

    // ---- Confirm screens ----
    private static readonly string[] ConfirmMainMenuItems = { "Yes, return to main menu", "No, stay in game" };
    private static readonly string[] ConfirmExitItems     = { "Yes, exit to desktop",     "No, stay in game" };

    // ---- Settings menu ----
    // 0: Key Bindings, 1: Window Mode (toggle), 2: FPS Overlay, 3: Sound
    private const int SettingsItemKeyBindings = 0;
    private const int SettingsItemWindowMode  = 1;
    private const int SettingsItemFps         = 2;
    private const int SettingsItemSound       = 3;
    private const int SettingsItemCount       = 4;

    // ---- Sound screen items ----
    private const int SoundItemVolume   = 0;
    private const int SoundItemScrubber = 1;
    private const int SoundItemBack     = 2;
    private const int SoundItemCount    = 3;

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
        Action           onReturnToMainMenu,
        Action<bool>     onWindowModeToggle,
        Action           onConfigSaved,
        Action<int>      onVolumeChanged,
        Action<bool>     onScrubberToggled)
    {
        _saveStates          = saveStates;
        _config              = config;
        _onExitToDesktop     = onExitToDesktop;
        _onResetGame         = onResetGame;
        _onReturnToMainMenu  = onReturnToMainMenu;
        _onWindowModeToggle  = onWindowModeToggle;
        _onConfigSaved       = onConfigSaved;
        _onVolumeChanged     = onVolumeChanged;
        _onScrubberToggled   = onScrubberToggled;
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

        // Left/Right adjust volume when on Sound screen at the Volume item
        if (Current == Screen.Sound && SelectedItem == SoundItemVolume)
        {
            if (key == Keys.Left)  { AdjustVolume(-5); return true; }
            if (key == Keys.Right) { AdjustVolume( 5); return true; }
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

    private void AdjustVolume(int delta)
    {
        int next = Math.Clamp(_config.Volume + delta, 0, 100);
        if (next == _config.Volume) return;
        _config.Volume = next;
        _onVolumeChanged(next);
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
        Screen.Root             => RootItems.Length,
        Screen.SaveSlotSelect   => SaveStateManager.SlotCount,
        Screen.Settings         => SettingsItemCount,
        Screen.KeyBindings      => BindingActions.Length,
        Screen.Sound            => SoundItemCount,
        Screen.ConfirmMainMenu  => ConfirmMainMenuItems.Length,
        Screen.ConfirmExit      => ConfirmExitItems.Length,
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
                    case RootItemReturnToMain: // Return to Main Menu
                        NavigateTo(Screen.ConfirmMainMenu);
                        SelectedItem = 1; // default to "No" — prevents accidental confirmation
                        break;
                    case 7: // Exit
                        NavigateTo(Screen.ConfirmExit);
                        SelectedItem = 1; // default to "No" — prevents accidental exit
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
                    case SettingsItemKeyBindings:
                        NavigateTo(Screen.KeyBindings);
                        break;
                    case SettingsItemWindowMode:
                        // Toggle between fullscreen and windowed
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

            case Screen.Sound:
                switch (SelectedItem)
                {
                    case SoundItemScrubber:
                        bool scrubOn = !_config.SoundScrubberEnabled;
                        _config.SoundScrubberEnabled = scrubOn;
                        _onScrubberToggled(scrubOn);
                        break;
                    case SoundItemBack:
                        NavigateTo(Screen.Settings);
                        break;
                    // SoundItemVolume is handled by Left/Right in HandleKey, not Return
                }
                break;

            case Screen.ConfirmMainMenu:
                if (SelectedItem == 0) // Yes
                {
                    Close();
                    _onReturnToMainMenu();
                }
                else // No
                {
                    NavigateTo(Screen.Root);
                }
                break;

            case Screen.ConfirmExit:
                if (SelectedItem == 0) // Yes
                {
                    Close();
                    _onExitToDesktop();
                }
                else // No
                {
                    NavigateTo(Screen.Root);
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
                "← Back",
            },

            Screen.ConfirmMainMenu => ConfirmMainMenuItems,
            Screen.ConfirmExit     => ConfirmExitItems,

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
        Screen.Root             => "PAUSED",
        Screen.SaveSlotSelect   => $"SELECT SLOT  (active: {_saveStates.ActiveSlot + 1})",
        Screen.Settings         => "SETTINGS",
        Screen.KeyBindings      => RebindingAction != null
            ? $"PRESS KEY FOR  {BindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper()}"
            : "KEY BINDINGS",
        Screen.Sound            => "SOUND",
        Screen.ConfirmMainMenu  => "RETURN TO MAIN MENU?",
        Screen.ConfirmExit      => "EXIT TO DESKTOP?",
        _ => ""
    };

    /// <summary>Renders the menu overlay onto the given Graphics context.</summary>
    public void Render(Graphics g, Rectangle bounds)
    {
        MenuRenderer.Draw(g, bounds, this);
    }
}
