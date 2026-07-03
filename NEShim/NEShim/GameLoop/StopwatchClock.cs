using System.Diagnostics;

namespace NEShim.GameLoop;

internal sealed class StopwatchClock : IClock
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();
    public long Frequency     => Stopwatch.Frequency;
}
