using NEShim.Audio;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class SoundHandler : ScreenHandler
    {
        public  const int VolumeIndex = 0;
        private const int FilterIndex = 1;
        private const int EqIndex     = 2;
        private const int MusicIndex  = 3;
        private const int BackIndex   = 4;

        public SoundHandler(MainMenuScreen menu) : base(menu) { }

        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 5;

        public override string[] GetItems()
        {
            var mode  = AudioFilterModeParser.Parse(Menu._config.AudioFilter);
            var items = new string[5];
            items[VolumeIndex] = Menu._localization.SoundVolume;
            items[FilterIndex] = $"{Menu._localization.AudioFilterLabel}: {Menu.AudioFilterDisplayName(mode)}";
            items[EqIndex]     = $"{Menu._localization.AudioEqLabel}: {EqSummary()}";
            items[MusicIndex]  = Menu._config.MainMenuMusicEnabled
                ? Menu._localization.SoundMusicOn
                : Menu._localization.SoundMusicOff;
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
            {
                Menu.NavigateTo(Screen.AudioFilter);
                return;
            }
            if (index == EqIndex)
            {
                Menu.NavigateTo(Screen.AudioEq);
                return;
            }
            if (index == MusicIndex)
            {
                bool musicOn = !Menu._config.MainMenuMusicEnabled;
                Menu._config.MainMenuMusicEnabled = musicOn;
                Menu._onMenuMusicToggled(musicOn);
                return;
            }
            if (index == BackIndex)
                Menu.NavigateTo(Screen.Settings);
        }

        private string EqSummary() =>
            Menu._config.AudioEqBass == 0 && Menu._config.AudioEqMid == 0 && Menu._config.AudioEqTreble == 0
                ? Menu._localization.AudioEqFlat
                : Menu._localization.AudioEqCustom;
    }
}
