using NAudio.Wave;

namespace NEShim.Audio;

/// <summary>
/// Adapts <see cref="AudioFileReader"/> to <see cref="ILoopableSource"/> so
/// <see cref="LoopingSampleProvider"/> can seek back to the start without depending on
/// the concrete NAudio type.
/// </summary>
internal sealed class AudioFileReaderSource : ILoopableSource
{
    private readonly AudioFileReader _inner;

    public AudioFileReaderSource(AudioFileReader inner) => _inner = inner;

    public WaveFormat WaveFormat    => _inner.WaveFormat;
    public long       Length        => _inner.Length;
    public long       Position      { get => _inner.Position; set => _inner.Position = value; }

    public int Read(float[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);
}
