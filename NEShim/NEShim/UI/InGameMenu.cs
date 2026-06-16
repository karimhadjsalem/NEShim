using System.Drawing;
using System.Windows.Forms;
using NEShim.Audio;
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
internal sealed partial class InGameMenu
{
    private readonly SaveStateManager _saveStates;
    private readonly AppConfig        _config;
    private readonly LocalizationData _localization;
    private readonly Action           _onExitToDesktop;
    private readonly Action           _onResetGame;
    private readonly Action           _onReturnToMainMenu;
    private readonly Action<bool>     _onWindowModeToggle;
    private readonly Action           _onConfigSaved;
    private readonly Action<int>      _onVolumeChanged;
    private readonly Action<AudioFilterMode>        _onFilterChanged;
    private readonly Action<Rendering.VideoFilterMode> _onVideoFilterChanged;
    private readonly Action<Rendering.OverscanMode>    _onOverscanModeChanged;

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
        Action<int>             onVolumeChanged,
        Action<AudioFilterMode> onFilterChanged,
        Action<Rendering.VideoFilterMode> onVideoFilterChanged,
        Action<Rendering.OverscanMode>    onOverscanModeChanged)
    {
        _saveStates              = saveStates;
        _config                  = config;
        _localization            = localization;
        _onExitToDesktop         = onExitToDesktop;
        _onResetGame             = onResetGame;
        _onReturnToMainMenu      = onReturnToMainMenu;
        _onWindowModeToggle      = onWindowModeToggle;
        _onConfigSaved           = onConfigSaved;
        _onVolumeChanged       = onVolumeChanged;
        _onFilterChanged       = onFilterChanged;
        _onVideoFilterChanged  = onVideoFilterChanged;
        _onOverscanModeChanged = onOverscanModeChanged;

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
            [Screen.AudioFilter]            = new AudioFilterHandler(this),
            [Screen.ConfirmLoad]            = new ConfirmHandler(this,
                _localization.InGameLoadTitle,   _localization.InGameConfirmYesLoad,
                () => { _saveStates.LoadFromActiveSlot(); Close(); }),
            [Screen.ConfirmMainMenu]        = new ConfirmHandler(this,
                _localization.InGameReturnTitle, _localization.InGameConfirmYesReturn,
                () => { Close(); _onReturnToMainMenu(); }),
            [Screen.ConfirmExit]            = new ConfirmHandler(this,
                _localization.InGameExitTitle,   _localization.InGameConfirmYesExit,
                () => { Close(); _onExitToDesktop(); }),
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
        Screen.AudioFilter      => Screen.Sound,
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

}
