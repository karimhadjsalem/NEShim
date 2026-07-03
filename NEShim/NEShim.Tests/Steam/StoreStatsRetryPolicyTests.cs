using NEShim.Steam;
using NUnit.Framework;

namespace NEShim.Tests.Steam;

[TestFixture]
public class StoreStatsRetryPolicyTests
{
    [Test]
    public void IsPending_InitiallyFalse()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 0);
        Assert.That(policy.IsPending, Is.False);
    }

    [Test]
    public void Schedule_SetsPendingTrue()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 0);
        policy.Schedule();
        Assert.That(policy.IsPending, Is.True);
    }

    [Test]
    public void Cancel_ClearsPendingFlag()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 0);
        policy.Schedule();
        policy.Cancel();
        Assert.That(policy.IsPending, Is.False);
    }

    [Test]
    public void Tick_WhenNotPending_DoesNotCallDelegate()
    {
        var policy    = new StoreStatsRetryPolicy(intervalTicks: 0);
        int callCount = 0;
        policy.Tick(() => { callCount++; return true; });
        Assert.That(callCount, Is.EqualTo(0));
    }

    [Test]
    public void Tick_WithZeroInterval_FiresDelegateImmediately()
    {
        var policy    = new StoreStatsRetryPolicy(intervalTicks: 0);
        int callCount = 0;
        policy.Schedule();
        policy.Tick(() => { callCount++; return true; });
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void Tick_WhenStoreSucceeds_ClearsPending()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 0);
        policy.Schedule();
        policy.Tick(() => true);
        Assert.That(policy.IsPending, Is.False);
    }

    [Test]
    public void Tick_WhenStoreFails_RemainsInPendingState()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 0);
        policy.Schedule();
        policy.Tick(() => false);
        Assert.That(policy.IsPending, Is.True);
    }

    [Test]
    public void Tick_WithCountdown_WaitsBeforeFiring()
    {
        var policy    = new StoreStatsRetryPolicy(intervalTicks: 2);
        int callCount = 0;
        policy.Schedule(); // _countdown = 2

        policy.Tick(() => { callCount++; return true; }); // countdown 2→1
        policy.Tick(() => { callCount++; return true; }); // countdown 1→0
        Assert.That(callCount, Is.EqualTo(0), "Should not fire during countdown");

        policy.Tick(() => { callCount++; return true; }); // fires
        Assert.That(callCount, Is.EqualTo(1));
        Assert.That(policy.IsPending, Is.False);
    }

    [Test]
    public void Tick_AfterFailedStore_ReschedulesCountdownForRetry()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 1);
        policy.Schedule(); // _countdown = 1

        policy.Tick(() => false); // countdown 1→0
        policy.Tick(() => false); // fires, fails → _countdown reset to 1
        Assert.That(policy.IsPending, Is.True);

        policy.Tick(() => false); // countdown 1→0
        int callCount = 0;
        policy.Tick(() => { callCount++; return true; }); // fires again
        Assert.That(callCount, Is.EqualTo(1));
        Assert.That(policy.IsPending, Is.False);
    }

    [Test]
    public void Cancel_AfterSuccessfulSchedule_PreventsNextFire()
    {
        var policy    = new StoreStatsRetryPolicy(intervalTicks: 0);
        int callCount = 0;
        policy.Schedule();
        policy.Cancel();
        policy.Tick(() => { callCount++; return true; });
        Assert.That(callCount, Is.EqualTo(0));
    }

    [Test]
    public void Schedule_CanBeCalledMultipleTimes_ResetsCountdown()
    {
        var policy = new StoreStatsRetryPolicy(intervalTicks: 3);
        policy.Schedule();
        policy.Tick(() => false); // countdown 3→2
        policy.Schedule();        // reset countdown back to 3
        int callCount = 0;
        // Should take 3 more ticks before firing (not 2)
        policy.Tick(() => { callCount++; return true; }); // 3→2
        policy.Tick(() => { callCount++; return true; }); // 2→1
        policy.Tick(() => { callCount++; return true; }); // 1→0
        Assert.That(callCount, Is.EqualTo(0));
        policy.Tick(() => { callCount++; return true; }); // fires
        Assert.That(callCount, Is.EqualTo(1));
    }
}
