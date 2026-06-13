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
        public virtual  bool     IsItemEnabled(int index) => true;
    }
}
