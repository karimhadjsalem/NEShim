using NEShim.Rendering;

namespace NEShim.UI;

internal sealed class VideoMotionEffectHandler : SharedScreenHandler
{
    private static readonly VideoMotionEffectMode[] AllModes = VideoMotionEffectModeParser.AllModes;

    private int BackIndex => AllModes.Length;

    public VideoMotionEffectHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.VideoMotionEffectTitle;
    public override int    ItemCount => AllModes.Length + 1;

    public override string[] GetItems()
    {
        var current = VideoMotionEffectModeParser.Parse(Menu.Config.VideoMotionEffect);
        var items   = new string[ItemCount];
        for (int i = 0; i < AllModes.Length; i++)
        {
            var mode = AllModes[i];
            items[i] = mode == current
                ? $"✓ {MenuBindingHelpers.VideoMotionEffectDisplayName(mode, Menu.Localization)}"
                : $"  {MenuBindingHelpers.VideoMotionEffectDisplayName(mode, Menu.Localization)}";
        }
        items[BackIndex] = Menu.Localization.Back;
        return items;
    }

    public override void Activate(int index)
    {
        if (index < AllModes.Length)
        {
            var mode = AllModes[index];
            Menu.Config.VideoMotionEffect = mode.ToString();
            Menu.ClearPreset();
            Menu.OnVideoMotionEffectChanged(mode);
        }
        Menu.NavigateTo(Screen.Video);
    }
}
