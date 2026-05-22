namespace NEShim.UI;

internal sealed partial class MainMenuScreen
{
    private sealed class VideoHandler : ScreenHandler
    {
        public VideoHandler(MainMenuScreen menu) : base(menu) { }
        public override string   Title     => Menu._localization.VideoTitle;
        public override int      ItemCount => 4;
        public override string[] GetItems() => new[]
        {
            Menu._config.WindowMode == "Fullscreen" ? Menu._localization.VideoWindowFullscreen : Menu._localization.VideoWindowWindowed,
            Menu._config.GraphicsSmoothingEnabled   ? Menu._localization.VideoGraphicsSmooth   : Menu._localization.VideoGraphicsOriginal,
            Menu._config.ShowFps                    ? Menu._localization.VideoFpsOn            : Menu._localization.VideoFpsOff,
            Menu._localization.Back,
        };
        public override void Activate(int index)
        {
            switch (index)
            {
                case 0:
                    Menu._onWindowModeToggle(Menu._config.WindowMode != "Fullscreen");
                    break;
                case 1:
                    bool smoothOn = !Menu._config.GraphicsSmoothingEnabled;
                    Menu._config.GraphicsSmoothingEnabled = smoothOn;
                    Menu._onGraphicsScalerToggled(smoothOn);
                    break;
                case 2:
                    Menu._config.ShowFps = !Menu._config.ShowFps;
                    Menu._onConfigSaved();
                    break;
                case 3:
                    Menu.NavigateTo(Screen.Settings);
                    break;
            }
        }
    }
}
