using System.Linq;

namespace NEShim.UI;

internal sealed class KeyboardBindingsHandler : SharedScreenHandler
{
    private readonly int _player;
    private readonly (string Label, string ConfigKey)[] _actions;

    /// <param name="player">1-based player this screen edits keyboard bindings for. Defaults to
    /// player 1, matching this handler's pre-multiplayer behavior.</param>
    public KeyboardBindingsHandler(IMenuHost menu, int player = 1) : base(menu)
    {
        _player  = player;
        _actions = MenuBindingHelpers.BuildBindingActions(menu.Localization, player);
    }

    public override bool   ShowsControllerDiagram => true;
    public override string Title => Menu.RebindingAction != null
        ? string.Format(Menu.Localization.PressKeyTitle,
            _actions.First(b => b.ConfigKey == Menu.RebindingAction).Label.ToUpper())
        : Menu.Localization.SettingsKeyboard.ToUpper();
    public override int      ItemCount => _actions.Length;
    public override string[] GetItems()
        => _actions
            .Select(b => b.ConfigKey == ""
                ? Menu.Localization.Back
                : $"{b.Label}\t{Menu.KeyboardLabel(b.ConfigKey)}")
            .ToArray();
    public override void Activate(int index)
    {
        var (_, configKey) = _actions[index];
        if (configKey == "")
        {
            // See GamepadBindingsHandler.Activate's identical comment: player 1's Back target
            // follows the other players to PlayerSelect once that submenu exists.
            Menu.NavigateTo(_player == 1 && Menu.Config.PlayerCount <= 1 ? Screen.Settings : Screen.PlayerSelect);
        }
        else
        {
            Menu.RebindingAction = configKey;
        }
    }
    public override string? GetActiveNesButton(int selectedItem)
    {
        var key = _actions[selectedItem].ConfigKey;
        return MenuBindingHelpers.IsNesButtonKey(key) ? key : null;
    }
}
