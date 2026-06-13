using System.Linq;
using NEShim.Steam;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class GamepadBindingsHandler : ScreenHandler
    {
        public GamepadBindingsHandler(MainMenuScreen menu) : base(menu) { }
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
}
