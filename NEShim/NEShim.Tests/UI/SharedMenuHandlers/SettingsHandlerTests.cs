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

    // ── PlayerCount > 1: "Keyboard Controls"/"Gamepad Controls" rows replaced by a single
    // "Player Controls" row (player 1 is listed there too — see PlayerSelectHandler — so showing
    // player 1's bindings both here and in that submenu would be redundant) ────────────────────

    [Test]
    public void ItemCount_PlayerCountAboveOne_IsSix()
    {
        _config.PlayerCount = 2;
        Assert.That(_handler.ItemCount, Is.EqualTo(6));
    }

    [Test]
    public void GetItems_PlayerCountAboveOne_DoesNotIncludeKeyboardOrGamepadRows()
    {
        _config.PlayerCount = 2;
        var items = _handler.GetItems();
        Assert.That(items, Does.Not.Contain(_localization.SettingsKeyboard));
        Assert.That(items, Does.Not.Contain(_localization.SettingsGamepad));
    }

    [Test]
    public void GetItems_PlayerCountAboveOne_IncludesPlayerControlsAtIndex2()
    {
        _config.PlayerCount = 2;
        Assert.That(_handler.GetItems()[2], Is.EqualTo(_localization.SettingsPlayerControls));
    }

    [Test]
    public void Activate_PlayerCountAboveOne_Index2_NavigatesToPlayerSelect()
    {
        _config.PlayerCount = 2;
        _handler.Activate(2);
        _host.Received(1).NavigateTo(Screen.PlayerSelect);
    }

    [TestCase(2)] [TestCase(3)]
    public void Activate_PlayerCountAboveOne_NeverNavigatesToPlayer1BindingsDirectly(int index)
    {
        // With PlayerCount > 1, indices 2/3 belong to "Player Controls"/D-pad-stick toggle, not
        // to KeyboardBindings/GamepadBindings — those are only reachable via Player Controls now.
        _config.PlayerCount = 2;
        _handler.Activate(index);
        _host.DidNotReceive().NavigateTo(Screen.KeyboardBindings);
        _host.DidNotReceive().NavigateTo(Screen.GamepadBindings);
    }

    [Test]
    public void Activate_PlayerCountAboveOne_ShiftedIndices_StillNavigateCorrectly()
    {
        _config.PlayerCount = 2;
        _host.RootScreen.Returns(Screen.Main);

        _handler.Activate(3); // D-pad/stick toggle, was index 4 when PlayerCount==1
        Assert.That(_config.GamepadDpadStickInterchangeable, Is.False);

        _handler.Activate(4); // Language, was index 5
        _host.Received(1).NavigateTo(Screen.Language);

        _handler.Activate(5); // Back, was index 6
        _host.Received(1).NavigateTo(Screen.Main);
    }

    [Test]
    public void Activate_PlayerCountAboveOne_VideoAndSoundRows_Unaffected()
    {
        _config.PlayerCount = 3;
        _handler.Activate(0);
        _host.Received(1).NavigateTo(Screen.Video);

        _handler.Activate(1);
        _host.Received(1).NavigateTo(Screen.Sound);
    }
}
