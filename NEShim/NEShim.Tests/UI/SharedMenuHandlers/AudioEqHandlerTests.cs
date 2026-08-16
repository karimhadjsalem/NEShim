using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class AudioEqHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private AudioEqHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new AudioEqHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedAudioEqTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.AudioEqTitle));

    [Test]
    public void ItemCount_IsFive() => Assert.That(_handler.ItemCount, Is.EqualTo(5));

    [TestCase(0, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    public void IsSliderIndex_MatchesBassMidTrebleOnly(int index, bool expected) =>
        Assert.That(AudioEqHandler.IsSliderIndex(index), Is.EqualTo(expected));

    [Test]
    public void GetSliderData_BassIndex_ReflectsConfigValue()
    {
        _config.AudioEqBass = 6;
        var data = _handler.GetSliderData(AudioEqHandler.BassIndex);
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.Value.Fill01, Is.EqualTo((6 + 12) / 24f).Within(0.001f));
    }

    [Test]
    public void GetSliderData_ResetOrBackIndex_ReturnsNull()
    {
        Assert.That(_handler.GetSliderData(3), Is.Null);
        Assert.That(_handler.GetSliderData(4), Is.Null);
    }

    [Test]
    public void Activate_ResetIndex_CallsResetEq()
    {
        _handler.Activate(3);
        _host.Received(1).ResetEq();
    }

    [Test]
    public void Activate_BackIndex_NavigatesToSound()
    {
        _handler.Activate(4);
        _host.Received(1).NavigateTo(Screen.Sound);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Activate_SliderIndex_IsNoOp(int index)
    {
        _handler.Activate(index);
        _host.DidNotReceive().ResetEq();
        _host.DidNotReceive().NavigateTo(Arg.Any<Screen>());
    }
}
