using NEShim.Audio;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class SoundHandler : ScreenHandler
    {
        public  const int VolumeIndex = 0;
        private const int FilterIndex = 1;
        private const int BackIndex   = 2;

        public SoundHandler(InGameMenu menu) : base(menu) { }

        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 3;

        public override string[] GetItems()
        {
            var mode  = AudioFilterModeParser.Parse(Menu._config.AudioFilter);
            var items = new string[3];
            items[VolumeIndex] = string.Format(Menu._localization.SoundVolume, Menu._config.Volume);
            items[FilterIndex] = $"{Menu._localization.AudioFilterLabel}: {AudioFilterModeParser.DisplayName(mode)}";
            items[BackIndex]   = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index == FilterIndex)
                Menu.NavigateTo(Screen.AudioFilter);
            else if (index == BackIndex)
                Menu.NavigateTo(Screen.Settings);
        }
    }
}
