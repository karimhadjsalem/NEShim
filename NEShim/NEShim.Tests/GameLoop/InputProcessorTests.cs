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
}
