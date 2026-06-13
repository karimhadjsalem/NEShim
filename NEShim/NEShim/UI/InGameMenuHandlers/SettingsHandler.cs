namespace NEShim.UI;

internal sealed partial class InGameMenu
{
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
}
