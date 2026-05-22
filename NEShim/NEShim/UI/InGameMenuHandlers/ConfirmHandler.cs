namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class ConfirmHandler : ScreenHandler
    {
        private readonly string _title;
        private readonly string _yesItem;
        private readonly Action _onConfirm;

        public ConfirmHandler(InGameMenu menu, string title, string yesItem, Action onConfirm)
            : base(menu) { _title = title; _yesItem = yesItem; _onConfirm = onConfirm; }

        public override string   Title     => _title;
        public override int      ItemCount => 2;
        public override string[] GetItems() =>
            new[] { _yesItem, Menu._localization.InGameConfirmNoStay };
        public override void Activate(int index)
        {
            if (index == 0) _onConfirm();
            else Menu.NavigateTo(Screen.Root);
        }
    }
}
