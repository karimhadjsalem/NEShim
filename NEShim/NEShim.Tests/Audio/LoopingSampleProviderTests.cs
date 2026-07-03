using NAudio.Wave;
using NEShim.Audio;
using NUnit.Framework;

namespace NEShim.Tests.Audio;

[TestFixture]
internal sealed class LoopingSampleProviderTests
{
    // Minimal ILoopableSource that returns a fixed sequence of samples and supports seek.
    private sealed class FakeSource : ILoopableSource
    {
        private readonly float[] _samples;
        private int _position; // in samples (floats), not bytes

        public WaveFormat WaveFormat { get; }
        // Length and Position in bytes (matching NAudio convention)
        public long Length   => _samples.Length * sizeof(float);
        public long Position
        {
            get => _position * sizeof(float);
            set => _position = (int)(value / sizeof(float));
        }

        public FakeSource(float[] samples)
        {
            _samples = samples;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int remaining = _samples.Length - _position;
            int toRead    = Math.Min(count, remaining);
            if (toRead <= 0) return 0;
            Array.Copy(_samples, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }
    }

    [Test]
    public void WaveFormat_MatchesSource()
    {
        var source   = new FakeSource(new float[8]);
        var provider = new LoopingSampleProvider(source);

        Assert.That(provider.WaveFormat, Is.EqualTo(source.WaveFormat));
    }

    [Test]
    public void Read_WithinSinglePass_ReturnsSamplesUnchanged()
    {
        var samples  = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var source   = new FakeSource(samples);
        var provider = new LoopingSampleProvider(source);
        var buffer   = new float[4];

        int read = provider.Read(buffer, 0, 4);

        Assert.That(read, Is.EqualTo(4));
        Assert.That(buffer, Is.EqualTo(samples));
    }

    [Test]
    public void Read_PastEndOfSource_SeeksBackAndContinues()
    {
        // 4-sample source, ask for 6 samples → should read 4, loop, read 2 more
        var samples  = new float[] { 1f, 2f, 3f, 4f };
        var source   = new FakeSource(samples);
        var provider = new LoopingSampleProvider(source);
        var buffer   = new float[6];

        int read = provider.Read(buffer, 0, 6);

        Assert.That(read, Is.EqualTo(6));
        Assert.That(buffer[0], Is.EqualTo(1f));
        Assert.That(buffer[1], Is.EqualTo(2f));
        Assert.That(buffer[2], Is.EqualTo(3f));
        Assert.That(buffer[3], Is.EqualTo(4f));
        Assert.That(buffer[4], Is.EqualTo(1f)); // looped
        Assert.That(buffer[5], Is.EqualTo(2f)); // looped
    }

    [Test]
    public void Read_ExactlyAtEnd_LoopsCleanly()
    {
        // Read exactly the source length, then read again — should loop back to start
        var samples  = new float[] { 0.5f, 0.6f };
        var source   = new FakeSource(samples);
        var provider = new LoopingSampleProvider(source);
        var buffer   = new float[4];

        // First call: reads 2, hits EOF, seeks back, reads 2 more
        int read = provider.Read(buffer, 0, 4);

        Assert.That(read, Is.EqualTo(4));
        Assert.That(buffer[0], Is.EqualTo(0.5f));
        Assert.That(buffer[1], Is.EqualTo(0.6f));
        Assert.That(buffer[2], Is.EqualTo(0.5f));
        Assert.That(buffer[3], Is.EqualTo(0.6f));
    }

    [Test]
    public void Read_EmptySource_ReturnsZeroWithoutHanging()
    {
        // Length==0 guard: should not loop forever on an empty source
        var source   = new FakeSource(Array.Empty<float>());
        var provider = new LoopingSampleProvider(source);
        var buffer   = new float[4];

        int read = provider.Read(buffer, 0, 4);

        Assert.That(read, Is.EqualTo(0));
    }

    [Test]
    public void Read_WithOffset_WritesToCorrectBufferPosition()
    {
        var samples  = new float[] { 7f, 8f };
        var source   = new FakeSource(samples);
        var provider = new LoopingSampleProvider(source);
        var buffer   = new float[5];

        int read = provider.Read(buffer, offset: 2, count: 2);

        Assert.That(read, Is.EqualTo(2));
        Assert.That(buffer[0], Is.EqualTo(0f)); // untouched
        Assert.That(buffer[1], Is.EqualTo(0f)); // untouched
        Assert.That(buffer[2], Is.EqualTo(7f));
        Assert.That(buffer[3], Is.EqualTo(8f));
        Assert.That(buffer[4], Is.EqualTo(0f)); // untouched
    }

    [Test]
    public void Read_MultipleLoops_PositionResetEachTime()
    {
        // Three full source loops: 3 × 2 samples = 6
        var samples  = new float[] { 1f, 2f };
        var source   = new FakeSource(samples);
        var provider = new LoopingSampleProvider(source);
        var buffer   = new float[6];

        provider.Read(buffer, 0, 6);

        Assert.That(buffer, Is.EqualTo(new float[] { 1f, 2f, 1f, 2f, 1f, 2f }));
    }
}
