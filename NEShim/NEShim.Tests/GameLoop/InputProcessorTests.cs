using System.Collections.Immutable;
using NEShim.Config;
using NEShim.GameLoop;
using NEShim.Input;
using NEShim.Saves;
using NEShim.Localization;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.GameLoop;

[TestFixture]
internal class InputProcessorTests
{
    private IInputReader    _input       = null!;
    private IMenuInputTarget _menuInput  = null!;
    private ISaveManager    _saves       = null!;
    private AppConfig       _config      = null!;
    // Synchronous inline marshal — adequate for unit tests where no UI thread exists
    private static readonly Action<Action> SyncMarshal = a => a();

    [SetUp]
    public void SetUp()
    {
        _input     = Substitute.For<IInputReader>();
        _menuInput = Substitute.For<IMenuInputTarget>();
        _saves     = Substitute.For<ISaveManager>();
        _saves.SlotCount.Returns(8);
        _config    = new AppConfig();
    }

    private InGameMenu CreateMenu() =>
        new(_saves, _config, new LocalizationData(),
            () => { }, () => { }, () => { }, _ => { }, () => { },
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            _ => { }, _ => { }, (_, _, _, _) => { }, (_, _, _) => { });

    private InputProcessor CreateProcessor(InGameMenu menu) =>
        new(_input, menu, _menuInput, SyncMarshal);

    // ---- Poll ----

    [Test]
    public void Poll_DelegatesToInputReaderWithConfig()
    {
        var expected = new InputSnapshot(ImmutableHashSet<string>.Empty);
        _input.PollSnapshot(_config).Returns(expected);
        var processor = CreateProcessor(CreateMenu());

        var result = processor.Poll(_config);

        Assert.That(result, Is.SameAs(expected));
    }

    // ---- AdvanceHotkeys ----

    [Test]
    public void AdvanceHotkeys_DelegatesToInputReader()
    {
        var processor = CreateProcessor(CreateMenu());
        processor.AdvanceHotkeys(_config);
        _input.Received(1).AdvanceHotkeyState(_config);
    }

    // ---- TryDismissDisconnectScreen ----

    [Test]
    public void TryDismissDisconnectScreen_WhenMenuClosed_ReturnsFalse()
    {
        var menu = CreateMenu(); // menu starts closed
        var processor = CreateProcessor(menu);

        bool result = processor.TryDismissDisconnectScreen(isMainMenuActive: false);

        Assert.That(result, Is.False);
        Assert.That(processor.JustDismissedDisconnectScreen, Is.False);
    }

    [Test]
    public void TryDismissDisconnectScreen_WhenMainMenuActive_ReturnsFalse()
    {
        var menu = CreateMenu();
        menu.Open(InGameMenu.Screen.ControllerDisconnected);
        _input.IsAnyInputJustPressed().Returns(true);
        var processor = CreateProcessor(menu);

        bool result = processor.TryDismissDisconnectScreen(isMainMenuActive: true);

        Assert.That(result, Is.False);
        Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void TryDismissDisconnectScreen_WhenOnWrongScreen_ReturnsFalse()
    {
        var menu = CreateMenu();
        menu.Open(); // opens on Root screen, not ControllerDisconnected
        _input.IsAnyInputJustPressed().Returns(true);
        var processor = CreateProcessor(menu);

        bool result = processor.TryDismissDisconnectScreen(isMainMenuActive: false);

        Assert.That(result, Is.False);
        Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void TryDismissDisconnectScreen_WhenNoInput_ReturnsFalse()
    {
        var menu = CreateMenu();
        menu.Open(InGameMenu.Screen.ControllerDisconnected);
        _input.IsAnyInputJustPressed().Returns(false);
        var processor = CreateProcessor(menu);

        bool result = processor.TryDismissDisconnectScreen(isMainMenuActive: false);

        Assert.That(result, Is.False);
        Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void TryDismissDisconnectScreen_WhenInputReceived_ClosesMenuAndReturnsTrue()
    {
        var menu = CreateMenu();
        menu.Open(InGameMenu.Screen.ControllerDisconnected);
        _input.IsAnyInputJustPressed().Returns(true);
        var processor = CreateProcessor(menu);

        bool result = processor.TryDismissDisconnectScreen(isMainMenuActive: false);

        Assert.That(result, Is.True);
        Assert.That(processor.JustDismissedDisconnectScreen, Is.True);
        Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void TryDismissDisconnectScreen_FlagResetOnNextCall()
    {
        var menu = CreateMenu();
        menu.Open(InGameMenu.Screen.ControllerDisconnected);
        _input.IsAnyInputJustPressed().Returns(true);
        var processor = CreateProcessor(menu);

        processor.TryDismissDisconnectScreen(isMainMenuActive: false); // dismisses
        // menu is now closed — next call shouldn't set the flag
        _input.IsAnyInputJustPressed().Returns(false);
        processor.TryDismissDisconnectScreen(isMainMenuActive: false);

        Assert.That(processor.JustDismissedDisconnectScreen, Is.False);
    }

    // ---- PollPausedMenuInput ----
    // These tests were unwritable before the Control→Action<Action> refactor because
    // BeginInvoke requires a live WinForms message pump. SyncMarshal runs the delegate
    // immediately, so dispatched calls are visible to NSubstitute.

    [Test]
    public void PollPausedMenuInput_WhenNotWaiting_AndNavPresent_DispatchesNavToMenuInput()
    {
        var nav = new MenuNavInput { Down = true };
        _menuInput.IsWaitingForGamepadButton.Returns(false);
        _input.PollMenuNav(_config).Returns(nav);
        var processor = CreateProcessor(CreateMenu());

        processor.PollPausedMenuInput(_config);

        _menuInput.Received(1).HandleGamepadNav(nav);
    }

    [Test]
    public void PollPausedMenuInput_WhenNotWaiting_AndNoNav_DoesNotDispatchNav()
    {
        _menuInput.IsWaitingForGamepadButton.Returns(false);
        _input.PollMenuNav(_config).Returns(default(MenuNavInput));
        var processor = CreateProcessor(CreateMenu());

        processor.PollPausedMenuInput(_config);

        _menuInput.DidNotReceive().HandleGamepadNav(Arg.Any<MenuNavInput>());
    }

    [Test]
    public void PollPausedMenuInput_WhenWaiting_AndButtonPressed_DispatchesButtonToMenuInput()
    {
        _menuInput.IsWaitingForGamepadButton.Returns(true);
        _input.PollAnyGamepadButtonPressed().Returns("A");
        var processor = CreateProcessor(CreateMenu());

        processor.PollPausedMenuInput(_config);

        _menuInput.Received(1).HandleGamepadButtonPress("A");
    }

    [Test]
    public void PollPausedMenuInput_WhenWaiting_AndNoButton_DoesNotDispatchButton()
    {
        _menuInput.IsWaitingForGamepadButton.Returns(true);
        _input.PollAnyGamepadButtonPressed().Returns((string?)null);
        var processor = CreateProcessor(CreateMenu());

        processor.PollPausedMenuInput(_config);

        _menuInput.DidNotReceive().HandleGamepadButtonPress(Arg.Any<string>());
    }

    [Test]
    public void PollPausedMenuInput_WhenEnteringBindingMode_FlushesBindingEdges()
    {
        // First call: not waiting — sets _prevIsWaitingForGamepadButton = false
        _menuInput.IsWaitingForGamepadButton.Returns(false);
        _input.PollMenuNav(_config).Returns(default(MenuNavInput));
        var processor = CreateProcessor(CreateMenu());
        processor.PollPausedMenuInput(_config);

        // Second call: now waiting — false→true transition must flush
        _menuInput.IsWaitingForGamepadButton.Returns(true);
        _input.PollAnyGamepadButtonPressed().Returns((string?)null);
        processor.PollPausedMenuInput(_config);

        _input.Received(1).FlushBindingEdges();
    }

    [Test]
    public void PollPausedMenuInput_WhenExitingBindingMode_AdvancesNavEdgeStateBeforeRealPoll()
    {
        // First call: waiting — sets _prevIsWaitingForGamepadButton = true
        _menuInput.IsWaitingForGamepadButton.Returns(true);
        _input.PollAnyGamepadButtonPressed().Returns((string?)null);
        var processor = CreateProcessor(CreateMenu());
        processor.PollPausedMenuInput(_config);

        // Second call: not waiting — true→false transition triggers one throwaway
        // PollMenuNav to advance edge state, then a second real nav poll
        _menuInput.IsWaitingForGamepadButton.Returns(false);
        _input.PollMenuNav(_config).Returns(default(MenuNavInput));
        processor.PollPausedMenuInput(_config);

        _input.Received(2).PollMenuNav(_config);
    }

    [Test]
    public void PollPausedMenuInput_WhenNotWaiting_CallsGetHeldSliderDir()
    {
        _menuInput.IsWaitingForGamepadButton.Returns(false);
        _input.PollMenuNav(_config).Returns(default(MenuNavInput));
        _input.GetHeldSliderDir(_config).Returns((false, false));
        var processor = CreateProcessor(CreateMenu());

        processor.PollPausedMenuInput(_config);

        _input.Received(1).GetHeldSliderDir(_config);
    }

    [Test]
    public void PollPausedMenuInput_WhenInBindingMode_DoesNotCallGetHeldSliderDir()
    {
        _menuInput.IsWaitingForGamepadButton.Returns(true);
        _input.PollAnyGamepadButtonPressed().Returns((string?)null);
        var processor = CreateProcessor(CreateMenu());

        processor.PollPausedMenuInput(_config);

        _input.DidNotReceive().GetHeldSliderDir(Arg.Any<AppConfig>());
    }

    [Test]
    public void PollPausedMenuInput_WhenHeldLeftOnFirstCall_DoesNotDispatchRepeatImmediately()
    {
        _menuInput.IsWaitingForGamepadButton.Returns(false);
        _input.PollMenuNav(_config).Returns(default(MenuNavInput));
        _input.GetHeldSliderDir(_config).Returns((true, false)); // Left held
        var processor = CreateProcessor(CreateMenu());

        // First call: direction just started — initial delay not elapsed; no repeat
        processor.PollPausedMenuInput(_config);
        // Second call immediately: still within initial delay; still no repeat
        processor.PollPausedMenuInput(_config);

        // No repeat should fire — only edge nav (none here) should dispatch
        _menuInput.DidNotReceive().HandleGamepadNav(Arg.Is<MenuNavInput>(n => n.Left));
    }
}
