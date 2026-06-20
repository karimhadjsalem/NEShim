using NEShim.Localization;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class SettingsHandler : ScreenHandler
    {
        public SettingsHandler(MainMenuScreen menu) : base(menu) { }
        public override string   Title     => Menu._localization.SettingsTitle;
        public override int      ItemCount => 6;
        public override string[] GetItems() => new[]
        {
            Menu._localization.SettingsKeyboard,
            Menu._localization.SettingsGamepad,
            Menu._localization.SettingsVideo,
            Menu._localization.SettingsSound,
            $"{Menu._localization.SettingsLanguage}: {CurrentLanguageName()}",
            Menu._localization.Back,
        };

        private string CurrentLanguageName()
        {
            var code = Menu._config.Language;
            if (code.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                return Menu._localization.LanguageAuto;
            return LanguageRegistry.FindByCode(code)?.NativeName ?? code;
        }
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0: Menu.NavigateTo(Screen.KeyboardBindings); break;
                case 1: Menu.NavigateTo(Screen.GamepadBindings);  break;
                case 2: Menu.NavigateTo(Screen.Video);            break;
                case 3: Menu.NavigateTo(Screen.Sound);            break;
                case 4: Menu.NavigateTo(Screen.Language);         break;
                case 5: Menu.NavigateTo(Screen.Main);             break;
            }
        }
    }
}
