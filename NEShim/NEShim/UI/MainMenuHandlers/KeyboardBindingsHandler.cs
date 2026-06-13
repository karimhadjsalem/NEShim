using System.Linq;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class KeyboardBindingsHandler : ScreenHandler
    {
        public KeyboardBindingsHandler(MainMenuScreen menu) : base(menu) { }
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
}
