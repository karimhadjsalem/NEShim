using NEShim.Audio;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class AudioFilterHandler : ScreenHandler
    {
        private static readonly AudioFilterMode[] AllFilters = Enum.GetValues<AudioFilterMode>();

        private int BackIndex => AllFilters.Length;

        public AudioFilterHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.AudioFilterTitle;
        public override int    ItemCount => AllFilters.Length + 1;

        public override string[] GetItems()
        {
            var current = AudioFilterModeParser.Parse(Menu._config.AudioFilter);
            var items   = new string[ItemCount];
            for (int i = 0; i < AllFilters.Length; i++)
            {
                var mode = AllFilters[i];
                items[i] = mode == current
                    ? $"✓ {AudioFilterModeParser.DisplayName(mode)}"
                    : $"  {AudioFilterModeParser.DisplayName(mode)}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index < AllFilters.Length)
            {
                var mode = AllFilters[index];
                Menu._config.AudioFilter = mode.ToString();
                Menu._onFilterChanged(mode);
            }
            Menu.NavigateTo(Screen.Sound);
        }
    }
}
