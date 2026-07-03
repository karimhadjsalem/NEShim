using NAudio.Wave;

namespace NEShim.Audio;

/// <summary>
/// Minimal interface for an audio source that can be looped by seeking back to the start.
/// Implemented by AudioFileReaderSource, which wraps NAudio's AudioFileReader.
/// Extracted so LoopingSampleProvider can be unit tested with a fake source.
/// </summary>
internal interface ILoopableSource : ISampleProvider
{
    long Length   { get; }
    long Position { get; set; }
}
