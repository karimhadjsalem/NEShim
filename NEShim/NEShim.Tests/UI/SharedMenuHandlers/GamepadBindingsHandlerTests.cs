using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

/// <summary>
/// Isolated tests using a mocked <see cref="IMenuHost"/> — previously this handler could only be
/// exercised indirectly by driving full menu navigation through InGameMenu/MainMenuScreen.
/// IsItemEnabled's native-Steam-mode branch is not exercised here: SteamInputManager.IsUsingNativeActions()
/// is a static call that's always false in the test process (no live Steam session), matching the
/// existing "Steam unavailable" test convention used throughout this codebase.
///
/// The handler builds its own action table from Menu.Localization/Menu.Config (via
/// MenuBindingHelpers.BuildGamepadBindingActions) rather than reading it off IMenuHost, so these
/// tests exercise the real 9-row (8 buttons + Back) default table instead of a hand-rolled one.
/// </summary>
[TestFixture]
internal class GamepadBindingsHandlerTests
{
    private const int BackIndex = 8; // Up,Down,Left,Right,A,B,Start,Select,Back

    private IMenuHost _host = null!;
    private LocalizationData _localization = null!;
    private GamepadBindingsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Localization.Returns(_localization);
        _host.Config.Returns(new AppConfig());
        // NSubstitute defaults unconfigured string-typed properties to "", not null — must set
        // this explicitly or every "not currently rebinding" test would see a false positive.
        _host.GamepadRebindingAction.Returns((string?)null);
        _handler = new GamepadBindingsHandler(_host);
    }

    [Test]
    public void ShowsControllerDiagram_IsTrue() => Assert.That(_handler.ShowsControllerDiagram, Is.True);

    [Test]
    public void Title_NotRebinding_ReturnsUppercaseSettingsGamepad()
    {
        Assert.That(_handler.Title, Is.EqualTo(_localization.SettingsGamepad.ToUpper()));
    }

    [Test]
    public void Title_Rebinding_ReturnsFormattedPressButtonTitle()
    {
        _host.GamepadRebindingAction.Returns("P1 Down");
        Assert.That(_handler.Title, Is.EqualTo(string.Format(_localization.PressButtonTitle, "DOWN")));
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
    public void GetItems_BindingRow_IncludesLabelAndGamepadLabel()
    {
        _host.GetGamepadLabel("P1 Up").Returns("D-Pad Up");
        Assert.That(_handler.GetItems()[0], Is.EqualTo("Up\tD-Pad Up"));
    }

    [Test]
    public void IsItemEnabled_SteamUnavailable_AlwaysTrue()
    {
        Assert.That(_handler.IsItemEnabled(0), Is.True);
        Assert.That(_handler.IsItemEnabled(BackIndex), Is.True);
    }

    [Test]
    public void GetItemValueIcon_BackRow_ReturnsZero()
    {
        Assert.That(_handler.GetItemValueIcon(BackIndex), Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void GetItemValueIcon_BindingRow_ReturnsGlyphFromMenu()
    {
        var glyph = new IntPtr(789);
        _host.GetGamepadGlyph("P1 Down").Returns(glyph);
        Assert.That(_handler.GetItemValueIcon(1), Is.EqualTo(glyph));
    }

    [Test]
    public void Activate_BackRow_NavigatesToSettings()
    {
        _handler.Activate(BackIndex);
        _host.Received(1).NavigateTo(Screen.Settings);
    }

    [Test]
    public void Activate_BindingRow_SetsGamepadRebindingAction()
    {
        _handler.Activate(1);
        _host.Received(1).GamepadRebindingAction = "P1 Down";
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
        var handler = new GamepadBindingsHandler(_host, player: 2);
        Assert.That(handler.GetActiveNesButton(0), Is.EqualTo("P2 Up"));
    }

    [Test]
    public void Player2_BackRow_NavigatesToPlayerSelect()
    {
        var handler = new GamepadBindingsHandler(_host, player: 2);
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
        var handler = new GamepadBindingsHandler(_host);
        handler.Activate(BackIndex);
        _host.Received(1).NavigateTo(Screen.PlayerSelect);
    }
}
