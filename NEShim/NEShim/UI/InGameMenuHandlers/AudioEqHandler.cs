namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class AudioEqHandler : ScreenHandler
    {
        public const int BassIndex   = 0;
        public const int MidIndex    = 1;
        public const int TrebleIndex = 2;
        private const int ResetIndex = 3;
        // Back is at index 4.

        public static bool IsSliderIndex(int index) => index <= TrebleIndex;

        private const int BarWidth = 12;

        private static string EqSlider(string label, int value)
        {
            int filled = (int)Math.Round((value + 12.0) / 24.0 * BarWidth);
            filled     = Math.Clamp(filled, 0, BarWidth);
            string bar = new string('█', filled) + new string('░', BarWidth - filled);
            string num = value == 0 ? "0" : value.ToString("+0;-0");
            return $"{label}\t◀{bar}▶ {num.PadLeft(4)}";
        }

        public AudioEqHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.AudioEqTitle;
        public override int    ItemCount => 5;

        public override string[] GetItems() =>
        [
            EqSlider(Menu._localization.AudioEqBass,   Menu._config.AudioEqBass),
            EqSlider(Menu._localization.AudioEqMid,    Menu._config.AudioEqMid),
            EqSlider(Menu._localization.AudioEqTreble, Menu._config.AudioEqTreble),
            Menu._localization.AudioEqReset,
            Menu._localization.Back,
        ];

        public override void Activate(int index)
        {
            if (index == ResetIndex) Menu.ResetEq();
            else if (index == 4)     Menu.NavigateTo(Screen.Sound);
        }
    }
}
