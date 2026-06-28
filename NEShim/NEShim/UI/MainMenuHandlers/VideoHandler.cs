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
        public override int    ItemCount => NEShim.Platform.PlatformDetector.IsD3D11Active ? 7 : 5;

        public override string[] GetItems()
        {
            var currentFilter   = VideoFilterModeParser.Parse(Menu._config.VideoFilter);
            var currentOverscan = OverscanModeParser.Parse(Menu._config.OverscanMode);

            string windowItem   = Menu._config.WindowMode == "Fullscreen"
                ? Menu._localization.VideoWindowFullscreen
                : Menu._localization.VideoWindowWindowed;
            string filterItem   = $"{Menu._localization.VideoFilterLabel}: {FilterDisplayName(currentFilter)}";
            string overscanItem = $"{Menu._localization.OverscanLabel}: {OverscanDisplayName(currentOverscan)}";
            string fpsItem      = Menu._config.ShowFps ? Menu._localization.VideoFpsOn : Menu._localization.VideoFpsOff;

            if (!NEShim.Platform.PlatformDetector.IsD3D11Active)
                return [windowItem, filterItem, overscanItem, fpsItem, Menu._localization.Back];

            var currentColor  = VideoColorFilterModeParser.Parse(Menu._config.VideoColorFilter);
            var currentMotion = VideoMotionEffectModeParser.Parse(Menu._config.VideoMotionEffect);
            string colorItem  = $"{Menu._localization.VideoColorFilterLabel}: {ColorDisplayName(currentColor)}";
            string motionItem = $"{Menu._localization.VideoMotionEffectLabel}: {MotionDisplayName(currentMotion)}";
            return [windowItem, filterItem, colorItem, motionItem, overscanItem, fpsItem, Menu._localization.Back];
        }

        public override void Activate(int index)
        {
            // In GDI mode Color Effect and Motion Effect are hidden; shift indices ≥ 2 to match the full layout.
            if (!NEShim.Platform.PlatformDetector.IsD3D11Active && index >= 2)
                index += 2;

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
                    Menu.NavigateTo(Screen.VideoMotionEffect);
                    break;
                case 4:
                    var currentOverscan = OverscanModeParser.Parse(Menu._config.OverscanMode);
                    int nextIdx         = (Array.IndexOf(OverscanCycle, currentOverscan) + 1) % OverscanCycle.Length;
                    var newOverscan     = OverscanCycle[nextIdx];
                    Menu._config.OverscanMode = newOverscan.ToString();
                    Menu._onOverscanModeChanged(newOverscan);
                    break;
                case 5:
                    Menu._config.ShowFps = !Menu._config.ShowFps;
                    Menu._onConfigSaved();
                    break;
                case 6:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }

        private string FilterDisplayName(VideoFilterMode mode) => mode switch
        {
            VideoFilterMode.Bilinear      => Menu._localization.VideoFilterSmooth,
            VideoFilterMode.PixelPerfect  => Menu._localization.VideoFilterPixelPerfect,
            VideoFilterMode.CrtScanlines  => Menu._localization.VideoFilterCrtScanlines,
            VideoFilterMode.CrtPhosphor   => Menu._localization.VideoFilterCrtPhosphor,
            VideoFilterMode.NtscComposite => Menu._localization.VideoFilterNtscComposite,
            VideoFilterMode.CrtScreen     => Menu._localization.VideoFilterCrtScreen,
            _                             => mode.ToString(),
        };

        private string ColorDisplayName(VideoColorFilterMode mode) => mode switch
        {
            VideoColorFilterMode.None               => Menu._localization.VideoColorFilterNone,
            VideoColorFilterMode.Warm               => Menu._localization.VideoColorFilterWarm,
            VideoColorFilterMode.Greyscale          => Menu._localization.VideoColorFilterGreyscale,
            VideoColorFilterMode.NesColorCorrection => Menu._localization.VideoColorFilterNesColors,
            VideoColorFilterMode.Cool               => Menu._localization.VideoColorFilterCool,
            VideoColorFilterMode.PhosphorAmber      => Menu._localization.VideoColorFilterPhosphorAmber,
            VideoColorFilterMode.PhosphorGreen      => Menu._localization.VideoColorFilterPhosphorGreen,
            _                                       => mode.ToString(),
        };

        private string MotionDisplayName(VideoMotionEffectMode mode) => mode switch
        {
            VideoMotionEffectMode.None        => Menu._localization.VideoMotionEffectNone,
            VideoMotionEffectMode.CrtJitter   => Menu._localization.VideoMotionEffectCrtJitter,
            VideoMotionEffectMode.ScanlineBob => Menu._localization.VideoMotionEffectScanlineBob,
            _                                 => mode.ToString(),
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
