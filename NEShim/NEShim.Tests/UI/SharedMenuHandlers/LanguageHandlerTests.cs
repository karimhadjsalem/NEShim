using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class LanguageHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private LanguageHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new LanguageHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedLanguageTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.LanguageTitle));

    [Test]
    public void ItemCount_IsLanguageCountPlusAutoPlusBack() =>
        Assert.That(_handler.ItemCount, Is.EqualTo(LanguageRegistry.AllLanguages.Count + 2));

    [Test]
    public void GetItems_AutoSelected_ChecksmarksAutoRow()
    {
        _config.Language = "Auto";
        Assert.That(_handler.GetItems()[0], Does.StartWith("✓"));
    }

    [Test]
    public void GetItems_SpecificLanguageSelected_ChecksmarksThatRowNotAuto()
    {
        var lang = LanguageRegistry.AllLanguages[0];
        _config.Language = lang.Code;

        var items = _handler.GetItems();

        Assert.That(items[0], Does.Not.StartWith("✓"));
        Assert.That(items[1], Does.StartWith("✓"));
    }

    [Test]
    public void GetItems_LastRow_IsBack()
    {
        var items = _handler.GetItems();
        Assert.That(items[^1], Is.EqualTo(_localization.Back));
    }

    [Test]
    public void Activate_AutoRow_SetsLanguageToAutoAndNotifies()
    {
        _handler.Activate(0);

        Assert.That(_config.Language, Is.EqualTo("Auto"));
        _host.Received(1).OnLanguageChanged("Auto");
        _host.Received(1).NavigateTo(Screen.Settings);
    }

    [Test]
    public void Activate_LanguageRow_SetsLanguageCodeAndNotifies()
    {
        var lang = LanguageRegistry.AllLanguages[0];
        _handler.Activate(1);

        Assert.That(_config.Language, Is.EqualTo(lang.Code));
        _host.Received(1).OnLanguageChanged(lang.Code);
        _host.Received(1).NavigateTo(Screen.Settings);
    }

    [Test]
    public void Activate_BackRow_DoesNotChangeLanguage_ButNavigatesToSettings()
    {
        _config.Language = "Auto";
        int backIndex = _handler.ItemCount - 1;

        _handler.Activate(backIndex);

        Assert.That(_config.Language, Is.EqualTo("Auto"));
        _host.Received(1).NavigateTo(Screen.Settings);
        _host.DidNotReceive().OnLanguageChanged(Arg.Any<string>());
    }

    [Test]
    public void GetItemIcon_AutoRow_ReturnsZero() =>
        Assert.That(_handler.GetItemIcon(0), Is.EqualTo(IntPtr.Zero));

    [Test]
    public void GetItemIcon_BackRow_ReturnsZero()
    {
        int backIndex = _handler.ItemCount - 1;
        Assert.That(_handler.GetItemIcon(backIndex), Is.EqualTo(IntPtr.Zero));
    }
}
