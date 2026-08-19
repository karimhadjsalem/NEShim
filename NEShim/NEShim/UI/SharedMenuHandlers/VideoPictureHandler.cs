using NEShim.Rendering;

namespace NEShim.UI;

internal sealed class VideoPictureHandler : SharedScreenHandler
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

    public VideoPictureHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.VideoPictureTitle;
    public override int    ItemCount => 7;

    public override string[] GetItems()
    {
        var currentColor = VideoColorFilterModeParser.Parse(Menu.Config.VideoColorFilter);
        return
        [
            $"{Menu.Localization.VideoColorPresetLabel}: {MenuBindingHelpers.VideoColorFilterDisplayName(currentColor, Menu.Localization)}",
            Menu.Localization.VideoBrightnessLabel,
            Menu.Localization.VideoContrastLabel,
            Menu.Localization.VideoSaturationLabel,
            Menu.Localization.VideoHueLabel,
            Menu.Localization.VideoResetPicture,
            Menu.Localization.Back,
        ];
    }

    public override SliderItemData? GetSliderData(int index) => index switch
    {
        BrightnessIndex => new SliderItemData(Menu.Localization.VideoBrightnessLabel, (Menu.Config.VideoBrightness + 100) / 200f, FormatPct(Menu.Config.VideoBrightness)),
        ContrastIndex   => new SliderItemData(Menu.Localization.VideoContrastLabel,   (Menu.Config.VideoContrast   + 100) / 200f, FormatPct(Menu.Config.VideoContrast)),
        SaturationIndex => new SliderItemData(Menu.Localization.VideoSaturationLabel, (Menu.Config.VideoSaturation + 100) / 200f, FormatPct(Menu.Config.VideoSaturation)),
        HueIndex        => new SliderItemData(Menu.Localization.VideoHueLabel,        (Menu.Config.VideoHue        + 100) / 200f, FormatPct(Menu.Config.VideoHue)),
        _               => null,
    };

    public override void Activate(int index)
    {
        switch (index)
        {
            case ColorPresetIndex:
                var current  = VideoColorFilterModeParser.Parse(Menu.Config.VideoColorFilter);
                int nextIdx  = (Array.IndexOf(AllColorModes, current) + 1) % AllColorModes.Length;
                var nextMode = AllColorModes[nextIdx];
                Menu.Config.VideoColorFilter = nextMode.ToString();
                Menu.ClearPreset();
                Menu.OnVideoColorFilterChanged(nextMode);
                break;
            case ResetIndex:
                Menu.ResetPicture();
                break;
            case 6:
                Menu.NavigateTo(Screen.Video);
                break;
            // Slider indices (1-4): activation is a no-op; use left/right to adjust.
        }
    }

    private static string FormatPct(int value) => value == 0 ? "0" : value.ToString("+0;-0");
}
