using NEShim.Config;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class PlayerSelectHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
    }

    private PlayerSelectHandler CreateHandler() => new(_host);

    // ── ItemCount: gamepad row per player + keyboard rows (gated by HideKeyboardControlsForExtraPlayers) + Back ──

    [TestCase(2, 4)]  // gamepad P1,P2 (2) + keyboard P1 only (1, hidden beyond P1 by default) + Back
    [TestCase(3, 5)]  // gamepad P1-P3 (3) + keyboard P1 only (1) + Back
    [TestCase(4, 6)]  // gamepad P1-P4 (4) + keyboard P1 only (1) + Back
    public void ItemCount_DefaultHideKeyboard_IsGamepadRowsPlusOneKeyboardRowPlusBack(int playerCount, int expectedItemCount)
    {
        _config.PlayerCount = playerCount;
        Assert.That(CreateHandler().ItemCount, Is.EqualTo(expectedItemCount));
    }

    [TestCase(2, 5)]  // gamepad P1,P2 (2) + keyboard P1,P2 (2) + Back
    [TestCase(4, 9)]  // gamepad P1-P4 (4) + keyboard P1-P4 (4) + Back
    public void ItemCount_HideKeyboardDisabled_IncludesKeyboardRowPerPlayer(int playerCount, int expectedItemCount)
    {
        _config.PlayerCount = playerCount;
        _config.HideKeyboardControlsForExtraPlayers = false;
        Assert.That(CreateHandler().ItemCount, Is.EqualTo(expectedItemCount));
    }

    // ── GetItems: all gamepad rows (incl. player 1) first, then keyboard rows, then Back ──

    [Test]
    public void GetItems_FourPlayers_DefaultHideKeyboard_ListsAllGamepadThenPlayer1KeyboardThenBack()
    {
        _config.PlayerCount = 4;
        var items = CreateHandler().GetItems();

        Assert.That(items.Length, Is.EqualTo(6));
        Assert.That(items[0], Does.Contain("1").And.Contain(_localization.SettingsGamepad));
        Assert.That(items[1], Does.Contain("2").And.Contain(_localization.SettingsGamepad));
        Assert.That(items[2], Does.Contain("3").And.Contain(_localization.SettingsGamepad));
        Assert.That(items[3], Does.Contain("4").And.Contain(_localization.SettingsGamepad));
        Assert.That(items[4], Does.Contain("1").And.Contain(_localization.SettingsKeyboard));
        Assert.That(items[5], Is.EqualTo(_localization.Back));
    }

    [Test]
    public void GetItems_FourPlayers_HideKeyboardDisabled_ListsAllGamepadThenAllKeyboardThenBack()
    {
        _config.PlayerCount = 4;
        _config.HideKeyboardControlsForExtraPlayers = false;
        var items = CreateHandler().GetItems();

        Assert.That(items.Length, Is.EqualTo(9));
        for (int player = 1; player <= 4; player++)
            Assert.That(items[player - 1], Does.Contain(player.ToString()).And.Contain(_localization.SettingsGamepad));
        for (int player = 1; player <= 4; player++)
            Assert.That(items[4 + player - 1], Does.Contain(player.ToString()).And.Contain(_localization.SettingsKeyboard));
        Assert.That(items[8], Is.EqualTo(_localization.Back));
    }

    // ── SeparatorIndex: boundary between the gamepad group and the keyboard group ──

    [Test]
    public void SeparatorIndex_EqualsGamepadRowCount()
    {
        _config.PlayerCount = 3;
        Assert.That(CreateHandler().SeparatorIndex, Is.EqualTo(3));
    }

    // ── Activate: gamepad rows (all players, incl. player 1) ──

    [TestCase(0, Screen.GamepadBindings)]
    [TestCase(1, Screen.GamepadBindingsP2)]
    [TestCase(2, Screen.GamepadBindingsP3)]
    [TestCase(3, Screen.GamepadBindingsP4)]
    public void Activate_GamepadRow_NavigatesToExpectedPerPlayerScreen(int index, Screen expected)
    {
        _config.PlayerCount = 4;
        CreateHandler().Activate(index);
        _host.Received(1).NavigateTo(expected);
    }

    // ── Activate: keyboard rows, default (hidden beyond player 1) ──

    [Test]
    public void Activate_KeyboardRow_DefaultHideKeyboard_OnlyPlayer1RowExists()
    {
        _config.PlayerCount = 4;
        var handler = CreateHandler();

        handler.Activate(4); // first row after the 4 gamepad rows
        _host.Received(1).NavigateTo(Screen.KeyboardBindings);
    }

    // ── Activate: keyboard rows, HideKeyboardControlsForExtraPlayers disabled ──

    [TestCase(4, Screen.KeyboardBindings)]
    [TestCase(5, Screen.KeyboardBindingsP2)]
    [TestCase(6, Screen.KeyboardBindingsP3)]
    [TestCase(7, Screen.KeyboardBindingsP4)]
    public void Activate_KeyboardRow_HideKeyboardDisabled_NavigatesToExpectedPerPlayerScreen(int index, Screen expected)
    {
        _config.PlayerCount = 4;
        _config.HideKeyboardControlsForExtraPlayers = false;
        CreateHandler().Activate(index);
        _host.Received(1).NavigateTo(expected);
    }

    [Test]
    public void Activate_BackRow_NavigatesToSettings()
    {
        _config.PlayerCount = 4;
        var handler = CreateHandler();
        handler.Activate(handler.ItemCount - 1);
        _host.Received(1).NavigateTo(Screen.Settings);
    }
}
