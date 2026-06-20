using System.Drawing;
using NEShim.Localization;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class LanguageHandler : ScreenHandler
    {
        // Index 0 = Auto, indices 1..N = languages, last = Back.
        private int LanguageCount => LanguageRegistry.AllLanguages.Count;
        private int BackIndex     => 1 + LanguageCount;

        public LanguageHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.LanguageTitle;
        public override int    ItemCount => 1 + LanguageCount + 1;

        public override string[] GetItems()
        {
            var    current = Menu._config.Language;
            bool   isAuto  = current.Equals("Auto", StringComparison.OrdinalIgnoreCase);
            var    items   = new string[ItemCount];

            items[0] = isAuto ? $"✓  {Menu._localization.LanguageAuto}"
                               : $"   {Menu._localization.LanguageAuto}";

            for (int i = 0; i < LanguageCount; i++)
            {
                var  lang     = LanguageRegistry.AllLanguages[i];
                bool selected = !isAuto && lang.Code.Equals(current, StringComparison.OrdinalIgnoreCase);
                items[i + 1]  = selected ? $"✓  {lang.NativeName}"
                                          : $"   {lang.NativeName}";
            }

            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index == 0)
            {
                Menu._config.Language = "Auto";
                Menu._onLanguageChanged("Auto");
            }
            else if (index < BackIndex)
            {
                var lang = LanguageRegistry.AllLanguages[index - 1];
                Menu._config.Language = lang.Code;
                Menu._onLanguageChanged(lang.Code);
            }
            Menu.NavigateTo(Screen.Settings);
        }

        public override Bitmap? GetItemIcon(int index)
        {
            // Language rows (1..N) have flag icons; Auto and Back have none.
            if (index > 0 && index < BackIndex)
                return FlagImageLoader.Get(index);
            return null;
        }
    }
}
