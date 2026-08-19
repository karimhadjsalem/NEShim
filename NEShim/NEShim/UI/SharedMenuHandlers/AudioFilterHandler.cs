using NEShim.Audio;

namespace NEShim.UI;

internal sealed class AudioFilterHandler : SharedScreenHandler
{
    private static readonly AudioFilterMode[] AllFilters = Enum.GetValues<AudioFilterMode>();

    private int BackIndex => AllFilters.Length;

    public AudioFilterHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.AudioFilterTitle;
    public override int    ItemCount => AllFilters.Length + 1;

    public override string[] GetItems()
    {
        var current = AudioFilterModeParser.Parse(Menu.Config.AudioFilter);
        var items   = new string[ItemCount];
        for (int i = 0; i < AllFilters.Length; i++)
        {
            var mode = AllFilters[i];
            items[i] = mode == current
                ? $"✓ {MenuBindingHelpers.AudioFilterDisplayName(mode, Menu.Localization)}"
                : $"  {MenuBindingHelpers.AudioFilterDisplayName(mode, Menu.Localization)}";
        }
        items[BackIndex] = Menu.Localization.Back;
        return items;
    }

    public override void Activate(int index)
    {
        if (index < AllFilters.Length)
        {
            var mode = AllFilters[index];
            Menu.Config.AudioFilter = mode.ToString();
            Menu.OnFilterChanged(mode);
        }
        Menu.NavigateTo(Screen.Sound);
    }
}
