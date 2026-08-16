namespace NEShim.UI;

/// <summary>
/// Common contract implemented by both the menu-specific nested <c>ScreenHandler</c> base classes
/// (<see cref="InGameMenu"/>'s and <see cref="MainMenuScreen"/>'s own, for handlers that need full
/// access to their owning menu) and <see cref="SharedScreenHandler"/> (for handlers whose logic is
/// identical between the two menus, typed against <see cref="IMenuHost"/> instead). Lets both
/// menus keep a single <c>Dictionary&lt;Screen, IScreenHandler&gt;</c> regardless of which handler
/// shape backs a given screen.
/// </summary>
internal interface IScreenHandler
{
    string   Title     { get; }
    int      ItemCount { get; }
    string[] GetItems();
    void     Activate(int index);
    bool            IsItemEnabled(int index);
    IntPtr          GetItemIcon(int index);
    IntPtr          GetItemValueIcon(int index);
    SliderItemData? GetSliderData(int index);
    bool            IsConfirmStyle { get; }
    bool            ShowsControllerDiagram { get; }
}
