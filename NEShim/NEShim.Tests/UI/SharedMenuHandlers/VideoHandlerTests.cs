using NEShim.Config;
using NEShim.Localization;
using NEShim.Platform;
using NEShim.Rendering;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

/// <summary>
/// ItemCount/Activate's index layout depends on PlatformDetector.SupportsAdvancedVideoFeatures
/// (IsD3D11Active || IsSdlGpuRendererActive) — reset to false in SetUp, matching the convention
/// already established in InGameMenuTests.cs/MainMenuScreenTests.cs; tests that need the extended
/// feature set opt in explicitly within their own body.
/// </summary>
[TestFixture]
internal class VideoHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private VideoHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        PlatformDetector.SetD3D11Active(false);
        PlatformDetector.SetSdlGpuRendererActive(false);

        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new VideoHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedVideoTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.VideoTitle));

    [Test]
    public void ItemCount_BasicRenderer_IsFive() =>
        Assert.That(_handler.ItemCount, Is.EqualTo(5));

    [Test]
    public void ItemCount_AdvancedRenderer_IsNine()
    {
        PlatformDetector.SetD3D11Active(true);
        Assert.That(_handler.ItemCount, Is.EqualTo(9));
    }

    [Test]
    public void GetItems_BasicRenderer_OmitsPresetsOverlayMotionPicture()
    {
        var items = _handler.GetItems();
        Assert.That(items, Has.Length.EqualTo(5));
        Assert.That(items[^1], Is.EqualTo(_localization.Back));
    }

    [Test]
    public void GetItems_WindowModeReflectsConfig()
    {
        _config.WindowMode = "Fullscreen";
        Assert.That(_handler.GetItems()[0], Is.EqualTo(_localization.VideoWindowFullscreen));

        _config.WindowMode = "Windowed";
        Assert.That(_handler.GetItems()[0], Is.EqualTo(_localization.VideoWindowWindowed));
    }

    [Test]
    public void Activate_BasicRenderer_WindowModeToggle_TogglesFullscreen()
    {
        // Basic layout: [window=0, filter=1, overscan=2, fps=3, back=4].
        _config.WindowMode = "Windowed";
        _handler.Activate(0);

        _host.Received(1).OnWindowModeToggle(true);
    }

    [Test]
    public void Activate_BasicRenderer_FpsRow_TogglesShowFpsAndSaves()
    {
        bool original = _config.ShowFps;
        _handler.Activate(3); // basic layout: [window, filter, overscan, fps, back]

        Assert.That(_config.ShowFps, Is.EqualTo(!original));
        _host.Received(1).OnConfigSaved();
    }

    [Test]
    public void Activate_BasicRenderer_BackRow_NavigatesToSettings()
    {
        _handler.Activate(4);
        _host.Received(1).NavigateTo(Screen.Settings);
    }

    [Test]
    public void Activate_AdvancedRenderer_PresetsRow_NavigatesToVideoPresets()
    {
        PlatformDetector.SetD3D11Active(true);
        _handler.Activate(0);
        _host.Received(1).NavigateTo(Screen.VideoPresets);
    }

    [Test]
    public void Activate_AdvancedRenderer_OverscanRow_CyclesOverscanAndClearsPreset()
    {
        PlatformDetector.SetD3D11Active(true);
        _config.OverscanMode = OverscanMode.Overscan.ToString();

        _handler.Activate(6); // advanced layout: [presets,window,filter,overlay,motion,picture,overscan,fps,back]

        Assert.That(_config.OverscanMode, Is.EqualTo(OverscanMode.Normal.ToString()));
        _host.Received(1).ClearPreset();
        _host.Received(1).OnOverscanModeChanged(OverscanMode.Normal);
    }

    [Test]
    public void Activate_AdvancedRenderer_MotionEffectRow_NavigatesToVideoMotionEffect()
    {
        PlatformDetector.SetD3D11Active(true);
        _handler.Activate(4);
        _host.Received(1).NavigateTo(Screen.VideoMotionEffect);
    }

    [Test]
    public void Activate_AdvancedRenderer_PictureRow_NavigatesToVideoPicture()
    {
        PlatformDetector.SetD3D11Active(true);
        _handler.Activate(5);
        _host.Received(1).NavigateTo(Screen.VideoPicture);
    }

    [Test]
    public void Activate_AdvancedRenderer_OverlayRow_CyclesOverlayAndClearsPreset()
    {
        PlatformDetector.SetD3D11Active(true);
        _config.VideoFilterOverlay = "None";
        _config.VideoFilter = VideoFilterMode.PixelPerfect.ToString();

        _handler.Activate(3);

        Assert.That(_config.VideoFilterOverlay, Is.EqualTo(VideoFilterMode.CrtScanlines.ToString()));
        _host.Received(1).ClearPreset();
        _host.Received(1).OnVideoFilterOverlayChanged(VideoFilterMode.CrtScanlines);
    }
}
