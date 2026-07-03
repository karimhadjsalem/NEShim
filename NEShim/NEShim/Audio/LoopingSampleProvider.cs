using NAudio.Wave;

namespace NEShim.Audio;

/// <summary>
/// Wraps an <see cref="ILoopableSource"/> and loops it infinitely by seeking back to the
/// start each time the source is exhausted.  Looping at the sample level avoids calling
/// Play() from a WaveOut callback thread.
/// </summary>
internal sealed class LoopingSampleProvider : ISampleProvider
{
    private readonly ILoopableSource _source;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public LoopingSampleProvider(ILoopableSource source) => _source = source;

    public int Read(float[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = _source.Read(buffer, offset + totalRead, count - totalRead);
            if (read > 0)
            {
                totalRead += read;
            }
            else
            {
                // End of file — seek to start and loop
                if (_source.Length == 0) break; // guard against empty file
                _source.Position = 0;
            }
        }
        return totalRead;
    }
}
