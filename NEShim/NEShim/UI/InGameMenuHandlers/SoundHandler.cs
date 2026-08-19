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

        public SoundHandler(InGameMenu menu) : base(menu) { }

        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 4;

        public override string[] GetItems()
        {
            var mode  = AudioFilterModeParser.Parse(Menu._config.AudioFilter);
            var items = new string[4];
            items[VolumeIndex] = Menu._localization.SoundVolume;
            items[FilterIndex] = $"{Menu._localization.AudioFilterLabel}: {MenuBindingHelpers.AudioFilterDisplayName(mode, Menu._localization)}";
            items[EqIndex]     = $"{Menu._localization.AudioEqLabel}: {EqSummary()}";
            items[BackIndex]   = Menu._localization.Back;
            return items;
        }

        public override SliderItemData? GetSliderData(int index)
        {
            if (index != VolumeIndex) return null;
            int volume = Menu._config.Volume;
            return new SliderItemData(Menu._localization.SoundVolume, volume / 100f, volume.ToString());
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
