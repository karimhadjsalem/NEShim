namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class SoundHandler : ScreenHandler
    {
        public  const int VolumeIndex   = 0;
        private const int ScrubberIndex = 1;
        private const int BackIndex     = 2;
        public SoundHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => Menu._localization.SoundTitle;
        public override int      ItemCount => 3;
        public override string[] GetItems() => new[]
        {
            string.Format(Menu._localization.SoundVolume, Menu._config.Volume),
            Menu._config.SoundScrubberEnabled ? Menu._localization.SoundScrubberOn : Menu._localization.SoundScrubberOff,
            Menu._localization.Back,
        };
        public override void Activate(int index)
        {
            switch (index)
            {
                case ScrubberIndex:
                    bool scrubOn = !Menu._config.SoundScrubberEnabled;
                    Menu._config.SoundScrubberEnabled = scrubOn;
                    Menu._onScrubberToggled(scrubOn);
                    break;
                case BackIndex:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }
    }
}
