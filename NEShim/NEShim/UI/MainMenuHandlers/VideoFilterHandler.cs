using NEShim.Platform;
using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class VideoFilterHandler : ScreenHandler
    {
        private VideoFilterMode[] FilterOptions =>
            PlatformDetector.IsD3D11Active
                ? VideoFilterModeParser.D3D11Supported
                : VideoFilterModeParser.GdiSupported;

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
                    ? $"✓ {FilterDisplayName(mode)}"
                    : $"  {FilterDisplayName(mode)}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index < FilterOptions.Length)
            {
                var mode = FilterOptions[index];
                Menu._config.VideoFilter = mode.ToString();
                Menu._onVideoFilterChanged(mode);
            }
            Menu.NavigateTo(Screen.Video);
        }

        private string FilterDisplayName(VideoFilterMode mode) => mode switch
        {
            VideoFilterMode.Bilinear      => Menu._localization.VideoFilterSmooth,
            VideoFilterMode.PixelPerfect  => Menu._localization.VideoFilterPixelPerfect,
            VideoFilterMode.CrtScanlines  => Menu._localization.VideoFilterCrtScanlines,
            VideoFilterMode.NtscComposite => Menu._localization.VideoFilterNtscComposite,
            _                             => mode.ToString(),
        };
    }
}
