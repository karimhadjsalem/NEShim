namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class ControllerDisconnectedHandler : ScreenHandler
    {
        public ControllerDisconnectedHandler(InGameMenu menu) : base(menu) { }
        public override string   Title     => "";
        public override int      ItemCount => 0;
        public override string[] GetItems() => Array.Empty<string>();
        public override void     Activate(int index) { }
    }
}
