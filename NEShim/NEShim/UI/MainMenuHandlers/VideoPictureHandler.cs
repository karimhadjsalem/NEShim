using System.Drawing;
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

        private const int BarWidth = 20;

        private static string PictureSlider(string label, int value)
        {
            int filled = (int)Math.Round((value + 100.0) / 200.0 * BarWidth);
            filled     = Math.Clamp(filled, 0, BarWidth);
            string bar = new string('█', filled) + new string('░', BarWidth - filled);
            string num = value == 0 ? "0" : value.ToString("+0;-0");
            return $"{label}\t◀{bar}▶  {num.PadLeft(4)}";
        }

        public VideoPictureHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoPictureTitle;
        public override int    ItemCount => 7;

        public override string[] GetItems()
        {
            var currentColor = VideoColorFilterModeParser.Parse(Menu._config.VideoColorFilter);
            return
            [
                $"{Menu._localization.VideoColorPresetLabel}: {ColorDisplayName(currentColor)}",
                PictureSlider(Menu._localization.VideoBrightnessLabel, Menu._config.VideoBrightness),
                PictureSlider(Menu._localization.VideoContrastLabel,   Menu._config.VideoContrast),
                PictureSlider(Menu._localization.VideoSaturationLabel, Menu._config.VideoSaturation),
                PictureSlider(Menu._localization.VideoHueLabel,        Menu._config.VideoHue),
                Menu._localization.VideoResetPicture,
                Menu._localization.Back,
            ];
        }

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
    }
}
