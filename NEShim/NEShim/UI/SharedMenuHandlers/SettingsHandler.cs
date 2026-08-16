using NEShim.Localization;

namespace NEShim.UI;

internal sealed class SettingsHandler : SharedScreenHandler
{
    public SettingsHandler(IMenuHost menu) : base(menu) { }
    public override string   Title     => Menu.Localization.SettingsTitle;
    public override int      ItemCount => 7;
    public override string[] GetItems() => new[]
    {
        Menu.Localization.SettingsVideo,
        Menu.Localization.SettingsSound,
        Menu.Localization.SettingsKeyboard,
        Menu.Localization.SettingsGamepad,
        Menu.Config.GamepadDpadStickInterchangeable
            ? Menu.Localization.DpadStickInterchangeableOn
            : Menu.Localization.DpadStickInterchangeableOff,
        $"{Menu.Localization.SettingsLanguage}: {CurrentLanguageName()}",
        Menu.Localization.Back,
    };

    private string CurrentLanguageName()
    {
        var code = Menu.Config.Language;
        if (code.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return Menu.Localization.LanguageAuto;
        return LanguageRegistry.FindByCode(code)?.NativeName ?? code;
    }
    public override void Activate(int index)
    {
        switch (index)
        {
            case 0: Menu.NavigateTo(Screen.Video);            break;
            case 1: Menu.NavigateTo(Screen.Sound);            break;
            case 2: Menu.NavigateTo(Screen.KeyboardBindings); break;
            case 3: Menu.NavigateTo(Screen.GamepadBindings);  break;
            case 4:
                Menu.Config.GamepadDpadStickInterchangeable = !Menu.Config.GamepadDpadStickInterchangeable;
                Menu.OnConfigSaved();
                break;
            case 5: Menu.NavigateTo(Screen.Language);         break;
            case 6: Menu.NavigateTo(Menu.RootScreen);         break;
        }
    }
}
