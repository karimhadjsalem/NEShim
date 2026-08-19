using System.Linq;
using NEShim.Steam;

namespace NEShim.UI;

internal sealed class GamepadBindingsHandler : SharedScreenHandler
{
    private readonly int _player;
    private readonly (string Label, string ConfigKey)[] _actions;

    /// <param name="player">1-based player this screen edits gamepad bindings for. Defaults to
    /// player 1, matching this handler's pre-multiplayer behavior.</param>
    public GamepadBindingsHandler(IMenuHost menu, int player = 1) : base(menu)
    {
        _player  = player;
        _actions = MenuBindingHelpers.BuildGamepadBindingActions(menu.Localization, menu.Config, player);
    }

    public override bool   ShowsControllerDiagram => true;
    public override string Title => Menu.GamepadRebindingAction != null
        ? string.Format(Menu.Localization.PressButtonTitle,
            _actions.First(b => b.ConfigKey == Menu.GamepadRebindingAction).Label.ToUpper())
        : Menu.Localization.SettingsGamepad.ToUpper();
    public override int      ItemCount => _actions.Length;
    public override string[] GetItems()
        => _actions
            .Select(b => b.ConfigKey == ""
                ? Menu.Localization.Back
                : $"{b.Label}\t{Menu.GetGamepadLabel(b.ConfigKey)}")
            .ToArray();
    public override bool IsItemEnabled(int index)
    {
        if (!SteamInputManager.IsUsingNativeActions(_player - 1)) return true;
        var configKey = _actions[index].ConfigKey;
        return configKey == "" || configKey == "OpenMenu";
    }
    public override IntPtr GetItemValueIcon(int index)
    {
        var configKey = _actions[index].ConfigKey;
        return configKey == "" ? IntPtr.Zero : Menu.GetGamepadGlyph(configKey);
    }
    public override void Activate(int index)
    {
        var (_, configKey) = _actions[index];
        if (configKey == "")
        {
            // Player 1 is also listed on the Player Controls submenu once it exists
            // (PlayerCount > 1 — see PlayerSelectHandler), so Back follows the other players
            // there for consistency; with PlayerCount == 1 that submenu doesn't exist and this
            // screen is only ever reached from Settings, matching pre-multiplayer behavior.
            Menu.NavigateTo(_player == 1 && Menu.Config.PlayerCount <= 1 ? Screen.Settings : Screen.PlayerSelect);
        }
        else
        {
            Menu.GamepadRebindingAction = configKey;
        }
    }
    public override string? GetActiveNesButton(int selectedItem)
    {
        var key = _actions[selectedItem].ConfigKey;
        return MenuBindingHelpers.IsNesButtonKey(key) ? key : null;
    }
}
