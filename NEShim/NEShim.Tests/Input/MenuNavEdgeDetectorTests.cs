using NEShim.Input;

namespace NEShim.Tests.Input;

[TestFixture]
internal class MenuNavEdgeDetectorTests
{
    // ── Basic edge-triggering (debounce disabled) ────────────────────────────────

    [Test]
    public void Advance_FirstPress_ReturnsTrueForThatDirection()
    {
        var detector = new MenuNavEdgeDetector();
        var nav = detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        Assert.That(nav.Up, Is.True);
    }

    [Test]
    public void Advance_HeldSecondCall_ReturnsFalse()
    {
        var detector = new MenuNavEdgeDetector();
        detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        var nav = detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        Assert.That(nav.Up, Is.False);
    }

    [Test]
    public void Advance_ReleaseThenPressAgain_FiresAgain()
    {
        var detector = new MenuNavEdgeDetector();
        detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        detector.Advance(up: false, down: false, left: false, right: false, confirm: false, back: false);
        var nav = detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        Assert.That(nav.Up, Is.True);
    }

    [Test]
    public void Advance_AllSixDirections_EachEdgeTriggeredIndependently()
    {
        var detector = new MenuNavEdgeDetector();
        var nav = detector.Advance(up: true, down: true, left: true, right: true, confirm: true, back: true);

        Assert.That(nav.Up,      Is.True);
        Assert.That(nav.Down,    Is.True);
        Assert.That(nav.Left,    Is.True);
        Assert.That(nav.Right,   Is.True);
        Assert.That(nav.Confirm, Is.True);
        Assert.That(nav.Back,    Is.True);
    }

    [Test]
    public void Reset_ClearsPreviousState_HeldDirectionFiresAgainAfterReset()
    {
        var detector = new MenuNavEdgeDetector();
        detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        detector.Reset();
        var nav = detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);
        Assert.That(nav.Up, Is.True);
    }

    // ── Debounce (Windows XInput/DirectInput debounces at the driver level; SDL3's Linux
    // (evdev) gamepad backend does not, so genuine mechanical D-pad bounce can otherwise surface
    // as multiple real press/release transitions from a single physical tap) ──────

    [Test]
    public void Advance_RapidReleaseAndRepress_WithinDebounceWindow_SecondEdgeSuppressed()
    {
        long now = 0;
        var detector = new MenuNavEdgeDetector(debounceMs: 50) { NowProvider = () => now };

        var first = detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);
        detector.Advance(up: false, down: false, left: false, right: false, confirm: false, back: false); // bounce release
        now = 10; // within the debounce window
        var bounced = detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);

        Assert.That(first.Down, Is.True);
        Assert.That(bounced.Down, Is.False);
    }

    [Test]
    public void Advance_RepressAfterDebounceWindow_IsAccepted()
    {
        long now = 0;
        var detector = new MenuNavEdgeDetector(debounceMs: 50) { NowProvider = () => now };

        detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);
        detector.Advance(up: false, down: false, left: false, right: false, confirm: false, back: false);
        now = 200; // a deliberate, well-separated second press
        var secondPress = detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);

        Assert.That(secondPress.Down, Is.True);
    }

    [Test]
    public void Advance_Debounce_IsPerDirection_OtherDirectionNotBlocked()
    {
        long now = 0;
        var detector = new MenuNavEdgeDetector(debounceMs: 50) { NowProvider = () => now };

        detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);

        now = 5; // well within Down's debounce window
        var nav = detector.Advance(up: true, down: false, left: false, right: false, confirm: false, back: false);

        Assert.That(nav.Up, Is.True);
    }

    [Test]
    public void Advance_DebounceDisabled_ByDefault_RapidRepressAlwaysAccepted()
    {
        long now = 0;
        var detector = new MenuNavEdgeDetector { NowProvider = () => now }; // debounceMs defaults to 0

        detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);
        detector.Advance(up: false, down: false, left: false, right: false, confirm: false, back: false);
        var nav = detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);

        Assert.That(nav.Down, Is.True);
    }

    [Test]
    public void Advance_FirstPress_IsNeverDebounced_RegardlessOfClockStartValue()
    {
        // Guards the NeverFired sentinel: if lastFireTicks defaulted to 0 and the injected
        // clock also starts at/near 0, "now - lastFireTicks < debounceMs" would incorrectly
        // suppress the very first legitimate press.
        var detector = new MenuNavEdgeDetector(debounceMs: 50) { NowProvider = () => 0 };
        var nav = detector.Advance(up: false, down: true, left: false, right: false, confirm: false, back: false);
        Assert.That(nav.Down, Is.True);
    }
}
