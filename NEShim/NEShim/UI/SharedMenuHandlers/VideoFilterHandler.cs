using NEShim.Rendering;

namespace NEShim.UI;

internal sealed class VideoFilterHandler : SharedScreenHandler
{
    private VideoFilterMode[] FilterOptions => VideoFilterModeParser.D3D11Supported;

    private int BackIndex => FilterOptions.Length;

    public VideoFilterHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.VideoFilterTitle;
    public override int    ItemCount => FilterOptions.Length + 1;

    public override string[] GetItems()
    {
        var current = VideoFilterModeParser.Parse(Menu.Config.VideoFilter);
        var options = FilterOptions;
        var items   = new string[ItemCount];
        for (int i = 0; i < options.Length; i++)
        {
            var mode = options[i];
            items[i] = mode == current
                ? $"✓ {MenuBindingHelpers.VideoFilterDisplayName(mode, Menu.Localization)}"
                : $"  {MenuBindingHelpers.VideoFilterDisplayName(mode, Menu.Localization)}";
        }
        items[BackIndex] = Menu.Localization.Back;
        return items;
    }

    public override void Activate(int index)
    {
        if (index < FilterOptions.Length)
        {
            var mode = FilterOptions[index];
            Menu.Config.VideoFilter = mode.ToString();
            Menu.ClearPreset();
            Menu.OnVideoFilterChanged(mode);
            var overlay = VideoFilterModeParser.ParseOverlay(Menu.Config.VideoFilterOverlay);
            if (overlay.HasValue && overlay.Value == mode)
            {
                Menu.Config.VideoFilterOverlay = "None";
                Menu.OnVideoFilterOverlayChanged(null);
            }
        }
        Menu.NavigateTo(Screen.Video);
    }
}
