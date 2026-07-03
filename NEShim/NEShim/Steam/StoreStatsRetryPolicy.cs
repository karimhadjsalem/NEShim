namespace NEShim.Steam;

/// <summary>
/// Manages a single-pending store-stats retry with a fixed countdown interval.
/// UI-thread-only; no synchronisation is applied internally.
/// </summary>
internal sealed class StoreStatsRetryPolicy
{
    private readonly int _intervalTicks;
    private bool _pending;
    private int  _countdown;

    public bool IsPending => _pending;

    public StoreStatsRetryPolicy(int intervalTicks)
    {
        _intervalTicks = intervalTicks;
    }

    /// <summary>Marks a store as pending and starts the countdown before the first retry.</summary>
    public void Schedule()
    {
        _pending   = true;
        _countdown = _intervalTicks;
    }

    /// <summary>Clears the pending flag (call when a successful store callback arrives).</summary>
    public void Cancel() => _pending = false;

    /// <summary>
    /// Decrements the countdown or fires <paramref name="storeStats"/> when it reaches zero.
    /// If the store returns false the countdown resets for the next attempt.
    /// No-op when nothing is pending.
    /// </summary>
    public void Tick(Func<bool> storeStats)
    {
        if (!_pending) return;
        if (_countdown > 0) { _countdown--; return; }

        if (storeStats())
            _pending = false;
        else
            _countdown = _intervalTicks;
    }
}
