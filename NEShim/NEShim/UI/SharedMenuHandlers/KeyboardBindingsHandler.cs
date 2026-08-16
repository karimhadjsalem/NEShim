using System.Linq;

namespace NEShim.UI;

internal sealed class KeyboardBindingsHandler : SharedScreenHandler
{
    public KeyboardBindingsHandler(IMenuHost menu) : base(menu) { }
    public override bool   ShowsControllerDiagram => true;
    public override string Title => Menu.RebindingAction != null
        ? string.Format(Menu.Localization.PressKeyTitle,
            Menu.BindingActions.First(b => b.ConfigKey == Menu.RebindingAction).Label.ToUpper())
        : Menu.Localization.SettingsKeyboard.ToUpper();
    public override int      ItemCount => Menu.BindingActions.Length;
    public override string[] GetItems()
        => Menu.BindingActions
            .Select(b => b.ConfigKey == ""
                ? Menu.Localization.Back
                : $"{b.Label}\t{Menu.KeyboardLabel(b.ConfigKey)}")
            .ToArray();
    public override void Activate(int index)
    {
        var (_, configKey) = Menu.BindingActions[index];
        if (configKey == "")
            Menu.NavigateTo(Screen.Settings);
        else
            Menu.RebindingAction = configKey;
    }
}
