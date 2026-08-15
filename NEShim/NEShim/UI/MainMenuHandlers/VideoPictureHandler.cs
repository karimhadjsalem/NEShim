using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class VideoPictureHandler : ScreenHandler
    {
        private static readonly VideoColorFilterMode[] AllColorModes = VideoColorFilterModeParser.AllModes;

        public const int ColorPresetIndex = 0;
        public const int BrightnessIndex  = 1;
        public const int ContrastIndex    = 2;
        public const int SaturationIndex  = 3;
        public const int HueIndex         = 4;
        public const int ResetIndex       = 5;
        // Back is index 6.

        public static bool IsSliderIndex(int index) => index >= BrightnessIndex && index <= HueIndex;

        public VideoPictureHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoPictureTitle;
        public override int    ItemCount => 7;

        public override string[] GetItems()
        {
            var currentColor = VideoColorFilterModeParser.Parse(Menu._config.VideoColorFilter);
            return
            [
                $"{Menu._localization.VideoColorPresetLabel}: {MenuBindingHelpers.VideoColorFilterDisplayName(currentColor, Menu._localization)}",
                Menu._localization.VideoBrightnessLabel,
                Menu._localization.VideoContrastLabel,
                Menu._localization.VideoSaturationLabel,
                Menu._localization.VideoHueLabel,
                Menu._localization.VideoResetPicture,
                Menu._localization.Back,
            ];
        }

        public override SliderItemData? GetSliderData(int index) => index switch
        {
            BrightnessIndex => new SliderItemData(Menu._localization.VideoBrightnessLabel, (Menu._config.VideoBrightness + 100) / 200f, FormatPct(Menu._config.VideoBrightness)),
            ContrastIndex   => new SliderItemData(Menu._localization.VideoContrastLabel,   (Menu._config.VideoContrast   + 100) / 200f, FormatPct(Menu._config.VideoContrast)),
            SaturationIndex => new SliderItemData(Menu._localization.VideoSaturationLabel, (Menu._config.VideoSaturation + 100) / 200f, FormatPct(Menu._config.VideoSaturation)),
            HueIndex        => new SliderItemData(Menu._localization.VideoHueLabel,        (Menu._config.VideoHue        + 100) / 200f, FormatPct(Menu._config.VideoHue)),
            _               => null,
        };

        public override void Activate(int index)
        {
            switch (index)
            {
                case ColorPresetIndex:
                    var current  = VideoColorFilterModeParser.Parse(Menu._config.VideoColorFilter);
                    int nextIdx  = (Array.IndexOf(AllColorModes, current) + 1) % AllColorModes.Length;
                    var nextMode = AllColorModes[nextIdx];
                    Menu._config.VideoColorFilter = nextMode.ToString();
                    Menu.ClearPreset();
                    Menu._onVideoColorFilterChanged(nextMode);
                    break;
                case ResetIndex:
                    Menu.ResetPicture();
                    break;
                case 6:
                    Menu.NavigateTo(Screen.Video);
                    break;
                // Slider indices (1–4): activation is a no-op; use left/right to adjust.
            }
        }

        private static string FormatPct(int value) => value == 0 ? "0" : value.ToString("+0;-0");
    }
}
