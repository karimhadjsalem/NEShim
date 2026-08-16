using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class VideoMotionEffectHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private VideoMotionEffectHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new VideoMotionEffectHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedVideoMotionEffectTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.VideoMotionEffectTitle));

    [Test]
    public void ItemCount_IsModeCountPlusBack() =>
        Assert.That(_handler.ItemCount, Is.EqualTo(VideoMotionEffectModeParser.AllModes.Length + 1));

    [Test]
    public void GetItems_CurrentMode_IsChecked()
    {
        _config.VideoMotionEffect = VideoMotionEffectMode.CrtJitter.ToString();
        int idx = Array.IndexOf(VideoMotionEffectModeParser.AllModes, VideoMotionEffectMode.CrtJitter);

        Assert.That(_handler.GetItems()[idx], Does.StartWith("✓"));
    }

    [Test]
    public void Activate_SelectsMode_ClearsPresetAndFiresCallback()
    {
        var mode = VideoMotionEffectModeParser.AllModes[1];
        int idx = Array.IndexOf(VideoMotionEffectModeParser.AllModes, mode);

        _handler.Activate(idx);

        Assert.That(_config.VideoMotionEffect, Is.EqualTo(mode.ToString()));
        _host.Received(1).ClearPreset();
        _host.Received(1).OnVideoMotionEffectChanged(mode);
        _host.Received(1).NavigateTo(Screen.Video);
    }

    [Test]
    public void Activate_BackRow_NavigatesWithoutChangingMode()
    {
        string original = _config.VideoMotionEffect;
        int backIndex = _handler.ItemCount - 1;

        _handler.Activate(backIndex);

        Assert.That(_config.VideoMotionEffect, Is.EqualTo(original));
        _host.Received(1).NavigateTo(Screen.Video);
        _host.DidNotReceive().OnVideoMotionEffectChanged(Arg.Any<VideoMotionEffectMode>());
    }
}
