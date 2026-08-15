using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class VideoFilterHandler : ScreenHandler
    {
        private VideoFilterMode[] FilterOptions => VideoFilterModeParser.D3D11Supported;

        private int BackIndex => FilterOptions.Length;

        public VideoFilterHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoFilterTitle;
        public override int    ItemCount => FilterOptions.Length + 1;

        public override string[] GetItems()
        {
            var current = VideoFilterModeParser.Parse(Menu._config.VideoFilter);
            var options = FilterOptions;
            var items   = new string[ItemCount];
            for (int i = 0; i < options.Length; i++)
            {
                var mode = options[i];
                items[i] = mode == current
                    ? $"✓ {MenuBindingHelpers.VideoFilterDisplayName(mode, Menu._localization)}"
                    : $"  {MenuBindingHelpers.VideoFilterDisplayName(mode, Menu._localization)}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index < FilterOptions.Length)
            {
                var mode    = FilterOptions[index];
                Menu._config.VideoFilter = mode.ToString();
                Menu.ClearPreset();
                Menu._onVideoFilterChanged(mode);
                var overlay = VideoFilterModeParser.ParseOverlay(Menu._config.VideoFilterOverlay);
                if (overlay.HasValue && overlay.Value == mode)
                {
                    Menu._config.VideoFilterOverlay = "None";
                    Menu._onVideoFilterOverlayChanged(null);
                }
            }
            Menu.NavigateTo(Screen.Video);
        }
    }
}
