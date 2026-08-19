using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class VideoPictureHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private VideoPictureHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new VideoPictureHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedVideoPictureTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.VideoPictureTitle));

    [Test]
    public void ItemCount_IsSeven() => Assert.That(_handler.ItemCount, Is.EqualTo(7));

    [TestCase(1, true)]
    [TestCase(4, true)]
    [TestCase(0, false)]
    [TestCase(5, false)]
    public void IsSliderIndex_MatchesBrightnessThroughHueOnly(int index, bool expected) =>
        Assert.That(VideoPictureHandler.IsSliderIndex(index), Is.EqualTo(expected));

    [Test]
    public void GetSliderData_BrightnessIndex_ReflectsConfigValue()
    {
        _config.VideoBrightness = 20;
        var data = _handler.GetSliderData(VideoPictureHandler.BrightnessIndex);
        Assert.That(data!.Value.Fill01, Is.EqualTo((20 + 100) / 200f).Within(0.001f));
    }

    [Test]
    public void Activate_ColorPresetIndex_CyclesToNextColorModeAndClearsPreset()
    {
        _config.VideoColorFilter = VideoColorFilterMode.None.ToString();
        var allModes = VideoColorFilterModeParser.AllModes;
        var expectedNext = allModes[(Array.IndexOf(allModes, VideoColorFilterMode.None) + 1) % allModes.Length];

        _handler.Activate(VideoPictureHandler.ColorPresetIndex);

        Assert.That(_config.VideoColorFilter, Is.EqualTo(expectedNext.ToString()));
        _host.Received(1).ClearPreset();
        _host.Received(1).OnVideoColorFilterChanged(expectedNext);
    }

    [Test]
    public void Activate_ResetIndex_CallsResetPicture()
    {
        _handler.Activate(VideoPictureHandler.ResetIndex);
        _host.Received(1).ResetPicture();
    }

    [Test]
    public void Activate_BackIndex_NavigatesToVideo()
    {
        _handler.Activate(6);
        _host.Received(1).NavigateTo(Screen.Video);
    }

    [Test]
    public void Activate_SliderIndex_IsNoOp()
    {
        _handler.Activate(VideoPictureHandler.BrightnessIndex);
        _host.DidNotReceive().ResetPicture();
        _host.DidNotReceive().NavigateTo(Arg.Any<Screen>());
        _host.DidNotReceive().ClearPreset();
    }
}
