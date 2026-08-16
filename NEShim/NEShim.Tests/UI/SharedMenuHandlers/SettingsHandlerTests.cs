using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class SettingsHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private SettingsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new SettingsHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedSettingsTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.SettingsTitle));

    [Test]
    public void ItemCount_IsSeven() => Assert.That(_handler.ItemCount, Is.EqualTo(7));

    [TestCase(true,  "DpadStickInterchangeableOn")]
    [TestCase(false, "DpadStickInterchangeableOff")]
    public void GetItems_DpadStickRow_ReflectsConfigState(bool linked, string propertyName)
    {
        _config.GamepadDpadStickInterchangeable = linked;
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(_localization)!;
        Assert.That(_handler.GetItems()[4], Is.EqualTo(expected));
    }

    [Test]
    public void GetItems_LanguageRow_ShowsAutoWhenLanguageIsAuto()
    {
        _config.Language = "Auto";
        Assert.That(_handler.GetItems()[5], Does.Contain(_localization.LanguageAuto));
    }

    [TestCase(0, Screen.Video)]
    [TestCase(1, Screen.Sound)]
    [TestCase(2, Screen.KeyboardBindings)]
    [TestCase(3, Screen.GamepadBindings)]
    [TestCase(5, Screen.Language)]
    public void Activate_NavigatesToExpectedScreen(int index, Screen expected)
    {
        _handler.Activate(index);
        _host.Received(1).NavigateTo(expected);
    }

    [Test]
    public void Activate_DpadStickRow_TogglesConfigAndSaves()
    {
        _config.GamepadDpadStickInterchangeable = true;
        _handler.Activate(4);

        Assert.That(_config.GamepadDpadStickInterchangeable, Is.False);
        _host.Received(1).OnConfigSaved();
    }

    [Test]
    public void Activate_BackRow_NavigatesToHostsRootScreen()
    {
        _host.RootScreen.Returns(Screen.Main);
        _handler.Activate(6);
        _host.Received(1).NavigateTo(Screen.Main);
    }
}
