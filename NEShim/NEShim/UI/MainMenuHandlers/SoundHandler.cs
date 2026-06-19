using NEShim.Audio;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class SoundHandler : ScreenHandler
    {
        public  const int VolumeIndex = 0;
        private const int FilterIndex = 1;
        private const int MusicIndex  = 2;
        private const int BackIndex   = 3;

        public SoundHandler(MainMenuScreen menu) : base(menu) { }

        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 4;

        public override string[] GetItems()
        {
            var mode  = AudioFilterModeParser.Parse(Menu._config.AudioFilter);
            var items = new string[4];
            items[VolumeIndex] = string.Format(Menu._localization.SoundVolume, Menu._config.Volume);
            items[FilterIndex] = $"{Menu._localization.AudioFilterLabel}: {Menu.AudioFilterDisplayName(mode)}";
            items[MusicIndex]  = Menu._config.MainMenuMusicEnabled
                ? Menu._localization.SoundMusicOn
                : Menu._localization.SoundMusicOff;
            items[BackIndex]   = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index == FilterIndex)
            {
                Menu.NavigateTo(Screen.AudioFilter);
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
    }
}
