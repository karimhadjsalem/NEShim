using NEShim.Audio;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class SoundHandler : ScreenHandler
    {
        public  const int VolumeIndex = 0;
        private const int FilterIndex = 1;
        private const int EqIndex     = 2;
        private const int BackIndex   = 3;

        private const int BarWidth = 20;

        private static string VolumeSlider(string label, int value)
        {
            int filled = (int)Math.Round(value / 100.0 * BarWidth);
            filled     = Math.Clamp(filled, 0, BarWidth);
            string bar = new string('█', filled) + new string('░', BarWidth - filled);
            return $"{label}  ◀{bar}▶  {value.ToString().PadLeft(3)}";
        }

        public SoundHandler(InGameMenu menu) : base(menu) { }

        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 4;

        public override string[] GetItems()
        {
            var mode  = AudioFilterModeParser.Parse(Menu._config.AudioFilter);
            var items = new string[4];
            items[VolumeIndex] = VolumeSlider(Menu._localization.SoundVolume, Menu._config.Volume);
            items[FilterIndex] = $"{Menu._localization.AudioFilterLabel}: {Menu.AudioFilterDisplayName(mode)}";
            items[EqIndex]     = $"{Menu._localization.AudioEqLabel}: {EqSummary()}";
            items[BackIndex]   = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index == FilterIndex)
                Menu.NavigateTo(Screen.AudioFilter);
            else if (index == EqIndex)
                Menu.NavigateTo(Screen.AudioEq);
            else if (index == BackIndex)
                Menu.NavigateTo(Screen.Settings);
        }

        private string EqSummary() =>
            Menu._config.AudioEqBass == 0 && Menu._config.AudioEqMid == 0 && Menu._config.AudioEqTreble == 0
                ? Menu._localization.AudioEqFlat
                : Menu._localization.AudioEqCustom;
    }
}
