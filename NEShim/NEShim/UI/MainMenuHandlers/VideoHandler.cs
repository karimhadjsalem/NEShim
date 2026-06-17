using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class VideoHandler : ScreenHandler
    {
        private static readonly OverscanMode[] OverscanCycle =
            [OverscanMode.Overscan, OverscanMode.Normal, OverscanMode.Underscan];

        public VideoHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoTitle;
        public override int    ItemCount => 6;

        public override string[] GetItems()
        {
            var currentFilter   = VideoFilterModeParser.Parse(Menu._config.VideoFilter);
            var currentColor    = VideoColorFilterModeParser.Parse(Menu._config.VideoColorFilter);
            var currentOverscan = OverscanModeParser.Parse(Menu._config.OverscanMode);
            return new[]
            {
                Menu._config.WindowMode == "Fullscreen"
                    ? Menu._localization.VideoWindowFullscreen
                    : Menu._localization.VideoWindowWindowed,
                $"{Menu._localization.VideoFilterLabel}: {FilterDisplayName(currentFilter)}",
                $"{Menu._localization.VideoColorFilterLabel}: {ColorDisplayName(currentColor)}",
                $"{Menu._localization.OverscanLabel}: {OverscanDisplayName(currentOverscan)}",
                Menu._config.ShowFps ? Menu._localization.VideoFpsOn : Menu._localization.VideoFpsOff,
                Menu._localization.Back,
            };
        }

        public override void Activate(int index)
        {
            switch (index)
            {
                case 0:
                    Menu._onWindowModeToggle(Menu._config.WindowMode != "Fullscreen");
                    break;
                case 1:
                    Menu.NavigateTo(Screen.VideoFilter);
                    break;
                case 2:
                    Menu.NavigateTo(Screen.VideoColorFilter);
                    break;
                case 3:
                    var currentOverscan = OverscanModeParser.Parse(Menu._config.OverscanMode);
                    int nextIdx         = (Array.IndexOf(OverscanCycle, currentOverscan) + 1) % OverscanCycle.Length;
                    var newOverscan     = OverscanCycle[nextIdx];
                    Menu._config.OverscanMode = newOverscan.ToString();
                    Menu._onOverscanModeChanged(newOverscan);
                    break;
                case 4:
                    Menu._config.ShowFps = !Menu._config.ShowFps;
                    Menu._onConfigSaved();
                    break;
                case 5:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }

        private string FilterDisplayName(VideoFilterMode mode) => mode switch
        {
            VideoFilterMode.Bilinear      => Menu._localization.VideoFilterSmooth,
            VideoFilterMode.PixelPerfect  => Menu._localization.VideoFilterPixelPerfect,
            VideoFilterMode.CrtScanlines  => Menu._localization.VideoFilterCrtScanlines,
            VideoFilterMode.NtscComposite => Menu._localization.VideoFilterNtscComposite,
            _                             => mode.ToString(),
        };

        private string ColorDisplayName(VideoColorFilterMode mode) => mode switch
        {
            VideoColorFilterMode.None               => Menu._localization.VideoColorFilterNone,
            VideoColorFilterMode.Warm               => Menu._localization.VideoColorFilterWarm,
            VideoColorFilterMode.Greyscale          => Menu._localization.VideoColorFilterGreyscale,
            VideoColorFilterMode.NesColorCorrection => Menu._localization.VideoColorFilterNesColors,
            _                                       => mode.ToString(),
        };

        private string OverscanDisplayName(OverscanMode mode) => mode switch
        {
            OverscanMode.Overscan  => Menu._localization.OverscanOverscan,
            OverscanMode.Normal    => Menu._localization.OverscanNormal,
            OverscanMode.Underscan => Menu._localization.OverscanUnderscan,
            _                      => mode.ToString(),
        };
    }
}
