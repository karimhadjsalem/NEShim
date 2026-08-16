using NEShim.Rendering;

namespace NEShim.UI;

internal sealed class VideoHandler : SharedScreenHandler
{
    private static readonly OverscanMode[] OverscanCycle =
        [OverscanMode.Overscan, OverscanMode.Normal, OverscanMode.Underscan];

    private static readonly VideoFilterMode?[] OverlayCycle =
        [null, VideoFilterMode.CrtScanlines, VideoFilterMode.CrtPhosphor, VideoFilterMode.CrtScreen];

    public VideoHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.VideoTitle;
    public override int    ItemCount => Platform.PlatformDetector.SupportsAdvancedVideoFeatures ? 9 : 5;

    public override string[] GetItems()
    {
        var currentFilter   = VideoFilterModeParser.Parse(Menu.Config.VideoFilter);
        var currentOverscan = OverscanModeParser.Parse(Menu.Config.OverscanMode);

        string windowItem   = Menu.Config.WindowMode == "Fullscreen"
            ? Menu.Localization.VideoWindowFullscreen
            : Menu.Localization.VideoWindowWindowed;
        string filterItem   = $"{Menu.Localization.VideoFilterLabel}: {MenuBindingHelpers.VideoFilterDisplayName(currentFilter, Menu.Localization)}";
        string overscanItem = $"{Menu.Localization.OverscanLabel}: {MenuBindingHelpers.OverscanDisplayName(currentOverscan, Menu.Localization)}";
        string fpsItem      = Menu.Config.ShowFps ? Menu.Localization.VideoFpsOn : Menu.Localization.VideoFpsOff;

        if (!Platform.PlatformDetector.SupportsAdvancedVideoFeatures)
            return [windowItem, filterItem, overscanItem, fpsItem, Menu.Localization.Back];

        var overlayMode   = VideoFilterModeParser.ParseOverlay(Menu.Config.VideoFilterOverlay);
        var currentMotion = VideoMotionEffectModeParser.Parse(Menu.Config.VideoMotionEffect);
        string overlayItem  = $"{Menu.Localization.VideoOverlayLabel}: {MenuBindingHelpers.VideoOverlayDisplayName(overlayMode, Menu.Localization)}";
        string motionItem   = $"{Menu.Localization.VideoMotionEffectLabel}: {MenuBindingHelpers.VideoMotionEffectDisplayName(currentMotion, Menu.Localization)}";
        string pictureItem  = Menu.Localization.VideoPictureLabel;
        string presetsItem  = $"{Menu.Localization.VideoPresetsLabel}: {MenuBindingHelpers.VideoPresetDisplayName(Menu.Config.VideoPreset, Menu.Localization)}";
        return [presetsItem, windowItem, filterItem, overlayItem, motionItem, pictureItem, overscanItem, fpsItem, Menu.Localization.Back];
    }

    public override void Activate(int index)
    {
        // In GDI mode Presets, Overlay, Motion Effect, and Picture are hidden;
        // remap GDI indices to the D3D11 layout (Presets is D3D11-only at index 0).
        if (!Platform.PlatformDetector.SupportsAdvancedVideoFeatures)
            index = index >= 2 ? index + 4 : index + 1;

        switch (index)
        {
            case 0:
                Menu.NavigateTo(Screen.VideoPresets);
                break;
            case 1:
                Menu.OnWindowModeToggle(Menu.Config.WindowMode != "Fullscreen");
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
                var currentOverscan = OverscanModeParser.Parse(Menu.Config.OverscanMode);
                int nextIdx         = (Array.IndexOf(OverscanCycle, currentOverscan) + 1) % OverscanCycle.Length;
                var newOverscan     = OverscanCycle[nextIdx];
                Menu.Config.OverscanMode = newOverscan.ToString();
                Menu.ClearPreset();
                Menu.OnOverscanModeChanged(newOverscan);
                break;
            case 7:
                Menu.Config.ShowFps = !Menu.Config.ShowFps;
                Menu.OnConfigSaved();
                break;
            case 8:
                Menu.NavigateTo(Screen.Settings);
                break;
        }
    }

    private void CycleOverlay()
    {
        var current  = VideoFilterModeParser.ParseOverlay(Menu.Config.VideoFilterOverlay);
        var primary  = VideoFilterModeParser.Parse(Menu.Config.VideoFilter);
        int startIdx = Array.IndexOf(OverlayCycle, current);
        if (startIdx < 0) startIdx = 0;

        for (int i = 1; i <= OverlayCycle.Length; i++)
        {
            int nextIdx   = (startIdx + i) % OverlayCycle.Length;
            var candidate = OverlayCycle[nextIdx];
            if (!candidate.HasValue || candidate.Value != primary)
            {
                Menu.Config.VideoFilterOverlay = candidate.HasValue ? candidate.Value.ToString() : "None";
                Menu.ClearPreset();
                Menu.OnVideoFilterOverlayChanged(candidate);
                return;
            }
        }
    }
}
