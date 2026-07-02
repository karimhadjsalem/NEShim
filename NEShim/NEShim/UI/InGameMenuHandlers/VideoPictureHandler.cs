namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class VideoPictureHandler : ScreenHandler
    {
        public const int BrightnessIndex = 0;
        public const int ContrastIndex   = 1;
        public const int SaturationIndex = 2;
        public const int ResetIndex      = 3;
        // Back is index 4.

        public static bool IsSliderIndex(int index) => index <= SaturationIndex;

        public VideoPictureHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoPictureTitle;
        public override int    ItemCount => 5;

        public override string[] GetItems() =>
        [
            string.Format(Menu._localization.VideoBrightnessLabel, Menu._config.VideoBrightness),
            string.Format(Menu._localization.VideoContrastLabel,   Menu._config.VideoContrast),
            string.Format(Menu._localization.VideoSaturationLabel, Menu._config.VideoSaturation),
            Menu._localization.VideoResetPicture,
            Menu._localization.Back,
        ];

        public override void Activate(int index)
        {
            if (index == ResetIndex)
                Menu.ResetPicture();
            else if (index >= ItemCount - 1)
                Menu.NavigateTo(Screen.Video);
            // Slider indices (0–2): activation is a no-op; use left/right to adjust.
        }
    }
}
