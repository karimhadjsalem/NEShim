using NEShim.Rendering;

namespace NEShim.UI;

internal sealed class VideoPresetsHandler : SharedScreenHandler
{
    private static readonly VideoPreset[] Presets = VideoPresetRegistry.All;

    // None(0), Presets(1..N), Back(N+1)
    private int BackIndex => Presets.Length + 1;

    public VideoPresetsHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.VideoPresetsTitle;
    public override int    ItemCount => Presets.Length + 2;

    public override string[] GetItems()
    {
        string active = Menu.Config.VideoPreset;
        var items = new string[ItemCount];
        items[0] = active == "None"
            ? $"✓ {Menu.Localization.VideoPresetNoPreset}"
            : $"  {Menu.Localization.VideoPresetNoPreset}";
        for (int i = 0; i < Presets.Length; i++)
        {
            string name = PresetName(i);
            items[i + 1] = Presets[i].Name == active
                ? $"✓ {name}"
                : $"  {name}";
        }
        items[BackIndex] = Menu.Localization.Back;
        return items;
    }

    public override void Activate(int index)
    {
        if (index == 0)
        {
            Menu.Config.VideoPreset = "None";
            Menu.OnConfigSaved();
        }
        else if (index < BackIndex)
        {
            Menu.ApplyPreset(Presets[index - 1]);
        }
        Menu.NavigateTo(Screen.Video);
    }

    private string PresetName(int index) => index switch
    {
        0 => Menu.Localization.VideoPresetNoFilters,
        1 => Menu.Localization.VideoPresetLivingRoom,
        2 => Menu.Localization.VideoPresetArcade,
        3 => Menu.Localization.VideoPresetSharp,
        4 => Menu.Localization.VideoPresetPhosphor,
        _ => "?",
    };
}
