namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class RootHandler : ScreenHandler
    {
        public RootHandler(InGameMenu menu) : base(menu) { }
        public override string Title     => Menu._localization.InGamePausedTitle;
        public override int    ItemCount => 8;
        public override string[] GetItems() => new[]
        {
            Menu._localization.InGameResume,
            Menu._localization.InGameResetGame,
            Menu._localization.InGameSelectSaveSlot,
            Menu._localization.InGameSaveGame,
            Menu._localization.InGameLoadGame,
            Menu._localization.InGameSettings,
            Menu._localization.InGameReturnToMain,
            Menu._localization.InGameExit,
        };
        public override bool IsItemEnabled(int index) =>
            index != RootItemLoadGame || Menu._saveStates.SlotExists(Menu._saveStates.ActiveSlot);
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0:                    Menu.Close(); break;
                case 1:                    Menu._onResetGame(); Menu.Close(); break;
                case 2:                    Menu.NavigateTo(Screen.SaveSlotSelect); break;
                case 3:                    Menu._saveStates.SaveToActiveSlot(); Menu.Close(); break;
                case RootItemLoadGame:     Menu.NavigateTo(Screen.ConfirmLoad); break;
                case 5:                    Menu.NavigateTo(Screen.Settings); break;
                case RootItemReturnToMain:
                    Menu.NavigateTo(Screen.ConfirmMainMenu);
                    Menu.SelectedItem = 1;
                    break;
                case 7:
                    Menu.NavigateTo(Screen.ConfirmExit);
                    Menu.SelectedItem = 1;
                    break;
            }
        }
    }
}
