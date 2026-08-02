using NSubstitute;
using NEShim.Config;
using NEShim.Input;
using NEShim.Input.Sources;

namespace NEShim.Tests.Input.Sources;

[TestFixture]
internal class SDL3GamepadSourceTests
{
    private AppConfig _config = null!;

    [SetUp]
    public void SetUp() => _config = new AppConfig(); // GamepadDeadzone=8000, AnalogStickMode="Cardinal"

    private static GamepadState NotConnected() => default;

    private static GamepadState Connected(
        bool dpadUp = false, bool dpadDown = false, bool dpadLeft = false, bool dpadRight = false,
        bool a = false, bool b = false, bool x = false, bool y = false,
        bool start = false, bool back = false,
        bool leftShoulder = false, bool rightShoulder = false,
        bool leftThumb = false, bool rightThumb = false,
        short thumbLX = 0, short thumbLY = 0) => new()
        {
            Connected     = true,
            DPadUp        = dpadUp,        DPadDown  = dpadDown,
            DPadLeft      = dpadLeft,      DPadRight = dpadRight,
            A = a, B = b, X = x, Y = y,
            Start         = start,         Back           = back,
            LeftShoulder  = leftShoulder,  RightShoulder  = rightShoulder,
            LeftThumb     = leftThumb,     RightThumb     = rightThumb,
            ThumbLX       = thumbLX,       ThumbLY        = thumbLY,
        };

    private static SDL3GamepadSource MakeSource(GamepadState state)
    {
        var device = Substitute.For<IGamepadDevice>();
        device.GetState(Arg.Any<uint>()).Returns(state);
        return new SDL3GamepadSource(device);
    }

    // ── IsAvailable ─────────────────────────────────────────────────────────────

    [Test]
    public void IsAvailable_WhenNotConnected_ReturnsFalse()
    {
        var source = MakeSource(NotConnected());
        source.GetActiveIdentifiers(_config);
        Assert.That(source.IsAvailable, Is.False);
    }

    [Test]
    public void IsAvailable_WhenConnected_ReturnsTrue()
    {
        var source = MakeSource(Connected());
        source.GetActiveIdentifiers(_config);
        Assert.That(source.IsAvailable, Is.True);
    }

    // ── Digital buttons ─────────────────────────────────────────────────────────

    [Test]
    public void GetActiveIdentifiers_NotConnected_ReturnsEmpty()
    {
        var source = MakeSource(NotConnected());
        Assert.That(source.GetActiveIdentifiers(_config), Is.Empty);
    }

    [Test]
    public void GetActiveIdentifiers_DPadUp_IncludesDPadUp()
    {
        var source = MakeSource(Connected(dpadUp: true));
        Assert.That(source.GetActiveIdentifiers(_config), Contains.Item("DPadUp"));
    }

    [Test]
    public void GetActiveIdentifiers_AllDigitalButtons_AllIncluded()
    {
        var source = MakeSource(Connected(
            dpadUp: true, dpadDown: true, dpadLeft: true, dpadRight: true,
            a: true, b: true, x: true, y: true,
            start: true, back: true,
            leftShoulder: true, rightShoulder: true,
            leftThumb: true, rightThumb: true));

        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Is.SupersetOf(new[]
        {
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
            "A", "B", "X", "Y", "Start", "Back",
            "LeftShoulder", "RightShoulder", "LeftThumb", "RightThumb",
        }));
    }

    // ── Analog — Cardinal mode ──────────────────────────────────────────────────

    [Test]
    public void GetActiveIdentifiers_CardinalMode_StickUpYDominant_IncludesAnalogUp()
    {
        // Y=10000 > dz=8000, |Y| >= |X| → cardinal selects Up only
        var source = MakeSource(Connected(thumbLY: 10000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("AnalogUp"));
        Assert.That(ids, Does.Not.Contain("AnalogLeft"));
        Assert.That(ids, Does.Not.Contain("AnalogRight"));
    }

    [Test]
    public void GetActiveIdentifiers_CardinalMode_XDominant_AnalogUpSuppressed()
    {
        // X=15000 > Y=10000 → right axis dominates; AnalogUp is suppressed
        var source = MakeSource(Connected(thumbLX: 15000, thumbLY: 10000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Does.Not.Contain("AnalogUp"));
        Assert.That(ids, Contains.Item("AnalogRight"));
    }

    [Test]
    public void GetActiveIdentifiers_BelowDeadzone_NoAnalogIdentifier()
    {
        var source = MakeSource(Connected(thumbLX: 1000, thumbLY: 1000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Does.Not.Contain("AnalogUp"));
        Assert.That(ids, Does.Not.Contain("AnalogDown"));
        Assert.That(ids, Does.Not.Contain("AnalogLeft"));
        Assert.That(ids, Does.Not.Contain("AnalogRight"));
    }

    // ── Analog — Diagonal mode ──────────────────────────────────────────────────

    [Test]
    public void GetActiveIdentifiers_DiagonalMode_BothAxesAboveDeadzone_BothPresent()
    {
        _config.AnalogStickMode = "Diagonal";
        var source = MakeSource(Connected(thumbLX: 10000, thumbLY: 10000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("AnalogUp"));
        Assert.That(ids, Contains.Item("AnalogRight"));
    }

    // ── D-Pad / analog-stick interchangeability ─────────────────────────────────
    // AppConfig.GamepadDpadStickInterchangeable defaults to true, matching _config's default
    // in [SetUp] — most tests below rely on that default rather than setting it explicitly.

    [Test]
    public void GetActiveIdentifiers_InterchangeableOn_DPadUpAlone_AlsoAddsAnalogUp()
    {
        var source = MakeSource(Connected(dpadUp: true));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("DPadUp"));
        Assert.That(ids, Contains.Item("AnalogUp"));
    }

    [Test]
    public void GetActiveIdentifiers_InterchangeableOn_AnalogStickAlone_AlsoAddsDPadUp()
    {
        // thumbLY well above the default 8000 deadzone.
        var source = MakeSource(Connected(thumbLY: 20000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("AnalogUp"));
        Assert.That(ids, Contains.Item("DPadUp"));
    }

    [Test]
    public void GetActiveIdentifiers_InterchangeableOn_DoesNotCrossPairUnrelatedDirections()
    {
        var source = MakeSource(Connected(dpadUp: true));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Does.Not.Contain("AnalogDown"));
        Assert.That(ids, Does.Not.Contain("AnalogLeft"));
        Assert.That(ids, Does.Not.Contain("AnalogRight"));
        Assert.That(ids, Does.Not.Contain("DPadDown"));
        Assert.That(ids, Does.Not.Contain("DPadLeft"));
        Assert.That(ids, Does.Not.Contain("DPadRight"));
    }

    [Test]
    public void GetActiveIdentifiers_InterchangeableOff_DPadUpAlone_DoesNotAddAnalogUp()
    {
        _config.GamepadDpadStickInterchangeable = false;
        var source = MakeSource(Connected(dpadUp: true));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("DPadUp"));
        Assert.That(ids, Does.Not.Contain("AnalogUp"));
    }

    [Test]
    public void GetActiveIdentifiers_InterchangeableOff_AnalogStickAlone_DoesNotAddDPadUp()
    {
        _config.GamepadDpadStickInterchangeable = false;
        var source = MakeSource(Connected(thumbLY: 20000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("AnalogUp"));
        Assert.That(ids, Does.Not.Contain("DPadUp"));
    }

    [Test]
    public void GetActiveIdentifiers_InterchangeableOn_NeitherPressed_NeitherAdded()
    {
        var source = MakeSource(Connected());
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Does.Not.Contain("DPadUp"));
        Assert.That(ids, Does.Not.Contain("AnalogUp"));
    }

    // ── Menu nav edge detection ─────────────────────────────────────────────────

    [Test]
    public void GetMenuNav_DPadUp_OnFirstPress_ReturnsUp()
    {
        var source = MakeSource(Connected(dpadUp: true));
        var nav = source.GetMenuNav(_config);
        Assert.That(nav.Up, Is.True);
    }

    [Test]
    public void GetMenuNav_DPadUp_HeldSecondFrame_ReturnsFalse()
    {
        var source = MakeSource(Connected(dpadUp: true));
        source.GetMenuNav(_config); // frame 1: edge consumed
        var nav = source.GetMenuNav(_config); // frame 2: held
        Assert.That(nav.Up, Is.False);
    }

    [Test]
    public void GetMenuNav_NotConnected_ReturnsDefault()
    {
        var source = MakeSource(NotConnected());
        Assert.That(source.GetMenuNav(_config).Any, Is.False);
    }

    [Test]
    public void GetMenuNav_Confirm_A_EdgeTriggered()
    {
        var source = MakeSource(Connected(a: true));
        var nav = source.GetMenuNav(_config);
        Assert.That(nav.Confirm, Is.True);
    }

    [Test]
    public void GetMenuNav_Back_B_EdgeTriggered()
    {
        var source = MakeSource(Connected(b: true));
        var nav = source.GetMenuNav(_config);
        Assert.That(nav.Back, Is.True);
    }

    // ── Menu nav debounce ─────────────────────────────────────────────────────────
    // Exhaustive debounce behavior (per-direction independence, window timing, the
    // never-debounce-the-first-press sentinel) is covered directly against MenuNavEdgeDetector
    // in MenuNavEdgeDetectorTests — this just confirms SDL3GamepadSource actually wires the
    // debounce through rather than bypassing it.

    [Test]
    public void GetMenuNav_RapidReleaseAndRepress_WithinDebounceWindow_SecondEdgeSuppressed()
    {
        long now = 0;
        var device = Substitute.For<IGamepadDevice>();
        bool pressed = true;
        device.GetState(Arg.Any<uint>()).Returns(_ => Connected(dpadDown: pressed));
        var source = new SDL3GamepadSource(device) { NowProvider = () => now };

        var first = source.GetMenuNav(_config); // t=0: genuine press, accepted
        pressed = false;
        source.GetMenuNav(_config);             // t=0: released (simulated contact bounce)
        pressed = true;
        now = 10;                               // t=10ms: re-pressed — within the debounce window
        var bounced = source.GetMenuNav(_config);

        Assert.That(first.Down, Is.True);
        Assert.That(bounced.Down, Is.False);
    }

    // ── FlushEdges ──────────────────────────────────────────────────────────────

    [Test]
    public void FlushEdges_WhenButtonHeld_SubsequentPollSeesThatButtonAlreadySeen()
    {
        var source = MakeSource(Connected(a: true));
        source.FlushEdges();
        Assert.That(source.PollAnyButtonPressed(), Is.Null);
    }

    [Test]
    public void FlushEdges_WhenNotConnected_SubsequentPollStillReturnsNull()
    {
        var source = MakeSource(NotConnected());
        source.FlushEdges();
        Assert.That(source.PollAnyButtonPressed(), Is.Null);
    }

    // ── PollAnyButtonPressed ────────────────────────────────────────────────────

    [Test]
    public void PollAnyButtonPressed_NotConnected_ReturnsNull()
    {
        var source = MakeSource(NotConnected());
        Assert.That(source.PollAnyButtonPressed(), Is.Null);
    }

    [Test]
    public void PollAnyButtonPressed_AJustPressed_ReturnsA()
    {
        var source = MakeSource(Connected(a: true));
        Assert.That(source.PollAnyButtonPressed(), Is.EqualTo("A"));
    }

    [Test]
    public void PollAnyButtonPressed_AHeld_ReturnsNull()
    {
        var source = MakeSource(Connected(a: true));
        source.PollAnyButtonPressed(); // edge consumed
        Assert.That(source.PollAnyButtonPressed(), Is.Null);
    }

    [Test]
    public void PollAnyButtonPressed_EachDigitalButton_ReturnsItsName()
    {
        var cases = new (GamepadState state, string expected)[]
        {
            (Connected(b: true),             "B"),
            (Connected(x: true),             "X"),
            (Connected(y: true),             "Y"),
            (Connected(start: true),         "Start"),
            (Connected(back: true),          "Back"),
            (Connected(leftShoulder: true),  "LeftShoulder"),
            (Connected(rightShoulder: true), "RightShoulder"),
            (Connected(leftThumb: true),     "LeftThumb"),
            (Connected(rightThumb: true),    "RightThumb"),
            (Connected(dpadDown: true),      "DPadDown"),
            (Connected(dpadLeft: true),      "DPadLeft"),
            (Connected(dpadRight: true),     "DPadRight"),
            (Connected(dpadUp: true),        "DPadUp"),
        };
        foreach (var (state, expected) in cases)
        {
            var source = MakeSource(state);
            Assert.That(source.PollAnyButtonPressed(), Is.EqualTo(expected), $"Expected {expected}");
        }
    }

    [Test]
    public void PollAnyButtonPressed_AnalogUpAboveBindingThreshold_ReturnsAnalogUp()
    {
        var source = MakeSource(Connected(thumbLY: 20000));
        Assert.That(source.PollAnyButtonPressed(), Is.EqualTo("AnalogUp"));
    }

    [Test]
    public void PollAnyButtonPressed_AnalogDownAboveBindingThreshold_ReturnsAnalogDown()
    {
        var source = MakeSource(Connected(thumbLY: -20000));
        Assert.That(source.PollAnyButtonPressed(), Is.EqualTo("AnalogDown"));
    }

    [Test]
    public void PollAnyButtonPressed_AnalogLeftAboveBindingThreshold_ReturnsAnalogLeft()
    {
        var source = MakeSource(Connected(thumbLX: -20000));
        Assert.That(source.PollAnyButtonPressed(), Is.EqualTo("AnalogLeft"));
    }

    [Test]
    public void PollAnyButtonPressed_AnalogRightAboveBindingThreshold_ReturnsAnalogRight()
    {
        var source = MakeSource(Connected(thumbLX: 20000));
        Assert.That(source.PollAnyButtonPressed(), Is.EqualTo("AnalogRight"));
    }

    [Test]
    public void PollAnyButtonPressed_AnalogHeld_ReturnsNull()
    {
        var source = MakeSource(Connected(thumbLY: 20000));
        source.PollAnyButtonPressed(); // edge consumed
        Assert.That(source.PollAnyButtonPressed(), Is.Null);
    }

    [Test]
    public void PollAnyButtonPressed_AnalogBelowBindingThreshold_ReturnsNull()
    {
        var source = MakeSource(Connected(thumbLY: 10000));
        Assert.That(source.PollAnyButtonPressed(), Is.Null);
    }

    // ── AnyJustPressed ──────────────────────────────────────────────────────────

    [Test]
    public void AnyJustPressed_NotConnected_ReturnsFalse()
    {
        var source = MakeSource(NotConnected());
        Assert.That(source.AnyJustPressed(), Is.False);
    }

    [Test]
    public void AnyJustPressed_ButtonJustPressed_ReturnsTrue()
    {
        var source = MakeSource(Connected(a: true));
        Assert.That(source.AnyJustPressed(), Is.True);
    }

    [Test]
    public void AnyJustPressed_ButtonHeld_ReturnsFalse()
    {
        var source = MakeSource(Connected(a: true));
        source.AnyJustPressed(); // edge consumed
        Assert.That(source.AnyJustPressed(), Is.False);
    }

    [Test]
    public void AnyJustPressed_AnalogMovedAboveThreshold_ReturnsTrue()
    {
        var source = MakeSource(Connected(thumbLX: 10000));
        Assert.That(source.AnyJustPressed(), Is.True);
    }

    [Test]
    public void AnyJustPressed_AnalogHeld_ReturnsFalse()
    {
        var source = MakeSource(Connected(thumbLX: 10000));
        source.AnyJustPressed();
        Assert.That(source.AnyJustPressed(), Is.False);
    }

    [Test]
    public void AnyJustPressed_AnalogBelowThreshold_ReturnsFalse()
    {
        var source = MakeSource(Connected(thumbLX: 5000));
        Assert.That(source.AnyJustPressed(), Is.False);
    }

    [Test]
    public void AnyJustPressed_DisconnectWhileHolding_ReturnsFalse()
    {
        bool connected = true;
        var device = Substitute.For<IGamepadDevice>();
        device.GetState(Arg.Any<uint>()).Returns(_ => connected ? Connected(a: true) : NotConnected());
        var source = new SDL3GamepadSource(device);

        source.AnyJustPressed(); // first call: edge consumed
        connected = false;
        Assert.That(source.AnyJustPressed(), Is.False);
    }
}
