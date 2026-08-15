using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class VideoHandler : ScreenHandler
    {
        private static readonly OverscanMode[] OverscanCycle =
            [OverscanMode.Overscan, OverscanMode.Normal, OverscanMode.Underscan];

        private static readonly VideoFilterMode?[] OverlayCycle =
            [null, VideoFilterMode.CrtScanlines, VideoFilterMode.CrtPhosphor, VideoFilterMode.CrtScreen];

        public VideoHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoTitle;
        public override int    ItemCount => NEShim.Platform.PlatformDetector.SupportsAdvancedVideoFeatures ? 9 : 5;

        public override string[] GetItems()
        {
            var currentFilter   = VideoFilterModeParser.Parse(Menu._config.VideoFilter);
            var currentOverscan = OverscanModeParser.Parse(Menu._config.OverscanMode);

            string windowItem   = Menu._config.WindowMode == "Fullscreen"
                ? Menu._localization.VideoWindowFullscreen
                : Menu._localization.VideoWindowWindowed;
            string filterItem   = $"{Menu._localization.VideoFilterLabel}: {MenuBindingHelpers.VideoFilterDisplayName(currentFilter, Menu._localization)}";
            string overscanItem = $"{Menu._localization.OverscanLabel}: {MenuBindingHelpers.OverscanDisplayName(currentOverscan, Menu._localization)}";
            string fpsItem      = Menu._config.ShowFps ? Menu._localization.VideoFpsOn : Menu._localization.VideoFpsOff;

            if (!NEShim.Platform.PlatformDetector.SupportsAdvancedVideoFeatures)
                return [windowItem, filterItem, overscanItem, fpsItem, Menu._localization.Back];

            var overlayMode   = VideoFilterModeParser.ParseOverlay(Menu._config.VideoFilterOverlay);
            var currentMotion = VideoMotionEffectModeParser.Parse(Menu._config.VideoMotionEffect);
            string overlayItem  = $"{Menu._localization.VideoOverlayLabel}: {MenuBindingHelpers.VideoOverlayDisplayName(overlayMode, Menu._localization)}";
            string motionItem   = $"{Menu._localization.VideoMotionEffectLabel}: {MenuBindingHelpers.VideoMotionEffectDisplayName(currentMotion, Menu._localization)}";
            string pictureItem  = Menu._localization.VideoPictureLabel;
            string presetsItem  = $"{Menu._localization.VideoPresetsLabel}: {MenuBindingHelpers.VideoPresetDisplayName(Menu._config.VideoPreset, Menu._localization)}";
            return [presetsItem, windowItem, filterItem, overlayItem, motionItem, pictureItem, overscanItem, fpsItem, Menu._localization.Back];
        }

        public override void Activate(int index)
        {
            // In GDI mode Presets, Overlay, Motion Effect, and Picture are hidden;
            // remap GDI indices to the D3D11 layout (Presets is D3D11-only at index 0).
            if (!NEShim.Platform.PlatformDetector.SupportsAdvancedVideoFeatures)
                index = index >= 2 ? index + 4 : index + 1;

            switch (index)
            {
                case 0:
                    Menu.NavigateTo(Screen.VideoPresets);
                    break;
                case 1:
                    Menu._onWindowModeToggle(Menu._config.WindowMode != "Fullscreen");
                    break;
                case 2:
                    Menu.NavigateTo(Screen.VideoFilter);
                    break;
                case 3:
                    CycleOverlay();
                    break;
                case 4:
                    Menu.NavigateTo(Screen.VideoMotionEffect);
                    break;
                case 5:
                    Menu.NavigateTo(Screen.VideoPicture);
                    break;
                case 6:
                    var currentOverscan = OverscanModeParser.Parse(Menu._config.OverscanMode);
                    int nextIdx         = (Array.IndexOf(OverscanCycle, currentOverscan) + 1) % OverscanCycle.Length;
                    var newOverscan     = OverscanCycle[nextIdx];
                    Menu._config.OverscanMode = newOverscan.ToString();
                    Menu.ClearPreset();
                    Menu._onOverscanModeChanged(newOverscan);
                    break;
                case 7:
                    Menu._config.ShowFps = !Menu._config.ShowFps;
                    Menu._onConfigSaved();
                    break;
                case 8:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }

        private void CycleOverlay()
        {
            var current  = VideoFilterModeParser.ParseOverlay(Menu._config.VideoFilterOverlay);
            var primary  = VideoFilterModeParser.Parse(Menu._config.VideoFilter);
            int startIdx = Array.IndexOf(OverlayCycle, current);
            if (startIdx < 0) startIdx = 0;

            for (int i = 1; i <= OverlayCycle.Length; i++)
            {
                int nextIdx   = (startIdx + i) % OverlayCycle.Length;
                var candidate = OverlayCycle[nextIdx];
                if (!candidate.HasValue || candidate.Value != primary)
                {
                    Menu._config.VideoFilterOverlay = candidate.HasValue ? candidate.Value.ToString() : "None";
                    Menu.ClearPreset();
                    Menu._onVideoFilterOverlayChanged(candidate);
                    return;
                }
            }
        }

    }
}
