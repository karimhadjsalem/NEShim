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
    public enum Screen { Root, SaveSlotSelect, Settings, KeyBindings, Video, Sound, ConfirmMainMenu, ConfirmExit }

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action           _onReturnToMainMenu;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<bool>     _onScrubberToggled;

    public bool IsOpen      { get; private set; }
    public Screen Current   { get; private set; } = Screen.Root;
    public int SelectedItem { get; private set; } = 0;

    public string? RebindingAction { get; private set; }
    public int[]?  FrozenFrame     { get; private set; }

    public event Action? Opened;
    public event Action? Closed;

    // ---- Root menu ----
    private static readonly string[] RootItems =
        { "Resume", "Reset Game", "Select Save Slot", "Save Game", "Load Game", "Settings", "Return to Main Menu", "Exit" };

    private const int RootItemLoadGame     = 4;
    private const int RootItemReturnToMain = 6;

    // ---- Confirm screens ----
    private static readonly string[] ConfirmMainMenuItems = { "Yes, return to main menu", "No, stay in game" };
    private static readonly string[] ConfirmExitItems     = { "Yes, exit to desktop",     "No, stay in game" };

    // ---- Settings: 3 items ----
    private const int SettingsItemKeyBindings = 0;
    private const int SettingsItemVideo       = 1;
    private const int SettingsItemSound       = 2;
    private const int SettingsItemCount       = 3;

    // ---- Video screen: 3 items ----
    private const int VideoItemWindowMode = 0;
    private const int VideoItemFps        = 1;
    private const int VideoItemBack       = 2;
    private const int VideoItemCount      = 3;

    // ---- Sound screen: 3 items ----
    private const int SoundItemVolume   = 0;
    private const int SoundItemScrubber = 1;
    private const int SoundItemBack     = 2;
    private const int SoundItemCount    = 3;

    // ---- Key-binding action order ----
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
        ("Back",   ""),
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
        _saveStates         = saveStates;
        _config             = config;
        _onExitToDesktop    = onExitToDesktop;
        _onResetGame        = onResetGame;
        _onReturnToMainMenu = onReturnToMainMenu;
        _onWindowModeToggle = onWindowModeToggle;
        _onConfigSaved      = onConfigSaved;
        _onVolumeChanged    = onVolumeChanged;
        _onScrubberToggled  = onScrubberToggled;
    }

    // ---- Open / Close ----

    public void Open(int[] frozenFrame)
    {
        if (IsOpen) return;
        FrozenFrame     = frozenFrame;
        IsOpen          = true;
        Current         = Screen.Root;
        SelectedItem    = 0;
        RebindingAction = null;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen          = false;
        RebindingAction = null;
        Closed?.Invoke();
    }

    // ---- Keyboard input ----

    public bool HandleKey(Keys key)
    {
        if (!IsOpen) return false;

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

        // Left/Right: volume on Sound screen
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

    // ---- Mouse input ----

    /// <summary>Highlights the item under the cursor. Returns true if repaint needed.</summary>
    public bool HandleMouseMove(Point p, Rectangle bounds)
    {
        if (!IsOpen || RebindingAction != null) return false;
        int hit = MenuRenderer.HitTestItem(p, bounds, this);
        if (hit >= 0 && IsItemEnabled(hit) && hit != SelectedItem)
        {
            SelectedItem = hit;
            return true;
        }
        return false;
    }

    /// <summary>Activates the item under the cursor. Returns true if repaint needed.</summary>
    public bool HandleMouseClick(Point p, Rectangle bounds)
    {
        if (!IsOpen) return false;
        if (RebindingAction != null) return true;
        int hit = MenuRenderer.HitTestItem(p, bounds, this);
        if (hit >= 0 && IsItemEnabled(hit))
        {
            SelectedItem = hit;
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

    private int ItemCount() => Current switch
    {
        Screen.Root            => RootItems.Length,
        Screen.SaveSlotSelect  => SaveStateManager.SlotCount,
        Screen.Settings        => SettingsItemCount,
        Screen.KeyBindings     => BindingActions.Length,
        Screen.Video           => VideoItemCount,
        Screen.Sound           => SoundItemCount,
        Screen.ConfirmMainMenu => ConfirmMainMenuItems.Length,
        Screen.ConfirmExit     => ConfirmExitItems.Length,
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
                    case 0: Close(); break;
                    case 1: _onResetGame(); Close(); break;
                    case 2: NavigateTo(Screen.SaveSlotSelect); break;
                    case 3: _saveStates.SaveToActiveSlot(); Close(); break;
                    case 4: _saveStates.LoadFromActiveSlot(); Close(); break;
                    case 5: NavigateTo(Screen.Settings); break;
                    case RootItemReturnToMain:
                        NavigateTo(Screen.ConfirmMainMenu);
                        SelectedItem = 1;
                        break;
                    case 7:
                        NavigateTo(Screen.ConfirmExit);
                        SelectedItem = 1;
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
                    case SettingsItemKeyBindings: NavigateTo(Screen.KeyBindings); break;
                    case SettingsItemVideo:        NavigateTo(Screen.Video);       break;
                    case SettingsItemSound:        NavigateTo(Screen.Sound);       break;
                }
                break;

            case Screen.Video:
                switch (SelectedItem)
                {
                    case VideoItemWindowMode:
                        _onWindowModeToggle(_config.WindowMode != "Fullscreen");
                        break;
                    case VideoItemFps:
                        _config.Developer.ShowFps = !_config.Developer.ShowFps;
                        _onConfigSaved();
                        break;
                    case VideoItemBack:
                        NavigateTo(Screen.Settings);
                        break;
                }
                break;

            case Screen.KeyBindings:
                var (_, configKey) = BindingActions[SelectedItem];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    RebindingAction = configKey;
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
                }
                break;

            case Screen.ConfirmMainMenu:
                if (SelectedItem == 0) { Close(); _onReturnToMainMenu(); }
                else NavigateTo(Screen.Root);
                break;

            case Screen.ConfirmExit:
                if (SelectedItem == 0) { Close(); _onExitToDesktop(); }
                else NavigateTo(Screen.Root);
                break;
        }
    }

    /// <summary>
    /// Assigns <paramref name="keyName"/> to <paramref name="action"/> and clears
    /// the same key from any other action to prevent duplicate bindings.
    /// </summary>
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

    // ---- Enabled state ----

    public bool IsItemEnabled(int index)
    {
        if (Current == Screen.Root && index == RootItemLoadGame)
            return _saveStates.SlotExists(_saveStates.ActiveSlot);
        return true;
    }

    // ---- Rendering info ----

    public string[] GetCurrentItems() => Current switch
    {
        Screen.Root => RootItems,

        Screen.SaveSlotSelect => Enumerable.Range(0, SaveStateManager.SlotCount)
            .Select(i => $"Slot {i + 1}{(i == _saveStates.ActiveSlot ? "  ◀ active" : "")}")
            .ToArray(),

        Screen.Settings => new[]
        {
            "Key Bindings",
            "Video",
            "Sound",
        },

        Screen.Video => new[]
        {
            $"Window Mode: {(_config.WindowMode == "Fullscreen" ? "Fullscreen" : "Windowed")}",
            $"FPS Overlay: {(_config.Developer.ShowFps ? "On" : "Off")}",
            "← Back",
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

    private string KeyLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? "(none)" : "(none)";

    public string GetTitle() => Current switch
    {
        Screen.Root            => "PAUSED",
        Screen.SaveSlotSelect  => $"SELECT SLOT  (active: {_saveStates.ActiveSlot + 1})",
        Screen.Settings        => "SETTINGS",
        Screen.KeyBindings     => RebindingAction != null
            ? $"PRESS KEY FOR  {BindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper()}"
            : "KEY BINDINGS",
        Screen.Video           => "VIDEO",
        Screen.Sound           => "SOUND",
        Screen.ConfirmMainMenu => "RETURN TO MAIN MENU?",
        Screen.ConfirmExit     => "EXIT TO DESKTOP?",
        _ => ""
    };

    public void Render(Graphics g, Rectangle bounds) => MenuRenderer.Draw(g, bounds, this);
}
