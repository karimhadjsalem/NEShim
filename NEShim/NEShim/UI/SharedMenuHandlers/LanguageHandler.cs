using NEShim.Localization;

namespace NEShim.UI;

internal sealed class LanguageHandler : SharedScreenHandler
{
    // Index 0 = Auto, indices 1..N = languages, last = Back.
    private int LanguageCount => LanguageRegistry.AllLanguages.Count;
    private int BackIndex     => 1 + LanguageCount;

    public LanguageHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.LanguageTitle;
    public override int    ItemCount => 1 + LanguageCount + 1;

    public override string[] GetItems()
    {
        var    current = Menu.Config.Language;
        bool   isAuto  = current.Equals("Auto", StringComparison.OrdinalIgnoreCase);
        var    items   = new string[ItemCount];

        items[0] = isAuto ? $"✓  {Menu.Localization.LanguageAuto}"
                           : $"   {Menu.Localization.LanguageAuto}";

        for (int i = 0; i < LanguageCount; i++)
        {
            var  lang     = LanguageRegistry.AllLanguages[i];
            bool selected = !isAuto && lang.Code.Equals(current, StringComparison.OrdinalIgnoreCase);
            items[i + 1]  = selected ? $"✓  {lang.NativeName}"
                                      : $"   {lang.NativeName}";
        }

        items[BackIndex] = Menu.Localization.Back;
        return items;
    }

    public override void Activate(int index)
    {
        if (index == 0)
        {
            Menu.Config.Language = "Auto";
            Menu.OnLanguageChanged("Auto");
        }
        else if (index < BackIndex)
        {
            var lang = LanguageRegistry.AllLanguages[index - 1];
            Menu.Config.Language = lang.Code;
            Menu.OnLanguageChanged(lang.Code);
        }
        Menu.NavigateTo(Screen.Settings);
    }

    public override IntPtr GetItemIcon(int index)
    {
        // Language rows (1..N) have flag icons; Auto and Back have none.
        if (index > 0 && index < BackIndex)
            return FlagImageLoader.Get(index);
        return IntPtr.Zero;
    }
}
