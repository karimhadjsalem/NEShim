using NEShim.GameLoop;
using NUnit.Framework;

namespace NEShim.Tests.GameLoop;

[TestFixture]
public class FpsTrackerTests
{
    private sealed class FakeClock : IClock
    {
        public long CurrentTimestamp;
        public long GetTimestamp() => CurrentTimestamp;
        public long Frequency { get; set; } = 1000; // 1000 ticks per second
    }

    [Test]
    public void CurrentFps_InitiallyZero()
    {
        var tracker = new FpsTracker(new FakeClock());
        Assert.That(tracker.CurrentFps, Is.EqualTo(0f));
    }

    [Test]
    public void Tick_BeforeWindowElapses_DoesNotUpdateFps()
    {
        var clock   = new FakeClock { Frequency = 1000 };
        var tracker = new FpsTracker(clock);
        clock.CurrentTimestamp = 999; // one tick short of one second
        tracker.Tick();
        Assert.That(tracker.CurrentFps, Is.EqualTo(0f));
    }

    [Test]
    public void Tick_WhenWindowElapses_UpdatesFps()
    {
        var clock   = new FakeClock { Frequency = 1000 };
        var tracker = new FpsTracker(clock);

        // 3 frames recorded at t=0, then a 4th frame at t=1500 triggers the window update.
        tracker.Tick();
        tracker.Tick();
        tracker.Tick();
        clock.CurrentTimestamp = 1500;
        tracker.Tick();

        // 4 frames in 1500 ticks → 4 * 1000 / 1500 ≈ 2.667
        float expected = 4f * 1000f / 1500f;
        Assert.That(tracker.CurrentFps, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void Tick_AfterWindowUpdate_ResetsFrameCountForNextWindow()
    {
        var clock   = new FakeClock { Frequency = 1000 };
        var tracker = new FpsTracker(clock);

        // First window: 1 frame at exactly 1 second → 1.0 fps
        clock.CurrentTimestamp = 1000;
        tracker.Tick();
        Assert.That(tracker.CurrentFps, Is.EqualTo(1f).Within(0.0001f));

        // Second window: 1 frame at t=2000 (1 second later) → 1.0 fps
        clock.CurrentTimestamp = 2000;
        tracker.Tick();
        Assert.That(tracker.CurrentFps, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void Tick_SixtyFramesInOneSecond_ReportsSixtyFps()
    {
        var clock   = new FakeClock { Frequency = 1000 };
        var tracker = new FpsTracker(clock);

        // 59 frames at t=0 (no window update yet), then the 60th at t=1000 triggers it.
        for (int i = 0; i < 59; i++) tracker.Tick();
        clock.CurrentTimestamp = 1000;
        tracker.Tick();

        Assert.That(tracker.CurrentFps, Is.EqualTo(60f).Within(0.0001f));
    }

    [Test]
    public void Tick_ExactlyAtBoundary_TriggersUpdate()
    {
        var clock   = new FakeClock { Frequency = 500 };
        var tracker = new FpsTracker(clock);
        clock.CurrentTimestamp = 500; // elapsed == Frequency exactly
        tracker.Tick();
        Assert.That(tracker.CurrentFps, Is.GreaterThan(0f));
    }
}
