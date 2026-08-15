namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private abstract class ScreenHandler
    {
        protected InGameMenu Menu { get; }
        protected ScreenHandler(InGameMenu menu) => Menu = menu;
        public abstract string   Title     { get; }
        public abstract int      ItemCount { get; }
        public abstract string[] GetItems();
        public abstract void     Activate(int index);
        public virtual  bool              IsItemEnabled(int index)   => true;
        public virtual  IntPtr            GetItemIcon(int index)     => IntPtr.Zero;
        public virtual  IntPtr            GetItemValueIcon(int index) => IntPtr.Zero;
        public virtual  SliderItemData?   GetSliderData(int index)   => null;

        /// <summary>True for confirm/warning-style screens — renderer draws a warning border/title/label.</summary>
        public virtual  bool              IsConfirmStyle           => false;
        /// <summary>True for binding screens wide enough to show the NES controller diagram column.</summary>
        public virtual  bool              ShowsControllerDiagram   => false;
    }
}
