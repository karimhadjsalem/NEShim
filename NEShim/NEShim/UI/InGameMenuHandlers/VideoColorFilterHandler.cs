using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class VideoColorFilterHandler : ScreenHandler
    {
        private static readonly VideoColorFilterMode[] AllModes = VideoColorFilterModeParser.AllModes;

        private int BackIndex => AllModes.Length;

        public VideoColorFilterHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoColorFilterTitle;
        public override int    ItemCount => AllModes.Length + 1;

        public override string[] GetItems()
        {
            var current = VideoColorFilterModeParser.Parse(Menu._config.VideoColorFilter);
            var items   = new string[ItemCount];
            for (int i = 0; i < AllModes.Length; i++)
            {
                var mode = AllModes[i];
                items[i] = mode == current
                    ? $"✓ {ColorDisplayName(mode)}"
                    : $"  {ColorDisplayName(mode)}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index < AllModes.Length)
            {
                var mode = AllModes[index];
                Menu._config.VideoColorFilter = mode.ToString();
                Menu._onVideoColorFilterChanged(mode);
            }
            Menu.NavigateTo(Screen.Video);
        }

        private string ColorDisplayName(VideoColorFilterMode mode) => mode switch
        {
            VideoColorFilterMode.None               => Menu._localization.VideoColorFilterNone,
            VideoColorFilterMode.Warm               => Menu._localization.VideoColorFilterWarm,
            VideoColorFilterMode.Greyscale          => Menu._localization.VideoColorFilterGreyscale,
            VideoColorFilterMode.NesColorCorrection => Menu._localization.VideoColorFilterNesColors,
            _                                       => mode.ToString(),
        };
    }
}
