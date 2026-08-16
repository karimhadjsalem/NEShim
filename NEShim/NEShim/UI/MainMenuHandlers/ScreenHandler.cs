namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private abstract class ScreenHandler : IScreenHandler
    {
        protected MainMenuScreen Menu { get; }
        protected ScreenHandler(MainMenuScreen menu) => Menu = menu;
        public abstract string   Title     { get; }
        public abstract int      ItemCount { get; }
        public abstract string[] GetItems();
        public abstract void     Activate(int index);
        public virtual  bool              IsItemEnabled(int index)   => true;
        public virtual  IntPtr            GetItemIcon(int index)     => IntPtr.Zero;
        public virtual  IntPtr            GetItemValueIcon(int index) => IntPtr.Zero;
        public virtual  SliderItemData?   GetSliderData(int index)   => null;

        /// <summary>Always false — MainMenuScreen has no confirm-style screens. Present only to
        /// satisfy IScreenHandler, shared with InGameMenu's ScreenHandler (which does use it).</summary>
        public virtual  bool              IsConfirmStyle           => false;
        /// <summary>True for binding screens wide enough to show the NES controller diagram column.</summary>
        public virtual  bool              ShowsControllerDiagram   => false;
    }
}
