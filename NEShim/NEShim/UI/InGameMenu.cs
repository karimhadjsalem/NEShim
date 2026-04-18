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
    public enum Screen { Root, SaveSlotSelect, Settings, KeyboardBindings, GamepadBindings, Video, Sound, ConfirmLoad, ConfirmMainMenu, ConfirmExit }

    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action           _onReturnToMainMenu;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<bool>     _onScrubberToggled;
    private readonly Action<bool>     _onGraphicsScalerToggled;

    public bool IsOpen      { get; private set; }
    public Screen Current   { get; private set; } = Screen.Root;
    public int SelectedItem { get; private set; } = 0;

    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding        => GamepadRebindingAction != null;
    public bool    IsGamepadOverriddenBySteam => Steam.SteamInputManager.HasConnectedController;
    public int[]?  FrozenFrame            { get; private set; }

    public event Action? Opened;
    public event Action? Closed;

    // ---- Root menu ----
    private static readonly string[] RootItems =
        { "Resume", "Reset Game", "Select Save Slot", "Save Game", "Load Game", "Settings", "Return to Main Menu", "Exit" };

    private const int RootItemLoadGame     = 4;
    private const int RootItemReturnToMain = 6;

    // ---- Confirm screens ----
    private static readonly string[] ConfirmLoadItems     = { "Yes, load game",           "No, stay in game" };
    private static readonly string[] ConfirmMainMenuItems = { "Yes, return to main menu", "No, stay in game" };
    private static readonly string[] ConfirmExitItems     = { "Yes, exit to desktop",     "No, stay in game" };

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
        Action<bool>     onScrubberToggled,
        Action<bool>     onGraphicsScalerToggled)
    {
        _saveStates              = saveStates;
        _config                  = config;
        _onExitToDesktop         = onExitToDesktop;
        _onResetGame             = onResetGame;
        _onReturnToMainMenu      = onReturnToMainMenu;
        _onWindowModeToggle      = onWindowModeToggle;
        _onConfigSaved           = onConfigSaved;
        _onVolumeChanged         = onVolumeChanged;
        _onScrubberToggled       = onScrubberToggled;
        _onGraphicsScalerToggled = onGraphicsScalerToggled;
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
        IsOpen                = false;
        RebindingAction       = null;
        GamepadRebindingAction = null;
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

        if (GamepadRebindingAction != null)
        {
            if (key == Keys.Escape) GamepadRebindingAction = null;
            return true; // block all nav keys while waiting for a gamepad button
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
                    NavigateTo(ParentScreen(Current));
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
            return "Start is reserved for the menu";
        }
        SetGamepadBinding(GamepadRebindingAction, buttonName);
        _onConfigSaved();
        GamepadRebindingAction = null;
        return null;
    }

    public void HandleGamepadNav(Input.MenuNavInput nav)
    {
        if (!IsOpen || !nav.Any) return;
        if (RebindingAction != null || GamepadRebindingAction != null) return;

        if (Current == Screen.Sound && SelectedItem == SoundItemVolume)
        {
            if (nav.Left)  { AdjustVolume(-5); return; }
            if (nav.Right) { AdjustVolume( 5); return; }
        }

        if (nav.Up)   MoveCursor(-1);
        if (nav.Down) MoveCursor(1);

        if (nav.Confirm && IsItemEnabled(SelectedItem))
            Activate();

        if (nav.Back)
        {
            if (Current == Screen.Root)
                Close();
            else
                NavigateTo(ParentScreen(Current));
        }
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
        Screen.Root               => RootItems.Length,
        Screen.SaveSlotSelect     => SaveStateManager.SlotCount + 1,
        Screen.Settings           => SettingsItemCount,
        Screen.KeyboardBindings   => BindingActions.Length,
        Screen.GamepadBindings    => BindingActions.Length,
        Screen.Video              => VideoItemCount,
        Screen.Sound              => SoundItemCount,
        Screen.ConfirmLoad        => ConfirmLoadItems.Length,
        Screen.ConfirmMainMenu    => ConfirmMainMenuItems.Length,
        Screen.ConfirmExit        => ConfirmExitItems.Length,
        _ => 1
    };

    private static Screen ParentScreen(Screen screen) => screen switch
    {
        Screen.SaveSlotSelect   => Screen.Root,
        Screen.Settings         => Screen.Root,
        Screen.ConfirmLoad      => Screen.Root,
        Screen.ConfirmMainMenu  => Screen.Root,
        Screen.ConfirmExit      => Screen.Root,
        Screen.KeyboardBindings => Screen.Settings,
        Screen.GamepadBindings  => Screen.Settings,
        Screen.Video            => Screen.Settings,
        Screen.Sound            => Screen.Settings,
        _                       => Screen.Root,
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
                    case 4: NavigateTo(Screen.ConfirmLoad); break;
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
                if (SelectedItem == SaveStateManager.SlotCount)
                    NavigateTo(Screen.Root);
                else
                {
                    _saveStates.ActiveSlot = SelectedItem;
                    _config.ActiveSlot     = SelectedItem;
                    NavigateTo(Screen.Root);
                }
                break;

            case Screen.Settings:
                switch (SelectedItem)
                {
                    case SettingsItemKeyboardBindings: NavigateTo(Screen.KeyboardBindings); break;
                    case SettingsItemGamepadBindings:  NavigateTo(Screen.GamepadBindings);  break;
                    case SettingsItemVideo:            NavigateTo(Screen.Video);            break;
                    case SettingsItemSound:            NavigateTo(Screen.Sound);            break;
                    case SettingsItemBack:             NavigateTo(Screen.Root);             break;
                }
                break;

            case Screen.Video:
                switch (SelectedItem)
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
                var (_, configKey) = BindingActions[SelectedItem];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    RebindingAction = configKey;
                break;
            }

            case Screen.GamepadBindings:
            {
                var (_, configKey) = BindingActions[SelectedItem];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    GamepadRebindingAction = configKey;
                break;
            }

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

            case Screen.ConfirmLoad:
                if (SelectedItem == 0) { _saveStates.LoadFromActiveSlot(); Close(); }
                else NavigateTo(Screen.Root);
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

    /// <summary>
    /// Assigns <paramref name="buttonName"/> to <paramref name="action"/> and clears
    /// the same button from any other action to prevent duplicate bindings.
    /// </summary>
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
            .Append("← Back")
            .ToArray(),

        Screen.Settings => new[]
        {
            "Keyboard Controls",
            "Gamepad Controls",
            "Video",
            "Sound",
            "← Back",
        },

        Screen.Video => new[]
        {
            $"Window Mode: {(_config.WindowMode == "Fullscreen" ? "Fullscreen" : "Windowed")}",
            $"Graphics: {(_config.GraphicsSmoothingEnabled ? "Smooth" : "Original")}",
            $"FPS Overlay: {(_config.ShowFps ? "On" : "Off")}",
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
            "← Back",
        },

        Screen.ConfirmLoad     => ConfirmLoadItems,
        Screen.ConfirmMainMenu => ConfirmMainMenuItems,
        Screen.ConfirmExit     => ConfirmExitItems,

        _ => Array.Empty<string>()
    };

    private string KeyboardLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? "(none)" : "(none)";

    private string GamepadLabel(string configKey)
        => _config.InputMappings.TryGetValue(configKey, out var b) ? b.GamepadButton ?? "(none)" : "(none)";

    public string GetTitle() => Current switch
    {
        Screen.Root            => "PAUSED",
        Screen.SaveSlotSelect  => $"SELECT SLOT  (active: {_saveStates.ActiveSlot + 1})",
        Screen.Settings        => "SETTINGS",
        Screen.KeyboardBindings => RebindingAction != null
            ? $"PRESS KEY FOR  {BindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper()}"
            : "KEYBOARD CONTROLS",

        Screen.GamepadBindings => GamepadRebindingAction != null
            ? $"PRESS BUTTON FOR  {BindingActions.First(b => b.ConfigKey == GamepadRebindingAction).Label.ToUpper()}"
            : "GAMEPAD CONTROLS",
        Screen.Video           => "VIDEO",
        Screen.Sound           => "SOUND",
        Screen.ConfirmLoad     => "LOAD GAME?",
        Screen.ConfirmMainMenu => "RETURN TO MAIN MENU?",
        Screen.ConfirmExit     => "EXIT TO DESKTOP?",
        _ => ""
    };

    public void Render(Graphics g, Rectangle bounds) => MenuRenderer.Draw(g, bounds, this);
}
