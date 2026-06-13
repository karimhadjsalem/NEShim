using System.Linq;

namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class ResumeSlotsHandler : ScreenHandler
    {
        public ResumeSlotsHandler(MainMenuScreen menu) : base(menu) { }
        public override string   Title     => Menu._localization.MainMenuLoadTitle;
        public override int      ItemCount => Menu._resumeOptions.Length;
        public override string[] GetItems() => Menu._resumeOptions.Select(o => o.Label).ToArray();
        public override void Activate(int index)
        {
            var opt = Menu._resumeOptions[index];
            if (opt.Load == null)
                Menu.NavigateTo(Screen.Main);
            else
            {
                opt.Load();
                Menu.IsVisible = false;
                Menu.ResumeChosen?.Invoke();
            }
        }
    }
}
