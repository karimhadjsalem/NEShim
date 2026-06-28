using NEShim.Platform;
using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class VideoFilterHandler : ScreenHandler
    {
        private VideoFilterMode[] FilterOptions =>
            PlatformDetector.IsD3D11Active
                ? VideoFilterModeParser.D3D11Supported
                : VideoFilterModeParser.GdiSupported;

        private int OverlayIndex => FilterOptions.Length;
        private int BackIndex    => PlatformDetector.IsD3D11Active ? FilterOptions.Length + 1 : FilterOptions.Length;

        public VideoFilterHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoFilterTitle;
        public override int    ItemCount => FilterOptions.Length + (PlatformDetector.IsD3D11Active ? 2 : 1);

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
            if (PlatformDetector.IsD3D11Active)
                items[OverlayIndex] = $"  {Menu._localization.VideoFilterOverlayLabel} →";
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override bool IsItemEnabled(int index)
        {
            if (index >= FilterOptions.Length) return true;
            var overlay = VideoFilterModeParser.ParseOverlay(Menu._config.VideoFilterOverlay);
            return overlay is null || FilterOptions[index] != overlay.Value;
        }

        public override void Activate(int index)
        {
            if (index < FilterOptions.Length)
            {
                var mode = FilterOptions[index];
                Menu._config.VideoFilter = mode.ToString();
                Menu._onVideoFilterChanged(mode);
                Menu.NavigateTo(Screen.Video);
            }
            else if (PlatformDetector.IsD3D11Active && index == OverlayIndex)
            {
                Menu.NavigateTo(Screen.VideoOverlay);
            }
            else
            {
                Menu.NavigateTo(Screen.Video);
            }
        }

        internal string FilterDisplayName(VideoFilterMode mode) => mode switch
        {
            VideoFilterMode.Bilinear      => Menu._localization.VideoFilterSmooth,
            VideoFilterMode.PixelPerfect  => Menu._localization.VideoFilterPixelPerfect,
            VideoFilterMode.CrtScanlines  => Menu._localization.VideoFilterCrtScanlines,
            VideoFilterMode.CrtPhosphor   => Menu._localization.VideoFilterCrtPhosphor,
            VideoFilterMode.NtscComposite => Menu._localization.VideoFilterNtscComposite,
            VideoFilterMode.CrtScreen     => Menu._localization.VideoFilterCrtScreen,
            _                             => mode.ToString(),
        };
    }
}
