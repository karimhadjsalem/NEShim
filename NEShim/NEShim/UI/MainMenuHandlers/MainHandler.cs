using NEShim.Config;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class MainHandler : ScreenHandler
    {
        private const int ResumeIndex   = 1;
        private const int SettingsIndex = 2;

        // "Change Game" only exists as a menu item in multi-game mode — the item index it
        // occupies, and the Exit index after it, shift accordingly. Mirrors InGameMenu's
        // RootHandler, which gates its own "Change Game" item the same way.
        private static bool HasChangeGame => MultiGameMode.IsActive;
        private const int ChangeGameIndex = 3;
        private int ExitIndex => HasChangeGame ? 4 : 3;

        public MainHandler(MainMenuScreen menu) : base(menu) { }
        public override string   Title     => Menu._localization.MainMenuTitle;
        public override int      ItemCount => HasChangeGame ? 5 : 4;
        public override string[] GetItems()
        {
            var resumeLabel = Menu.CanResume
                ? Menu._localization.MainMenuResumeGame
                : Menu._localization.MainMenuResumeGame + Menu._localization.SlotNoSave;
            var items = new List<string>
            {
                Menu._localization.MainMenuNewGame,
                resumeLabel,
                Menu._localization.MainMenuSettings,
            };
            if (HasChangeGame) items.Add(Menu._localization.MainMenuChangeGame);
            items.Add(Menu._localization.MainMenuExit);
            return items.ToArray();
        }
        public override bool IsItemEnabled(int index) =>
            index != ResumeIndex || Menu.CanResume;
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0:
                    Menu.IsVisible = false;
                    Menu.NewGameChosen?.Invoke();
                    break;
                case ResumeIndex:
                    Menu.BuildResumeOptions();
                    Menu.NavigateTo(Screen.ResumeSlots);
                    break;
                case SettingsIndex:
                    Menu.NavigateTo(Screen.Settings);
                    break;
                default:
                    if (HasChangeGame && index == ChangeGameIndex)
                    {
                        Menu.IsVisible = false;
                        Menu.ChangeGameChosen?.Invoke();
                    }
                    else if (index == ExitIndex)
                    {
                        Menu.IsVisible = false;
                        Menu.ExitChosen?.Invoke();
                    }
                    break;
            }
        }
    }
}
