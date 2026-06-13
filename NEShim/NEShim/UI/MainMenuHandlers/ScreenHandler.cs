namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private abstract class ScreenHandler
    {
        protected MainMenuScreen Menu { get; }
        protected ScreenHandler(MainMenuScreen menu) => Menu = menu;
        public abstract string   Title     { get; }
        public abstract int      ItemCount { get; }
        public abstract string[] GetItems();
        public abstract void     Activate(int index);
        public virtual  bool     IsItemEnabled(int index) => true;
    }
}
