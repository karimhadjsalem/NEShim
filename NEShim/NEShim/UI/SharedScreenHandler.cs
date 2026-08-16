namespace NEShim.UI;

/// <summary>
/// Base class for the ~11 screen handlers whose logic is identical between <see cref="InGameMenu"/>
/// and <see cref="MainMenuScreen"/> — typed against <see cref="IMenuHost"/> instead of either
/// concrete menu, so one implementation serves both. Mirrors the shape of each menu's own nested
/// <c>ScreenHandler</c> base class (used for the handlers that genuinely differ between the two
/// menus and need full access to their concrete owner), unified behind <see cref="IScreenHandler"/>
/// so both kinds coexist in one <c>Dictionary&lt;Screen, IScreenHandler&gt;</c> per menu.
/// </summary>
internal abstract class SharedScreenHandler : IScreenHandler
{
    protected IMenuHost Menu { get; }
    protected SharedScreenHandler(IMenuHost menu) => Menu = menu;

    public abstract string   Title     { get; }
    public abstract int      ItemCount { get; }
    public abstract string[] GetItems();
    public abstract void     Activate(int index);

    public virtual bool            IsItemEnabled(int index)    => true;
    public virtual IntPtr          GetItemIcon(int index)      => IntPtr.Zero;
    public virtual IntPtr          GetItemValueIcon(int index) => IntPtr.Zero;
    public virtual SliderItemData? GetSliderData(int index)    => null;
    public virtual bool            IsConfirmStyle              => false;
    public virtual bool            ShowsControllerDiagram      => false;
}
