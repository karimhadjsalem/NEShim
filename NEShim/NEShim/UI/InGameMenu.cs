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
/// Per-screen title, items, enabled state, and activation logic live in nested
/// ScreenHandler classes — one per Screen enum value.
/// </summary>
internal sealed class InGameMenu
{
    public enum Screen
    {
        Root, SaveSlotSelect, Settings, KeyboardBindings, GamepadBindings,
        Video, Sound, ConfirmLoad, ConfirmMainMenu, ConfirmExit, ControllerDisconnected
    }

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

    private readonly (string Label, string ConfigKey)[] _bindingActions;
    private readonly (string Label, string ConfigKey)[] _gamepadBindingActions;
    private readonly IReadOnlyDictionary<Screen, ScreenHandler> _handlers;

    // ---- Public state ----

    public bool   IsOpen      { get; private set; }
    public Screen Current     { get; private set; } = Screen.Root;
    public int    SelectedItem { get; private set; }

    public string? RebindingAction        { get; private set; }
    public string? GamepadRebindingAction { get; private set; }
    public bool    IsGamepadRebinding             => GamepadRebindingAction != null;
    public bool    OverrideStartBindingProtection => _config.OverrideStartBindingProtection;
    public int     OpenMenuBindingIndex           => Array.FindIndex(_gamepadBindingActions, b => b.ConfigKey == "OpenMenu");
    public string  CurrentOpenMenuBinding         => _config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder");

    /// <summary>
    /// Returns the NES button config key for the currently active selection or rebind
    /// so the controller diagram can highlight the relevant button.
    /// </summary>
    public string? ActiveNesButton
    {
        get
        {
            string? rebinding = RebindingAction ?? GamepadRebindingAction;
            if (rebinding != null)
                return IsNesButtonKey(rebinding) ? rebinding : null;

            if (Current == Screen.KeyboardBindings)
            {
                var key = _bindingActions[SelectedItem].ConfigKey;
                return IsNesButtonKey(key) ? key : null;
            }
            if (Current == Screen.GamepadBindings)
            {
                var key = _gamepadBindingActions[SelectedItem].ConfigKey;
                return IsNesButtonKey(key) ? key : null;
            }
            return null;
        }
    }

    private static bool IsNesButtonKey(string key) =>
        key is "P1 Up" or "P1 Down" or "P1 Left" or "P1 Right"
             or "P1 A"  or "P1 B"   or "P1 Start" or "P1 Select";

    public int[]? FrozenFrame { get; private set; }

    /// <summary>Exposes the loaded localization so stateless renderers can read strings and font family.</summary>
    public LocalizationData Localization => _localization;

    public event Action? Opened;
    public event Action? Closed;

    private const int RootItemLoadGame     = 4;
    private const int RootItemReturnToMain = 6;

    // ---- Constructor ----

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

        _bindingActions        = MenuBindingHelpers.BuildBindingActions(localization);
        _gamepadBindingActions = MenuBindingHelpers.BuildGamepadBindingActions(localization, config, _bindingActions);
        _handlers              = BuildHandlers();
    }

    private IReadOnlyDictionary<Screen, ScreenHandler> BuildHandlers() =>
        new Dictionary<Screen, ScreenHandler>
        {
            [Screen.Root]                   = new RootHandler(this),
            [Screen.SaveSlotSelect]         = new SaveSlotSelectHandler(this),
            [Screen.Settings]               = new SettingsHandler(this),
            [Screen.KeyboardBindings]       = new KeyboardBindingsHandler(this),
            [Screen.GamepadBindings]        = new GamepadBindingsHandler(this),
            [Screen.Video]                  = new VideoHandler(this),
            [Screen.Sound]                  = new SoundHandler(this),
            [Screen.ConfirmLoad]            = new ConfirmLoadHandler(this),
            [Screen.ConfirmMainMenu]        = new ConfirmMainMenuHandler(this),
            [Screen.ConfirmExit]            = new ConfirmExitHandler(this),
            [Screen.ControllerDisconnected] = new ControllerDisconnectedHandler(this),
        };

    // ---- Open / Close ----

    public void Open(int[] frozenFrame, Screen startScreen = Screen.Root)
    {
        if (IsOpen) return;
        FrozenFrame     = frozenFrame;
        IsOpen          = true;
        Current         = startScreen;
        SelectedItem    = 0;
        RebindingAction = null;
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen                 = false;
        RebindingAction        = null;
        GamepadRebindingAction = null;
        Closed?.Invoke();
    }

    // ---- Keyboard input ----

    public bool HandleKey(Keys key)
    {
        if (!IsOpen) return false;
        if (Current == Screen.ControllerDisconnected) return false;

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
            return true; // block all nav keys while waiting for a gamepad button
        }

        if (Current == Screen.Sound && SelectedItem == SoundHandler.VolumeIndex)
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
        if (!IsOpen || !nav.Any) return;
        if (Current == Screen.ControllerDisconnected) return;
        if (RebindingAction != null || GamepadRebindingAction != null) return;

        if (Current == Screen.Sound && SelectedItem == SoundHandler.VolumeIndex)
        {
            if (nav.Left)  { AdjustVolume(-5); return; }
            if (nav.Right) { AdjustVolume( 5); return; }
        }

        if (nav.Up)   MoveCursor(-1);
        if (nav.Down) MoveCursor(1);

        if (nav.Confirm && IsItemEnabled(SelectedItem))
            ActivateCurrent();

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
            ActivateCurrent();
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

    private void NavigateTo(Screen screen)
    {
        Current      = screen;
        SelectedItem = 0;
        if (!IsItemEnabled(0))
            MoveCursor(1);
    }

    private void ActivateCurrent() => _handlers[Current].Activate(SelectedItem);

    private int ItemCount() => _handlers.TryGetValue(Current, out var handler) ? handler.ItemCount : 1;

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

    // ---- Handler dispatch (public — called by renderer and tests) ----

    public bool IsItemEnabled(int index) =>
        _handlers.TryGetValue(Current, out var handler) ? handler.IsItemEnabled(index) : true;

    public string[] GetCurrentItems() =>
        _handlers.TryGetValue(Current, out var handler) ? handler.GetItems() : Array.Empty<string>();

    public string GetTitle() =>
        _handlers.TryGetValue(Current, out var handler) ? handler.Title : "";

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

    public void Render(Graphics g, Rectangle bounds) => MenuRenderer.Draw(g, bounds, this);

    // =========================================================================
    // Screen handlers
    // Each handler encapsulates one screen's title, items, enabled state, and
    // activation logic. To add a new screen: add an enum value, a handler class,
    // and an entry in BuildHandlers().
    // =========================================================================

    private abstract class ScreenHandler
    {
        protected InGameMenu Menu { get; }
        protected ScreenHandler(InGameMenu menu) => Menu = menu;
        public abstract string   Title     { get; }
        public abstract int      ItemCount { get; }
        public abstract string[] GetItems();
        public abstract void     Activate(int index);
        public virtual  bool     IsItemEnabled(int index) => true;
    }

    private sealed class RootHandler : ScreenHandler
    {
        public RootHandler(InGameMenu menu) : base(menu) { }
        public override string Title     => Menu._localization.InGamePausedTitle;
        public override int    ItemCount => 8;
        public override string[] GetItems() => new[]
        {
            Menu._localization.InGameResume,
            Menu._localization.InGameResetGame,
            Menu._localization.InGameSelectSaveSlot,
            Menu._localization.InGameSaveGame,
            Menu._localization.InGameLoadGame,
            Menu._localization.InGameSettings,
            Menu._localization.InGameReturnToMain,
            Menu._localization.InGameExit,
        };
        public override bool IsItemEnabled(int index) =>
            index != RootItemLoadGame || Menu._saveStates.SlotExists(Menu._saveStates.ActiveSlot);
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0:                    Menu.Close(); break;
                case 1:                    Menu._onResetGame(); Menu.Close(); break;
                case 2:                    Menu.NavigateTo(Screen.SaveSlotSelect); break;
                case 3:                    Menu._saveStates.SaveToActiveSlot(); Menu.Close(); break;
                case RootItemLoadGame:     Menu.NavigateTo(Screen.ConfirmLoad); break;
                case 5:                    Menu.NavigateTo(Screen.Settings); break;
                case RootItemReturnToMain:
                    Menu.NavigateTo(Screen.ConfirmMainMenu);
                    Menu.SelectedItem = 1;
                    break;
                case 7:
                    Menu.NavigateTo(Screen.ConfirmExit);
                    Menu.SelectedItem = 1;
                    break;
            }
        }
    }

    private sealed class SaveSlotSelectHandler : ScreenHandler
    {
        public SaveSlotSelectHandler(InGameMenu menu) : base(menu) { }
        public override string Title =>
            string.Format(Menu._localization.InGameSelectSlotTitle, Menu._saveStates.ActiveSlot + 1);
        public override int ItemCount => SaveStateManager.SlotCount + 1;
        public override string[] GetItems()
            => Enumerable.Range(0, SaveStateManager.SlotCount)
                .Select(i => string.Format(Menu._localization.SlotLabel, i + 1)
                           + (i == Menu._saveStates.ActiveSlot ? Menu._localization.SlotActive : ""))
                .Append(Menu._localization.Back)
                .ToArray();
        public override void Activate(int index)
        {
            if (index == SaveStateManager.SlotCount)
                Menu.NavigateTo(Screen.Root);
            else
            {
                Menu._saveStates.ActiveSlot = index;
                Menu._config.ActiveSlot     = index;
                Menu.NavigateTo(Screen.Root);
            }
        }
    }

    private sealed class SettingsHandler : ScreenHandler
    {
        public SettingsHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.SettingsTitle;
        public override int      ItemCount => 5;
        public override string[] GetItems() => new[]
        {
            Menu._localization.SettingsKeyboard,
            Menu._localization.SettingsGamepad,
            Menu._localization.SettingsVideo,
            Menu._localization.SettingsSound,
            Menu._localization.Back,
        };
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0: Menu.NavigateTo(Screen.KeyboardBindings); break;
                case 1: Menu.NavigateTo(Screen.GamepadBindings);  break;
                case 2: Menu.NavigateTo(Screen.Video);            break;
                case 3: Menu.NavigateTo(Screen.Sound);            break;
                case 4: Menu.NavigateTo(Screen.Root);             break;
            }
        }
    }

    private sealed class KeyboardBindingsHandler : ScreenHandler
    {
        public KeyboardBindingsHandler(InGameMenu menu) : base(menu) { }
        public override string Title => Menu.RebindingAction != null
            ? string.Format(Menu._localization.PressKeyTitle,
                Menu._bindingActions.First(b => b.ConfigKey == Menu.RebindingAction).Label.ToUpper())
            : Menu._localization.SettingsKeyboard.ToUpper();
        public override int      ItemCount => Menu._bindingActions.Length;
        public override string[] GetItems()
            => Menu._bindingActions
                .Select(b => b.ConfigKey == ""
                    ? Menu._localization.Back
                    : $"{b.Label,-8}  {Menu.KeyboardLabel(b.ConfigKey)}")
                .ToArray();
        public override void Activate(int index)
        {
            var (_, configKey) = Menu._bindingActions[index];
            if (configKey == "")
                Menu.NavigateTo(Screen.Settings);
            else
                Menu.RebindingAction = configKey;
        }
    }

    private sealed class GamepadBindingsHandler : ScreenHandler
    {
        public GamepadBindingsHandler(InGameMenu menu) : base(menu) { }
        public override string Title => Menu.GamepadRebindingAction != null
            ? string.Format(Menu._localization.PressButtonTitle,
                Menu._gamepadBindingActions.First(b => b.ConfigKey == Menu.GamepadRebindingAction).Label.ToUpper())
            : Menu._localization.SettingsGamepad.ToUpper();
        public override int      ItemCount => Menu._gamepadBindingActions.Length;
        public override string[] GetItems()
            => Menu._gamepadBindingActions
                .Select(b => b.ConfigKey == ""
                    ? Menu._localization.Back
                    : $"{b.Label,-8}  {Menu.GetGamepadLabel(b.ConfigKey)}")
                .ToArray();
        public override bool IsItemEnabled(int index)
        {
            if (!SteamInputManager.IsUsingNativeActions()) return true;
            var configKey = Menu._gamepadBindingActions[index].ConfigKey;
            return configKey == "" || configKey == "OpenMenu";
        }
        public override void Activate(int index)
        {
            var (_, configKey) = Menu._gamepadBindingActions[index];
            if (configKey == "")
                Menu.NavigateTo(Screen.Settings);
            else
                Menu.GamepadRebindingAction = configKey;
        }
    }

    private sealed class VideoHandler : ScreenHandler
    {
        public VideoHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.VideoTitle;
        public override int      ItemCount => 4;
        public override string[] GetItems() => new[]
        {
            Menu._config.WindowMode == "Fullscreen" ? Menu._localization.VideoWindowFullscreen : Menu._localization.VideoWindowWindowed,
            Menu._config.GraphicsSmoothingEnabled   ? Menu._localization.VideoGraphicsSmooth   : Menu._localization.VideoGraphicsOriginal,
            Menu._config.ShowFps                    ? Menu._localization.VideoFpsOn            : Menu._localization.VideoFpsOff,
            Menu._localization.Back,
        };
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0:
                    Menu._onWindowModeToggle(Menu._config.WindowMode != "Fullscreen");
                    break;
                case 1:
                    bool smoothOn = !Menu._config.GraphicsSmoothingEnabled;
                    Menu._config.GraphicsSmoothingEnabled = smoothOn;
                    Menu._onGraphicsScalerToggled(smoothOn);
                    break;
                case 2:
                    Menu._config.ShowFps = !Menu._config.ShowFps;
                    Menu._onConfigSaved();
                    break;
                case 3:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }
    }

    private sealed class SoundHandler : ScreenHandler
    {
        public  const int VolumeIndex   = 0;
        private const int ScrubberIndex = 1;
        private const int BackIndex     = 2;
        public SoundHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 3;
        public override string[] GetItems() => new[]
        {
            string.Format(Menu._localization.SoundVolume, Menu._config.Volume),
            Menu._config.SoundScrubberEnabled ? Menu._localization.SoundScrubberOn : Menu._localization.SoundScrubberOff,
            Menu._localization.Back,
        };
        public override void Activate(int index)
        {
            switch (index)
            {
                case ScrubberIndex:
                    bool scrubOn = !Menu._config.SoundScrubberEnabled;
                    Menu._config.SoundScrubberEnabled = scrubOn;
                    Menu._onScrubberToggled(scrubOn);
                    break;
                case BackIndex:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }
    }

    private sealed class ConfirmLoadHandler : ScreenHandler
    {
        public ConfirmLoadHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.InGameLoadTitle;
        public override int      ItemCount => 2;
        public override string[] GetItems() =>
            new[] { Menu._localization.InGameConfirmYesLoad, Menu._localization.InGameConfirmNoStay };
        public override void Activate(int index)
        {
            if (index == 0) { Menu._saveStates.LoadFromActiveSlot(); Menu.Close(); }
            else Menu.NavigateTo(Screen.Root);
        }
    }

    private sealed class ConfirmMainMenuHandler : ScreenHandler
    {
        public ConfirmMainMenuHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.InGameReturnTitle;
        public override int      ItemCount => 2;
        public override string[] GetItems() =>
            new[] { Menu._localization.InGameConfirmYesReturn, Menu._localization.InGameConfirmNoStay };
        public override void Activate(int index)
        {
            if (index == 0) { Menu.Close(); Menu._onReturnToMainMenu(); }
            else Menu.NavigateTo(Screen.Root);
        }
    }

    private sealed class ConfirmExitHandler : ScreenHandler
    {
        public ConfirmExitHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.InGameExitTitle;
        public override int      ItemCount => 2;
        public override string[] GetItems() =>
            new[] { Menu._localization.InGameConfirmYesExit, Menu._localization.InGameConfirmNoStay };
        public override void Activate(int index)
        {
            if (index == 0) { Menu.Close(); Menu._onExitToDesktop(); }
            else Menu.NavigateTo(Screen.Root);
        }
    }

    private sealed class ControllerDisconnectedHandler : ScreenHandler
    {
        public ControllerDisconnectedHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => "";
        public override int      ItemCount => 0;
        public override string[] GetItems() => Array.Empty<string>();
        public override void     Activate(int index) { }
    }
}
