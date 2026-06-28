using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class VideoOverlayHandler : ScreenHandler
    {
        private static readonly VideoFilterMode[] OverlayOptions = VideoFilterModeParser.OverlaySupported;

        private const int NoneIndex = 0;
        private int       BackIndex => NoneIndex + 1 + OverlayOptions.Length;

        public VideoOverlayHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoFilterOverlayTitle;
        public override int    ItemCount => BackIndex + 1;

        public override string[] GetItems()
        {
            var current = VideoFilterModeParser.ParseOverlay(Menu._config.VideoFilterOverlay);
            var items   = new string[ItemCount];
            items[0] = current is null
                ? $"✓ {Menu._localization.VideoMotionEffectNone}"
                : $"  {Menu._localization.VideoMotionEffectNone}";
            for (int i = 0; i < OverlayOptions.Length; i++)
            {
                var mode = OverlayOptions[i];
                items[i + 1] = mode == current
                    ? $"✓ {OverlayDisplayName(mode)}"
                    : $"  {OverlayDisplayName(mode)}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override bool IsItemEnabled(int index)
        {
            if (index == NoneIndex || index >= BackIndex) return true;
            var primary = VideoFilterModeParser.Parse(Menu._config.VideoFilter);
            return OverlayOptions[index - 1] != primary;
        }

        public override void Activate(int index)
        {
            if (index == 0)
            {
                Menu._config.VideoFilterOverlay = "None";
                Menu._onVideoFilterOverlayChanged(null);
            }
            else if (index <= OverlayOptions.Length)
            {
                var mode = OverlayOptions[index - 1];
                Menu._config.VideoFilterOverlay = mode.ToString();
                Menu._onVideoFilterOverlayChanged(mode);
            }
            Menu.NavigateTo(Screen.VideoFilter);
        }

        private string OverlayDisplayName(VideoFilterMode mode) => mode switch
        {
            VideoFilterMode.CrtScanlines => Menu._localization.VideoFilterCrtScanlines,
            VideoFilterMode.CrtPhosphor  => Menu._localization.VideoFilterCrtPhosphor,
            VideoFilterMode.CrtScreen    => Menu._localization.VideoFilterCrtScreen,
            _                            => mode.ToString(),
        };
    }
}
