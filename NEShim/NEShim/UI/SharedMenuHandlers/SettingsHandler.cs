using NEShim.Localization;

namespace NEShim.UI;

internal sealed class SettingsHandler : SharedScreenHandler
{
    public SettingsHandler(IMenuHost menu) : base(menu) { }

    // Once multiplayer is enabled, player 1's own "Keyboard Controls"/"Gamepad Controls" rows no
    // longer show here directly — a single "Player Controls" row replaces both, leading to
    // PlayerSelectHandler, which now lists every player (including player 1) together. Showing
    // player 1's bindings both here AND in that submenu would be redundant. Mirrors
    // MainHandler/RootHandler's "Change Game" conditional-item gating (see their doc comments)
    // for how the item count/indices shift.
    private bool HasPlayerControls => Menu.Config.PlayerCount > 1;

    private const int VideoIndex = 0;
    private const int SoundIndex = 1;

    // PlayerCount == 1: Video, Sound, Keyboard, Gamepad, DpadStick, Language, Back (7 items).
    // PlayerCount  > 1: Video, Sound, Player Controls, DpadStick, Language, Back (6 items).
    private const int KeyboardIndex       = 2; // only reachable when !HasPlayerControls
    private const int GamepadIndex        = 3; // only reachable when !HasPlayerControls
    private const int PlayerControlsIndex = 2; // only reachable when HasPlayerControls
    private int DpadStickIndex => HasPlayerControls ? 3 : 4;
    private int LanguageIndex  => HasPlayerControls ? 4 : 5;
    private int BackIndex      => HasPlayerControls ? 5 : 6;

    public override string Title     => Menu.Localization.SettingsTitle;
    public override int    ItemCount => HasPlayerControls ? 6 : 7;

    public override string[] GetItems()
    {
        var items = new List<string> { Menu.Localization.SettingsVideo, Menu.Localization.SettingsSound };
        if (HasPlayerControls)
        {
            items.Add(Menu.Localization.SettingsPlayerControls);
        }
        else
        {
            items.Add(Menu.Localization.SettingsKeyboard);
            items.Add(Menu.Localization.SettingsGamepad);
        }
        items.Add(Menu.Config.GamepadDpadStickInterchangeable
            ? Menu.Localization.DpadStickInterchangeableOn
            : Menu.Localization.DpadStickInterchangeableOff);
        items.Add($"{Menu.Localization.SettingsLanguage}: {CurrentLanguageName()}");
        items.Add(Menu.Localization.Back);
        return items.ToArray();
    }

    private string CurrentLanguageName()
    {
        var code = Menu.Config.Language;
        if (code.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return Menu.Localization.LanguageAuto;
        return LanguageRegistry.FindByCode(code)?.NativeName ?? code;
    }

    public override void Activate(int index)
    {
        if (index == VideoIndex)     { Menu.NavigateTo(Screen.Video);    return; }
        if (index == SoundIndex)     { Menu.NavigateTo(Screen.Sound);    return; }
        if (index == DpadStickIndex)
        {
            Menu.Config.GamepadDpadStickInterchangeable = !Menu.Config.GamepadDpadStickInterchangeable;
            Menu.OnConfigSaved();
            return;
        }
        if (index == LanguageIndex) { Menu.NavigateTo(Screen.Language);  return; }
        if (index == BackIndex)     { Menu.NavigateTo(Menu.RootScreen); return; }

        if (HasPlayerControls)
        {
            if (index == PlayerControlsIndex) Menu.NavigateTo(Screen.PlayerSelect);
        }
        else
        {
            if (index == KeyboardIndex)      Menu.NavigateTo(Screen.KeyboardBindings);
            else if (index == GamepadIndex)  Menu.NavigateTo(Screen.GamepadBindings);
        }
    }
}
