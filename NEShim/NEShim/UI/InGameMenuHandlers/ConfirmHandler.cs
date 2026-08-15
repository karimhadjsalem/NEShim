namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class ConfirmHandler : ScreenHandler
    {
        private readonly string _title;
        private readonly string _yesItem;
        private readonly Action _onConfirm;
        private readonly bool   _isWarningStyle;

        public ConfirmHandler(InGameMenu menu, string title, string yesItem, Action onConfirm, bool isWarningStyle)
            : base(menu) { _title = title; _yesItem = yesItem; _onConfirm = onConfirm; _isWarningStyle = isWarningStyle; }

        public override string   Title     => _title;
        public override bool     IsConfirmStyle => _isWarningStyle;
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
