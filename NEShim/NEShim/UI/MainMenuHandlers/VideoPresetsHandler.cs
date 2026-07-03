using NEShim.Rendering;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class VideoPresetsHandler : ScreenHandler
    {
        private static readonly VideoPreset[] Presets = VideoPresetRegistry.All;

        // None(0), Presets(1..N), Back(N+1)
        private int BackIndex => Presets.Length + 1;

        public VideoPresetsHandler(MainMenuScreen menu) : base(menu) { }

        public override string Title     => Menu._localization.VideoPresetsTitle;
        public override int    ItemCount => Presets.Length + 2;

        public override string[] GetItems()
        {
            string active = Menu._config.VideoPreset;
            var items = new string[ItemCount];
            items[0] = active == "None"
                ? $"✓ {Menu._localization.VideoColorFilterNone}"
                : $"  {Menu._localization.VideoColorFilterNone}";
            for (int i = 0; i < Presets.Length; i++)
            {
                string name = PresetName(i);
                items[i + 1] = Presets[i].Name == active
                    ? $"✓ {name}"
                    : $"  {name}";
            }
            items[BackIndex] = Menu._localization.Back;
            return items;
        }

        public override void Activate(int index)
        {
            if (index == 0)
            {
                Menu._config.VideoPreset = "None";
                Menu._onConfigSaved();
            }
            else if (index < BackIndex)
            {
                Menu.ApplyPreset(Presets[index - 1]);
            }
            Menu.NavigateTo(Screen.Video);
        }

        private string PresetName(int index) => index switch
        {
            0 => Menu._localization.VideoPresetLivingRoom,
            1 => Menu._localization.VideoPresetArcade,
            2 => Menu._localization.VideoPresetSharp,
            3 => Menu._localization.VideoPresetPhosphor,
            _ => "?",
        };
    }
}
