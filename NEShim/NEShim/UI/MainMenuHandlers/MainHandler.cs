namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class MainHandler : ScreenHandler
    {
        private const int ResumeIndex = 1;
        public MainHandler(MainMenuScreen menu) : base(menu) { }
        public override string   Title     => Menu._localization.MainMenuTitle;
        public override int      ItemCount => 4;
        public override string[] GetItems() => new[]
        {
            Menu._localization.MainMenuNewGame,
            Menu._localization.MainMenuResumeGame,
            Menu._localization.MainMenuSettings,
            Menu._localization.MainMenuExit,
        };
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
                case 2:
                    Menu.NavigateTo(Screen.Settings);
                    break;
                case 3:
                    Menu.IsVisible = false;
                    Menu.ExitChosen?.Invoke();
                    break;
            }
        }
    }
}
