using NEShim.Config;
using NEShim.Input;
using NEShim.Input.Sources;

namespace NEShim.Tests.Input.Sources;

[TestFixture]
internal class XInputSourceTests
{
    private AppConfig _config = null!;

    [SetUp]
    public void SetUp() => _config = new AppConfig(); // GamepadDeadzone=8000, AnalogStickMode="Cardinal"

    private static XInputHelper.GamepadState NotConnected() => default;

    private static XInputHelper.GamepadState Connected(
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

    // ── IsAvailable ─────────────────────────────────────────────────────────────

    [Test]
    public void IsAvailable_WhenNotConnected_ReturnsFalse()
    {
        var source = new XInputSource(() => NotConnected());
        source.GetActiveIdentifiers(_config);
        Assert.That(source.IsAvailable, Is.False);
    }

    [Test]
    public void IsAvailable_WhenConnected_ReturnsTrue()
    {
        var source = new XInputSource(() => Connected());
        source.GetActiveIdentifiers(_config);
        Assert.That(source.IsAvailable, Is.True);
    }

    // ── Digital buttons ─────────────────────────────────────────────────────────

    [Test]
    public void GetActiveIdentifiers_NotConnected_ReturnsEmpty()
    {
        var source = new XInputSource(() => NotConnected());
        Assert.That(source.GetActiveIdentifiers(_config), Is.Empty);
    }

    [Test]
    public void GetActiveIdentifiers_DPadUp_IncludesDPadUp()
    {
        var source = new XInputSource(() => Connected(dpadUp: true));
        Assert.That(source.GetActiveIdentifiers(_config), Contains.Item("DPadUp"));
    }

    [Test]
    public void GetActiveIdentifiers_AllDigitalButtons_AllIncluded()
    {
        var source = new XInputSource(() => Connected(
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
        var source = new XInputSource(() => Connected(thumbLY: 10000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("AnalogUp"));
        Assert.That(ids, Does.Not.Contain("AnalogLeft"));
        Assert.That(ids, Does.Not.Contain("AnalogRight"));
    }

    [Test]
    public void GetActiveIdentifiers_CardinalMode_XDominant_AnalogUpSuppressed()
    {
        // X=15000 > Y=10000 → right axis dominates; AnalogUp is suppressed
        var source = new XInputSource(() => Connected(thumbLX: 15000, thumbLY: 10000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Does.Not.Contain("AnalogUp"));
        Assert.That(ids, Contains.Item("AnalogRight"));
    }

    [Test]
    public void GetActiveIdentifiers_BelowDeadzone_NoAnalogIdentifier()
    {
        var source = new XInputSource(() => Connected(thumbLX: 1000, thumbLY: 1000));
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
        var source = new XInputSource(() => Connected(thumbLX: 10000, thumbLY: 10000));
        var ids = source.GetActiveIdentifiers(_config);

        Assert.That(ids, Contains.Item("AnalogUp"));
        Assert.That(ids, Contains.Item("AnalogRight"));
    }

    // ── Menu nav edge detection ─────────────────────────────────────────────────

    [Test]
    public void GetMenuNav_DPadUp_OnFirstPress_ReturnsUp()
    {
        var source = new XInputSource(() => Connected(dpadUp: true));
        var nav = source.GetMenuNav(_config);
        Assert.That(nav.Up, Is.True);
    }

    [Test]
    public void GetMenuNav_DPadUp_HeldSecondFrame_ReturnsFalse()
    {
        var source = new XInputSource(() => Connected(dpadUp: true));
        source.GetMenuNav(_config); // frame 1: edge consumed
        var nav = source.GetMenuNav(_config); // frame 2: held
        Assert.That(nav.Up, Is.False);
    }

    [Test]
    public void GetMenuNav_NotConnected_ReturnsDefault()
    {
        var source = new XInputSource(() => NotConnected());
        Assert.That(source.GetMenuNav(_config).Any, Is.False);
    }

    [Test]
    public void GetMenuNav_Confirm_A_EdgeTriggered()
    {
        var source = new XInputSource(() => Connected(a: true));
        var nav = source.GetMenuNav(_config);
        Assert.That(nav.Confirm, Is.True);
    }

    [Test]
    public void GetMenuNav_Back_B_EdgeTriggered()
    {
        var source = new XInputSource(() => Connected(b: true));
        var nav = source.GetMenuNav(_config);
        Assert.That(nav.Back, Is.True);
    }
}
