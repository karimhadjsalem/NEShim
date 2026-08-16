using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class KeyboardBindingsHandlerTests
{
    private IMenuHost _host = null!;
    private LocalizationData _localization = null!;
    private KeyboardBindingsHandler _handler = null!;
    private static readonly (string Label, string ConfigKey)[] Actions =
    {
        ("Up", "P1 Up"),
        ("Down", "P1 Down"),
        ("Back", ""),
    };

    [SetUp]
    public void SetUp()
    {
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Localization.Returns(_localization);
        _host.BindingActions.Returns(Actions);
        // NSubstitute defaults unconfigured string-typed properties to "", not null — must set
        // this explicitly or every "not currently rebinding" test would see a false positive.
        _host.RebindingAction.Returns((string?)null);
        _handler = new KeyboardBindingsHandler(_host);
    }

    [Test]
    public void ShowsControllerDiagram_IsTrue() => Assert.That(_handler.ShowsControllerDiagram, Is.True);

    [Test]
    public void Title_NotRebinding_ReturnsUppercaseSettingsKeyboard()
    {
        Assert.That(_handler.Title, Is.EqualTo(_localization.SettingsKeyboard.ToUpper()));
    }

    [Test]
    public void Title_Rebinding_ReturnsFormattedPressKeyTitle()
    {
        _host.RebindingAction.Returns("P1 Down");
        Assert.That(_handler.Title, Is.EqualTo(string.Format(_localization.PressKeyTitle, "DOWN")));
    }

    [Test]
    public void ItemCount_MatchesBindingActionsLength()
    {
        Assert.That(_handler.ItemCount, Is.EqualTo(Actions.Length));
    }

    [Test]
    public void GetItems_BackRow_ShowsLocalizedBack()
    {
        Assert.That(_handler.GetItems()[2], Is.EqualTo(_localization.Back));
    }

    [Test]
    public void GetItems_BindingRow_IncludesLabelAndKeyboardLabel()
    {
        _host.KeyboardLabel("P1 Up").Returns("W");
        Assert.That(_handler.GetItems()[0], Is.EqualTo("Up\tW"));
    }

    [Test]
    public void Activate_BackRow_NavigatesToSettings()
    {
        _handler.Activate(2);
        _host.Received(1).NavigateTo(Screen.Settings);
    }

    [Test]
    public void Activate_BindingRow_SetsRebindingAction()
    {
        _handler.Activate(1);
        _host.Received(1).RebindingAction = "P1 Down";
    }
}
