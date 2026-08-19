using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

/// <summary>
/// The handler builds its own action table from Menu.Localization (via
/// MenuBindingHelpers.BuildBindingActions) rather than reading it off IMenuHost, so these tests
/// exercise the real 9-row (8 buttons + Back) default table instead of a hand-rolled one.
/// </summary>
[TestFixture]
internal class KeyboardBindingsHandlerTests
{
    private const int BackIndex = 8; // Up,Down,Left,Right,A,B,Start,Select,Back

    private IMenuHost _host = null!;
    private LocalizationData _localization = null!;
    private KeyboardBindingsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Localization.Returns(_localization);
        _host.Config.Returns(new AppConfig()); // default PlayerCount=1 — Back targets Settings
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
    public void ItemCount_IsEightButtonsPlusBack()
    {
        Assert.That(_handler.ItemCount, Is.EqualTo(BackIndex + 1));
    }

    [Test]
    public void GetItems_BackRow_ShowsLocalizedBack()
    {
        Assert.That(_handler.GetItems()[BackIndex], Is.EqualTo(_localization.Back));
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
        _handler.Activate(BackIndex);
        _host.Received(1).NavigateTo(Screen.Settings);
    }

    [Test]
    public void Activate_BindingRow_SetsRebindingAction()
    {
        _handler.Activate(1);
        _host.Received(1).RebindingAction = "P1 Down";
    }

    [Test]
    public void GetActiveNesButton_BindingRow_ReturnsConfigKey()
    {
        Assert.That(_handler.GetActiveNesButton(1), Is.EqualTo("P1 Down"));
    }

    [Test]
    public void GetActiveNesButton_BackRow_ReturnsNull()
    {
        Assert.That(_handler.GetActiveNesButton(BackIndex), Is.Null);
    }

    // ── Player 2 ──────────────────────────────────────────────────────────────────

    [Test]
    public void Player2_ActionsUsePlayer2ConfigKeys()
    {
        var handler = new KeyboardBindingsHandler(_host, player: 2);
        Assert.That(handler.GetActiveNesButton(0), Is.EqualTo("P2 Up"));
    }

    [Test]
    public void Player2_BackRow_NavigatesToPlayerSelect()
    {
        var handler = new KeyboardBindingsHandler(_host, player: 2);
        handler.Activate(BackIndex);
        _host.Received(1).NavigateTo(Screen.PlayerSelect);
    }

    // ── Player 1, multiplayer enabled: Back follows the other players to PlayerSelect ──
    // once that submenu exists (PlayerCount > 1) — see PlayerSelectHandler, which now lists
    // player 1 alongside 2-4.

    [Test]
    public void Player1_PlayerCountAboveOne_BackRow_NavigatesToPlayerSelect()
    {
        _host.Config.Returns(new AppConfig { PlayerCount = 2 });
        var handler = new KeyboardBindingsHandler(_host);
        handler.Activate(BackIndex);
        _host.Received(1).NavigateTo(Screen.PlayerSelect);
    }
}
