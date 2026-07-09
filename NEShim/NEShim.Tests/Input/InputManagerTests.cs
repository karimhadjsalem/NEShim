using System.Collections.Generic;
using System.Collections.Immutable;
using SDL3;
using NSubstitute;
using NEShim.Config;
using NEShim.Input;
using NEShim.Input.Mappers;
using NEShim.Input.Sources;

namespace NEShim.Tests.Input;

[TestFixture]
internal class InputManagerTests
{
    private KeyboardInputSource _keyboard = null!;
    private IInputSource        _mockGamepadSource = null!;
    private IInputSource        _mockSteam  = null!;
    private IGamepadDevice      _stubDevice = null!;
    private InputManager        _manager    = null!;
    private AppConfig           _config     = null!;

    [SetUp]
    public void SetUp()
    {
        _keyboard = new KeyboardInputSource();

        _mockGamepadSource = Substitute.For<IInputSource>();
        _mockGamepadSource.IsAvailable.Returns(false);
        _mockGamepadSource.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());

        _mockSteam = Substitute.For<IInputSource>();
        _mockSteam.IsAvailable.Returns(false);
        _mockSteam.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());

        _stubDevice = Substitute.For<IGamepadDevice>();
        _stubDevice.GetState(Arg.Any<uint>()).Returns(default(GamepadState));

        _manager = new InputManager(
            _keyboard, _mockGamepadSource, _mockSteam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(),
            _stubDevice);

        _config = new AppConfig();
    }

    [TearDown]
    public void TearDown() => _stubDevice?.Dispose();

    // ── Keyboard mapping ────────────────────────────────────────────────────────

    [Test]
    public void OnKeyDown_ThenOnKeyUp_KeyIsNoLongerTracked()
    {
        _manager.OnKeyDown(SDL.Keycode.W);
        Assert.That(_manager.PollSnapshot(_config).IsPressed("P1 Up"), Is.True);

        _manager.OnKeyUp(SDL.Keycode.W);
        Assert.That(_manager.PollSnapshot(_config).IsPressed("P1 Up"), Is.False);
    }

    [Test]
    public void PollSnapshot_MapsKeyToNesButton()
    {
        _manager.OnKeyDown(SDL.Keycode.W);
        Assert.That(_manager.PollSnapshot(_config).IsPressed("P1 Up"), Is.True);
    }

    [Test]
    public void PollSnapshot_UnpressedKey_ButtonNotInSnapshot()
    {
        Assert.That(_manager.PollSnapshot(_config).IsPressed("P1 Up"), Is.False);
    }

    [Test]
    public void PollSnapshot_MultipleKeysMapped_AllAppearInSnapshot()
    {
        _manager.OnKeyDown(SDL.Keycode.W);
        _manager.OnKeyDown(SDL.Keycode.S);
        _manager.OnKeyDown(SDL.Keycode.Return);

        var snapshot = _manager.PollSnapshot(_config);
        Assert.That(snapshot.IsPressed("P1 Up"),    Is.True);
        Assert.That(snapshot.IsPressed("P1 Down"),  Is.True);
        Assert.That(snapshot.IsPressed("P1 Start"), Is.True);
    }

    [Test]
    public void PollSnapshot_NullKeyInBinding_DoesNotMapButton()
    {
        _config.InputMappings["P1 Up"] = new InputBinding(null, null);
        Assert.That(_manager.PollSnapshot(_config).IsPressed("P1 Up"), Is.False);
    }

    [Test]
    public void PollSnapshot_InvalidKeyName_DoesNotMapButton()
    {
        _config.InputMappings["P1 Up"] = new InputBinding("NotAValidKey!!!", null);
        Assert.That(_manager.PollSnapshot(_config).IsPressed("P1 Up"), Is.False);
    }

    // ── Source orchestration ────────────────────────────────────────────────────

    [Test]
    public void PollSnapshot_AvailableGamepadSource_SDL3GamepadMapperCalled()
    {
        var mockXMapper = Substitute.For<IInputMapper>();
        var manager = new InputManager(
            _keyboard, _mockGamepadSource, _mockSteam,
            new KeyboardMapper(), mockXMapper, new SteamInputMapper(), _stubDevice);

        _mockGamepadSource.IsAvailable.Returns(true);

        manager.PollSnapshot(_config);

        mockXMapper.Received(1).Map(
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<AppConfig>(),
            Arg.Any<ImmutableHashSet<string>.Builder>());
    }

    [Test]
    public void PollSnapshot_UnavailableGamepadSource_SDL3GamepadMapperNotCalled()
    {
        var mockXMapper = Substitute.For<IInputMapper>();
        var manager = new InputManager(
            _keyboard, _mockGamepadSource, _mockSteam,
            new KeyboardMapper(), mockXMapper, new SteamInputMapper(), _stubDevice);

        // _mockGamepadSource.IsAvailable defaults to false
        manager.PollSnapshot(_config);

        mockXMapper.DidNotReceive().Map(
            Arg.Any<IReadOnlySet<string>>(),
            Arg.Any<AppConfig>(),
            Arg.Any<ImmutableHashSet<string>.Builder>());
    }

    // ── GamepadDisconnected event (IoC) ─────────────────────────────────────────

    [Test]
    public void PollSnapshot_ControllerWasConnectedNowDisconnected_FiresGamepadDisconnected()
    {
        _mockGamepadSource.IsAvailable.Returns(true);
        _manager.PollSnapshot(_config); // wasConnected = true

        bool fired = false;
        _manager.GamepadDisconnected += () => fired = true;
        _mockGamepadSource.IsAvailable.Returns(false);
        _manager.PollSnapshot(_config);

        Assert.That(fired, Is.True);
    }

    [Test]
    public void PollSnapshot_ControllerNeverConnected_GamepadDisconnectedNotFired()
    {
        bool fired = false;
        _manager.GamepadDisconnected += () => fired = true;
        _manager.PollSnapshot(_config); // was never connected

        Assert.That(fired, Is.False);
    }

    [Test]
    public void PollSnapshot_ControllerStillConnected_GamepadDisconnectedNotFired()
    {
        _mockGamepadSource.IsAvailable.Returns(true);
        _manager.PollSnapshot(_config);

        bool fired = false;
        _manager.GamepadDisconnected += () => fired = true;
        _manager.PollSnapshot(_config); // still connected

        Assert.That(fired, Is.False);
    }

    [Test]
    public void PollSnapshot_DisconnectThenReconnect_EventFiresOnceOnDisconnectOnly()
    {
        int count = 0;
        _manager.GamepadDisconnected += () => count++;

        _mockGamepadSource.IsAvailable.Returns(true);
        _manager.PollSnapshot(_config); // connected

        _mockGamepadSource.IsAvailable.Returns(false);
        _manager.PollSnapshot(_config); // disconnected → fires

        _mockGamepadSource.IsAvailable.Returns(true);
        _manager.PollSnapshot(_config); // reconnected → no fire

        Assert.That(count, Is.EqualTo(1));
    }

    // ── HotkeyFired event (IoC) ─────────────────────────────────────────────────

    [Test]
    public void AdvanceHotkeyState_F5JustPressed_HotkeyFiredSaveActiveSlot()
    {
        var fired = new List<string>();
        _manager.HotkeyFired += action => fired.Add(action);

        _manager.OnKeyDown(SDL.Keycode.F5);
        _manager.AdvanceHotkeyState(_config);

        Assert.That(fired, Is.EqualTo(new[] { "SaveActiveSlot" }));
    }

    [Test]
    public void AdvanceHotkeyState_F5HeldTwoFrames_HotkeyFiredOnce()
    {
        int count = 0;
        _manager.HotkeyFired += _ => count++;

        _manager.OnKeyDown(SDL.Keycode.F5);
        _manager.AdvanceHotkeyState(_config); // frame 1: edge fires
        _manager.AdvanceHotkeyState(_config); // frame 2: held, no edge

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void AdvanceHotkeyState_F9JustPressed_HotkeyFiredLoadActiveSlot()
    {
        var fired = new List<string>();
        _manager.HotkeyFired += action => fired.Add(action);

        _manager.OnKeyDown(SDL.Keycode.F9);
        _manager.AdvanceHotkeyState(_config);

        Assert.That(fired, Contains.Item("LoadActiveSlot"));
    }

    [Test]
    public void AdvanceHotkeyState_F1JustPressed_HotkeyFiredSelectSlot1()
    {
        var fired = new List<string>();
        _manager.HotkeyFired += action => fired.Add(action);

        _manager.OnKeyDown(SDL.Keycode.F1);
        _manager.AdvanceHotkeyState(_config);

        Assert.That(fired, Contains.Item("SelectSlot1"));
    }

    [Test]
    public void AdvanceHotkeyState_TwoHotkeysInSameFrame_BothEventsFire()
    {
        var fired = new List<string>();
        _manager.HotkeyFired += action => fired.Add(action);

        _manager.OnKeyDown(SDL.Keycode.F1);
        _manager.OnKeyDown(SDL.Keycode.F5);
        _manager.AdvanceHotkeyState(_config);

        Assert.That(fired, Contains.Item("SelectSlot1"));
        Assert.That(fired, Contains.Item("SaveActiveSlot"));
    }

    [Test]
    public void AdvanceHotkeyState_NoInput_NoEventsFired()
    {
        bool anyFired = false;
        _manager.HotkeyFired += _ => anyFired = true;
        _manager.MenuToggleRequested += () => anyFired = true;

        _manager.AdvanceHotkeyState(_config);

        Assert.That(anyFired, Is.False);
    }

    // ── MenuToggleRequested event (IoC) ─────────────────────────────────────────

    [Test]
    public void AdvanceHotkeyState_EscJustPressed_MenuToggleRequestedFired()
    {
        bool fired = false;
        _manager.MenuToggleRequested += () => fired = true;

        _manager.OnKeyDown(SDL.Keycode.Escape);
        _manager.AdvanceHotkeyState(_config);

        Assert.That(fired, Is.True);
    }

    [Test]
    public void AdvanceHotkeyState_EscHeld_MenuToggleRequestedFiredOnce()
    {
        int count = 0;
        _manager.MenuToggleRequested += () => count++;

        _manager.OnKeyDown(SDL.Keycode.Escape);
        _manager.AdvanceHotkeyState(_config); // edge fires
        _manager.AdvanceHotkeyState(_config); // held, silent

        Assert.That(count, Is.EqualTo(1));
    }

    // ── Menu nav composition ────────────────────────────────────────────────────

    [Test]
    public void PollMenuNav_WhenNoPadConnected_ReturnsAllFalseNav()
    {
        Assert.That(_manager.PollMenuNav(_config).Any, Is.False);
    }

    [Test]
    public void PollMenuNav_GamepadAndSteam_Unioned()
    {
        var xInput = Substitute.For<IInputSource, IMenuNavSource>();
        xInput.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());
        ((IMenuNavSource)xInput).GetMenuNav(Arg.Any<AppConfig>()).Returns(new MenuNavInput { Up = true });

        var steam = Substitute.For<IInputSource, IMenuNavSource>();
        steam.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());
        ((IMenuNavSource)steam).GetMenuNav(Arg.Any<AppConfig>()).Returns(new MenuNavInput { Down = true });

        var manager = new InputManager(
            _keyboard, xInput, steam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(), _stubDevice);

        var nav = manager.PollMenuNav(_config);

        Assert.That(nav.Up,   Is.True);
        Assert.That(nav.Down, Is.True);
    }

    // ── Binding UI ──────────────────────────────────────────────────────────────

    [Test]
    public void PollAnyGamepadButtonPressed_WhenNoPadConnected_ReturnsNull()
    {
        Assert.That(_manager.PollAnyGamepadButtonPressed(), Is.Null);
    }

    // ── GetHeldSliderDir (keyboard path only — Gamepad/Steam are static globals) ─

    [Test]
    public void GetHeldSliderDir_NoInput_ReturnsBothFalse()
    {
        var (left, right) = _manager.GetHeldSliderDir(_config);
        Assert.That(left,  Is.False);
        Assert.That(right, Is.False);
    }

    [Test]
    public void GetHeldSliderDir_LeftArrowDown_ReturnsLeftTrue()
    {
        _manager.OnKeyDown(SDL.Keycode.Left);
        var (left, right) = _manager.GetHeldSliderDir(_config);
        Assert.That(left,  Is.True);
        Assert.That(right, Is.False);
    }

    [Test]
    public void GetHeldSliderDir_RightArrowDown_ReturnsRightTrue()
    {
        _manager.OnKeyDown(SDL.Keycode.Right);
        var (left, right) = _manager.GetHeldSliderDir(_config);
        Assert.That(right, Is.True);
        Assert.That(left,  Is.False);
    }

    [Test]
    public void GetHeldSliderDir_AfterKeyUp_ReturnsFalse()
    {
        _manager.OnKeyDown(SDL.Keycode.Left);
        _manager.OnKeyUp(SDL.Keycode.Left);
        var (left, _) = _manager.GetHeldSliderDir(_config);
        Assert.That(left, Is.False);
    }

    // ── PollAnyControllerButton ──────────────────────────────────────────────────

    [Test]
    public void PollAnyControllerButton_WhenSourceDoesNotImplementAnyButtonSource_ReturnsFalse()
    {
        // _mockGamepadSource is IInputSource only, not IAnyButtonSource
        Assert.That(_manager.PollAnyControllerButton(), Is.False);
    }

    [Test]
    public void PollAnyControllerButton_WithAnyButtonSource_ReturnsTrue_WhenButtonJustPressed()
    {
        var anySource = Substitute.For<IInputSource, IAnyButtonSource>();
        anySource.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());
        ((IAnyButtonSource)anySource).AnyJustPressed().Returns(true);

        var manager = new InputManager(
            _keyboard, anySource, _mockSteam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(), _stubDevice);

        Assert.That(manager.PollAnyControllerButton(), Is.True);
    }

    [Test]
    public void PollAnyControllerButton_WithAnyButtonSource_ReturnsFalse_WhenNoPress()
    {
        var anySource = Substitute.For<IInputSource, IAnyButtonSource>();
        anySource.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());
        ((IAnyButtonSource)anySource).AnyJustPressed().Returns(false);

        var manager = new InputManager(
            _keyboard, anySource, _mockSteam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(), _stubDevice);

        Assert.That(manager.PollAnyControllerButton(), Is.False);
    }

    // ── FlushBindingEdges ────────────────────────────────────────────────────────

    [Test]
    public void FlushBindingEdges_WhenSourceDoesNotImplementIBindingSource_IsNoOp()
    {
        // _mockGamepadSource is IInputSource only — FlushBindingEdges must not throw
        Assert.DoesNotThrow(() => _manager.FlushBindingEdges());
    }

    [Test]
    public void FlushBindingEdges_WithBindingSource_CallsFlushEdgesOnce()
    {
        var bindingSource = Substitute.For<IInputSource, IBindingSource>();
        bindingSource.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());

        var manager = new InputManager(
            _keyboard, bindingSource, _mockSteam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(), _stubDevice);

        manager.FlushBindingEdges();

        ((IBindingSource)bindingSource).Received(1).FlushEdges();
    }

    // ── PollAnyGamepadButtonPressed with IBindingSource ──────────────────────────

    [Test]
    public void PollAnyGamepadButtonPressed_WithBindingSource_DelegatesToPollAnyButtonPressed()
    {
        var bindingSource = Substitute.For<IInputSource, IBindingSource>();
        bindingSource.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());
        ((IBindingSource)bindingSource).PollAnyButtonPressed().Returns("B");

        var manager = new InputManager(
            _keyboard, bindingSource, _mockSteam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(), _stubDevice);

        Assert.That(manager.PollAnyGamepadButtonPressed(), Is.EqualTo("B"));
    }

    [Test]
    public void PollAnyGamepadButtonPressed_WithBindingSource_WhenNoPress_ReturnsNull()
    {
        var bindingSource = Substitute.For<IInputSource, IBindingSource>();
        bindingSource.GetActiveIdentifiers(Arg.Any<AppConfig>()).Returns(new HashSet<string>());
        ((IBindingSource)bindingSource).PollAnyButtonPressed().Returns((string?)null);

        var manager = new InputManager(
            _keyboard, bindingSource, _mockSteam,
            new KeyboardMapper(), new SDL3GamepadMapper(), new SteamInputMapper(), _stubDevice);

        Assert.That(manager.PollAnyGamepadButtonPressed(), Is.Null);
    }
}
