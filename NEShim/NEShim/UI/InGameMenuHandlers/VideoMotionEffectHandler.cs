using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class VideoMotionEffectHandler : ScreenHandler
    {
        private static readonly VideoMotionEffectMode[] AllModes = VideoMotionEffectModeParser.AllModes;

        private int BackIndex => AllModes.Length;

        public VideoMotionEffectHandler(InGameMenu menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoMotionEffectTitle;
        public override int    ItemCount => AllModes.Length + 1;

        public override string[] GetItems()
        {
            var current = VideoMotionEffectModeParser.Parse(Menu._config.VideoMotionEffect);
            var items   = new string[ItemCount];
            for (int i = 0; i < AllModes.Length; i++)
            {
                var mode = AllModes[i];
                items[i] = mode == current
                    ? $"✓ {MotionDisplayName(mode)}"
                    : $"  {MotionDisplayName(mode)}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index < AllModes.Length)
            {
                var mode = AllModes[index];
                Menu._config.VideoMotionEffect = mode.ToString();
                Menu._onVideoMotionEffectChanged(mode);
            }
            Menu.NavigateTo(Screen.Video);
        }

        private string MotionDisplayName(VideoMotionEffectMode mode) => mode switch
        {
            VideoMotionEffectMode.None      => Menu._localization.VideoMotionEffectNone,
            VideoMotionEffectMode.CrtJitter   => Menu._localization.VideoMotionEffectCrtJitter,
            VideoMotionEffectMode.ScanlineBob => Menu._localization.VideoMotionEffectScanlineBob,
            _                                 => mode.ToString(),
        };
    }
}
