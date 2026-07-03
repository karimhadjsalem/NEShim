namespace NEShim.GameLoop;

internal interface IClock
{
    long GetTimestamp();
    long Frequency { get; }
}
