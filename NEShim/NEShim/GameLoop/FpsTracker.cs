namespace NEShim.GameLoop;

internal sealed class FpsTracker
{
    private readonly IClock _clock;
    private int  _frameCount;
    private long _windowStart;

    public float CurrentFps { get; private set; }

    public FpsTracker(IClock clock)
    {
        _clock       = clock;
        _windowStart = clock.GetTimestamp();
    }

    public void Tick()
    {
        _frameCount++;
        long now     = _clock.GetTimestamp();
        long elapsed = now - _windowStart;
        if (elapsed >= _clock.Frequency)
        {
            CurrentFps   = (float)_frameCount * _clock.Frequency / elapsed;
            _frameCount  = 0;
            _windowStart = now;
        }
    }
}
