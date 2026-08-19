namespace NEShim.UI;

internal sealed class AudioEqHandler : SharedScreenHandler
{
    public const int BassIndex   = 0;
    public const int MidIndex    = 1;
    public const int TrebleIndex = 2;
    private const int ResetIndex = 3;
    // Back is at index 4.

    public static bool IsSliderIndex(int index) => index <= TrebleIndex;

    public AudioEqHandler(IMenuHost menu) : base(menu) { }

    public override string Title     => Menu.Localization.AudioEqTitle;
    public override int    ItemCount => 5;

    public override string[] GetItems() =>
    [
        Menu.Localization.AudioEqBass,
        Menu.Localization.AudioEqMid,
        Menu.Localization.AudioEqTreble,
        Menu.Localization.AudioEqReset,
        Menu.Localization.Back,
    ];

    public override SliderItemData? GetSliderData(int index) => index switch
    {
        BassIndex   => new SliderItemData(Menu.Localization.AudioEqBass,   (Menu.Config.AudioEqBass   + 12) / 24f, FormatDb(Menu.Config.AudioEqBass)),
        MidIndex    => new SliderItemData(Menu.Localization.AudioEqMid,    (Menu.Config.AudioEqMid    + 12) / 24f, FormatDb(Menu.Config.AudioEqMid)),
        TrebleIndex => new SliderItemData(Menu.Localization.AudioEqTreble, (Menu.Config.AudioEqTreble + 12) / 24f, FormatDb(Menu.Config.AudioEqTreble)),
        _           => null,
    };

    public override void Activate(int index)
    {
        if (index == ResetIndex) Menu.ResetEq();
        else if (index == 4)     Menu.NavigateTo(Screen.Sound);
    }

    private static string FormatDb(int value) => value == 0 ? "0" : value.ToString("+0;-0");
}
