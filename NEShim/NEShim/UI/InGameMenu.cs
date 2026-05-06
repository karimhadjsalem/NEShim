using System.Drawing;
using System.Windows.Forms;
using NEShim.Config;
using NEShim.Localization;
using NEShim.Saves;
using NEShim.Steam;

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
    private readonly LocalizationData _localization;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action           _onReturnToMainMenu;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<bool>     _onScrubberToggled;
    private readonly Action<bool>     _onGraphicsScalerToggled;

    // Binding-action table — labels are localised at construction time.
    private readonly (string Label, string ConfigKey)[] _bindingActions;

    public bool IsOpen      { get; private set; }
    public Screen Current   { get; private set; } = Screen.Root;
    public int SelectedItem { get; private set; } = 0;

    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding        => GamepadRebindingAction != null;
    public int[]?  FrozenFrame            { get; private set; }

    /// <summary>Exposes the loaded localization so stateless renderers can read strings and font family.</summary>
    public LocalizationData Localization => _localization;

    public event Action? Opened;
    public event Action? Closed;

    private const int RootItemCount   = 8;
    private const int ConfirmItemCount = 2;

    private const int RootItemLoadGame     = 4;
    private const int RootItemReturnToMain = 6;

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

    public InGameMenu(
        SaveStateManager saveStates,
        AppConfig        config,
        LocalizationData localization,
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
        _localization            = localization;
        _onExitToDesktop         = onExitToDesktop;
        _onResetGame             = onResetGame;
        _onReturnToMainMenu      = onReturnToMainMenu;
        _onWindowModeToggle      = onWindowModeToggle;
        _onConfigSaved           = onConfigSaved;
        _onVolumeChanged         = onVolumeChanged;
        _onScrubberToggled       = onScrubberToggled;
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
            return _localization.InGameRebindStartReserved;
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
        Screen.Root               => RootItemCount,
        Screen.SaveSlotSelect     => SaveStateManager.SlotCount + 1,
        Screen.Settings           => SettingsItemCount,
        Screen.KeyboardBindings   => _bindingActions.Length,
        Screen.GamepadBindings    => _bindingActions.Length,
        Screen.Video              => VideoItemCount,
        Screen.Sound              => SoundItemCount,
        Screen.ConfirmLoad        => ConfirmItemCount,
        Screen.ConfirmMainMenu    => ConfirmItemCount,
        Screen.ConfirmExit        => ConfirmItemCount,
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
        if (!IsItemEnabled(0))
            MoveCursor(1);
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
                var (_, configKey) = _bindingActions[SelectedItem];
                if (configKey == "")
                    NavigateTo(Screen.Settings);
                else
                    RebindingAction = configKey;
                break;
            }

            case Screen.GamepadBindings:
            {
                var (_, configKey) = _bindingActions[SelectedItem];
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

        // When a native Steam controller is active, in-game rebinding is not possible —
        // the user remaps via Steam's controller configurator. Show labels read-only.
        if (Current == Screen.GamepadBindings
            && SteamInputManager.IsUsingNativeActions()
            && _bindingActions[index].ConfigKey != "")
            return false;

        return true;
    }

    // ---- Rendering info ----

    public string[] GetCurrentItems() => Current switch
    {
        Screen.Root => new[]
        {
            _localization.InGameResume,
            _localization.InGameResetGame,
            _localization.InGameSelectSaveSlot,
            _localization.InGameSaveGame,
            _localization.InGameLoadGame,
            _localization.InGameSettings,
            _localization.InGameReturnToMain,
            _localization.InGameExit,
        },

        Screen.SaveSlotSelect => Enumerable.Range(0, SaveStateManager.SlotCount)
            .Select(i => string.Format(_localization.SlotLabel, i + 1)
                       + (i == _saveStates.ActiveSlot ? _localization.SlotActive : ""))
            .Append(_localization.Back)
            .ToArray(),

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
            _config.SoundScrubberEnabled ? _localization.SoundScrubberOn : _localization.SoundScrubberOff,
            _localization.Back,
        },

        Screen.ConfirmLoad     => new[] { _localization.InGameConfirmYesLoad,   _localization.InGameConfirmNoStay },
        Screen.ConfirmMainMenu => new[] { _localization.InGameConfirmYesReturn, _localization.InGameConfirmNoStay },
        Screen.ConfirmExit     => new[] { _localization.InGameConfirmYesExit,   _localization.InGameConfirmNoStay },

        _ => Array.Empty<string>()
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

    public string GetTitle() => Current switch
    {
        Screen.Root            => _localization.InGamePausedTitle,
        Screen.SaveSlotSelect  => string.Format(_localization.InGameSelectSlotTitle, _saveStates.ActiveSlot + 1),
        Screen.Settings        => _localization.SettingsTitle,
        Screen.KeyboardBindings => RebindingAction != null
            ? string.Format(_localization.PressKeyTitle,
                _bindingActions.First(b => b.ConfigKey == RebindingAction).Label.ToUpper())
            : _localization.SettingsKeyboard.ToUpper(),
        Screen.GamepadBindings => GamepadRebindingAction != null
            ? string.Format(_localization.PressButtonTitle,
                _bindingActions.First(b => b.ConfigKey == GamepadRebindingAction).Label.ToUpper())
            : _localization.SettingsGamepad.ToUpper(),
        Screen.Video           => _localization.VideoTitle,
        Screen.Sound           => _localization.SoundTitle,
        Screen.ConfirmLoad     => _localization.InGameLoadTitle,
        Screen.ConfirmMainMenu => _localization.InGameReturnTitle,
        Screen.ConfirmExit     => _localization.InGameExitTitle,
        _ => ""
    };

    public void Render(Graphics g, Rectangle bounds) => MenuRenderer.Draw(g, bounds, this);
}
