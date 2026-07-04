namespace NEShim.Emulation;

internal readonly struct FrameData
{
    public readonly int[]   VideoBuffer;
    public readonly int     BufferWidth;
    public readonly int     BufferHeight;
    public readonly short[] AudioSamples;
    public readonly int     SampleCount;

    public FrameData(int[] videoBuffer, int bufferWidth, int bufferHeight,
                     short[] audioSamples, int sampleCount)
    {
        VideoBuffer  = videoBuffer;
        BufferWidth  = bufferWidth;
        BufferHeight = bufferHeight;
        AudioSamples = audioSamples;
        SampleCount  = sampleCount;
    }
}
