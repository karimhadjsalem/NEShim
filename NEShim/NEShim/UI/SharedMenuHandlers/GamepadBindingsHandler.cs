using System.Linq;
using NEShim.Steam;

namespace NEShim.UI;

internal sealed class GamepadBindingsHandler : SharedScreenHandler
{
    public GamepadBindingsHandler(IMenuHost menu) : base(menu) { }
    public override bool   ShowsControllerDiagram => true;
    public override string Title => Menu.GamepadRebindingAction != null
        ? string.Format(Menu.Localization.PressButtonTitle,
            Menu.GamepadBindingActions.First(b => b.ConfigKey == Menu.GamepadRebindingAction).Label.ToUpper())
        : Menu.Localization.SettingsGamepad.ToUpper();
    public override int      ItemCount => Menu.GamepadBindingActions.Length;
    public override string[] GetItems()
        => Menu.GamepadBindingActions
            .Select(b => b.ConfigKey == ""
                ? Menu.Localization.Back
                : $"{b.Label}\t{Menu.GetGamepadLabel(b.ConfigKey)}")
            .ToArray();
    public override bool IsItemEnabled(int index)
    {
        if (!SteamInputManager.IsUsingNativeActions()) return true;
        var configKey = Menu.GamepadBindingActions[index].ConfigKey;
        return configKey == "" || configKey == "OpenMenu";
    }
    public override IntPtr GetItemValueIcon(int index)
    {
        var configKey = Menu.GamepadBindingActions[index].ConfigKey;
        return configKey == "" ? IntPtr.Zero : Menu.GetGamepadGlyph(configKey);
    }
    public override void Activate(int index)
    {
        var (_, configKey) = Menu.GamepadBindingActions[index];
        if (configKey == "")
            Menu.NavigateTo(Screen.Settings);
        else
            Menu.GamepadRebindingAction = configKey;
    }
}
