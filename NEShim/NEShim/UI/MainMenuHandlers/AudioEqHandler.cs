namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class AudioEqHandler : ScreenHandler
    {
        public const int BassIndex   = 0;
        public const int MidIndex    = 1;
        public const int TrebleIndex = 2;
        private const int ResetIndex = 3;
        // Back is at index 4.

        public static bool IsSliderIndex(int index) => index <= TrebleIndex;

        public AudioEqHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.AudioEqTitle;
        public override int    ItemCount => 5;

        public override string[] GetItems() =>
        [
            Menu._localization.AudioEqBass,
            Menu._localization.AudioEqMid,
            Menu._localization.AudioEqTreble,
            Menu._localization.AudioEqReset,
            Menu._localization.Back,
        ];

        public override SliderItemData? GetSliderData(int index) => index switch
        {
            BassIndex   => new SliderItemData(Menu._localization.AudioEqBass,   (Menu._config.AudioEqBass   + 12) / 24f, FormatDb(Menu._config.AudioEqBass)),
            MidIndex    => new SliderItemData(Menu._localization.AudioEqMid,    (Menu._config.AudioEqMid    + 12) / 24f, FormatDb(Menu._config.AudioEqMid)),
            TrebleIndex => new SliderItemData(Menu._localization.AudioEqTreble, (Menu._config.AudioEqTreble + 12) / 24f, FormatDb(Menu._config.AudioEqTreble)),
            _           => null,
        };

        public override void Activate(int index)
        {
            if (index == ResetIndex) Menu.ResetEq();
            else if (index == 4)     Menu.NavigateTo(Screen.Sound);
        }

        private static string FormatDb(int value) => value == 0 ? "0" : value.ToString("+0;-0");
    }
}
