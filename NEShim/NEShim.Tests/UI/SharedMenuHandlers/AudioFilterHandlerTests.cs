using NEShim.Audio;
using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class AudioFilterHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private AudioFilterHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new AudioFilterHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedAudioFilterTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.AudioFilterTitle));

    [Test]
    public void ItemCount_IsFilterCountPlusBack()
    {
        int filterCount = Enum.GetValues<AudioFilterMode>().Length;
        Assert.That(_handler.ItemCount, Is.EqualTo(filterCount + 1));
    }

    [Test]
    public void GetItems_CurrentFilter_IsChecked()
    {
        _config.AudioFilter = AudioFilterMode.Warm.ToString();
        int idx = Array.IndexOf(Enum.GetValues<AudioFilterMode>(), AudioFilterMode.Warm);

        Assert.That(_handler.GetItems()[idx], Does.StartWith("✓"));
    }

    [Test]
    public void GetItems_LastRow_IsBack()
    {
        Assert.That(_handler.GetItems()[^1], Is.EqualTo(_localization.Back));
    }

    [Test]
    public void Activate_FilterRow_UpdatesConfigAndFiresCallbackAndNavigates()
    {
        _handler.Activate(0);

        Assert.That(_config.AudioFilter, Is.EqualTo(AudioFilterMode.Default.ToString()));
        _host.Received(1).OnFilterChanged(AudioFilterMode.Default);
        _host.Received(1).NavigateTo(Screen.Sound);
    }

    [Test]
    public void Activate_BackRow_NavigatesWithoutChangingConfig()
    {
        string original = _config.AudioFilter;
        int backIndex = _handler.ItemCount - 1;

        _handler.Activate(backIndex);

        Assert.That(_config.AudioFilter, Is.EqualTo(original));
        _host.Received(1).NavigateTo(Screen.Sound);
        _host.DidNotReceive().OnFilterChanged(Arg.Any<AudioFilterMode>());
    }
}
