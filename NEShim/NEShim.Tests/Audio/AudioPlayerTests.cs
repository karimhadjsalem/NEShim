using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class AudioPlayerTests
{
    // Uses bufferFrames=1 → capacity = max(1×800×2, 4096) = 4096 shorts
    private static AudioPlayer Create() => new AudioPlayer(bufferFrames: 1);

    private static byte[] ReadAll(AudioPlayer player, int shortCount)
    {
        byte[] buf = new byte[shortCount * 2];
        player.Read(buf, 0, buf.Length);
        return buf;
    }

    private static short[] ShortView(byte[] buf)
    {
        var result = new short[buf.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = (short)(buf[i * 2] | (buf[i * 2 + 1] << 8));
        return result;
    }

    // ---- WaveFormat ----

    [Test]
    public void WaveFormat_Is44100Hz_16Bit_Stereo()
    {
        using var player = Create();
        Assert.That(player.WaveFormat.SampleRate,  Is.EqualTo(44100));
        Assert.That(player.WaveFormat.BitsPerSample, Is.EqualTo(16));
        Assert.That(player.WaveFormat.Channels,    Is.EqualTo(2));
    }

    // ---- Empty buffer ----

    [Test]
    public void Read_EmptyBuffer_ReturnsSilence()
    {
        using var player = Create();
        byte[] buf = new byte[20];
        int returned = player.Read(buf, 0, 20);
        Assert.That(returned,             Is.EqualTo(20));
        Assert.That(buf, Is.All.EqualTo((byte)0));
    }

    [Test]
    public void Read_AlwaysReturnsRequestedByteCount()
    {
        using var player = Create();
        const int count = 48;
        byte[] buf = new byte[count];
        int returned = player.Read(buf, 0, count);
        Assert.That(returned, Is.EqualTo(count));
    }

    // ---- Enqueue / Read round-trip ----

    [Test]
    public void Enqueue_ThenRead_ReturnsDataLittleEndian()
    {
        using var player = Create();
        // 2 stereo shorts: L=100, R=200
        player.Enqueue(new short[] { 100, 200 }, sampleCount: 1);

        byte[] buf   = ReadAll(player, shortCount: 2);
        short[] data = ShortView(buf);

        Assert.That(data[0], Is.EqualTo(100));
        Assert.That(data[1], Is.EqualTo(200));
    }

    [Test]
    public void Read_PartialDrain_FillsRemainderWithSilence()
    {
        using var player = Create();
        // Enqueue 2 shorts, then ask for 4
        player.Enqueue(new short[] { 50, 60 }, sampleCount: 1);

        byte[] buf   = ReadAll(player, shortCount: 4);
        short[] data = ShortView(buf);

        Assert.That(data[0], Is.EqualTo(50));
        Assert.That(data[1], Is.EqualTo(60));
        Assert.That(data[2], Is.EqualTo(0)); // silence
        Assert.That(data[3], Is.EqualTo(0)); // silence
    }

    [Test]
    public void Enqueue_MultipleFrames_ReadInFifoOrder()
    {
        using var player = Create();
        player.Enqueue(new short[] { 1, 2 }, sampleCount: 1);
        player.Enqueue(new short[] { 3, 4 }, sampleCount: 1);

        short[] data = ShortView(ReadAll(player, shortCount: 4));

        Assert.That(data[0], Is.EqualTo(1));
        Assert.That(data[1], Is.EqualTo(2));
        Assert.That(data[2], Is.EqualTo(3));
        Assert.That(data[3], Is.EqualTo(4));
    }

    [Test]
    public void Enqueue_SampleCountClampsToArrayLength()
    {
        using var player = Create();
        // sampleCount×2 = 10 but array only has 2 shorts → clamps to 2
        player.Enqueue(new short[] { 77, 88 }, sampleCount: 5);

        short[] data = ShortView(ReadAll(player, shortCount: 4));
        Assert.That(data[0], Is.EqualTo(77));
        Assert.That(data[1], Is.EqualTo(88));
        Assert.That(data[2], Is.EqualTo(0)); // no more data
    }

    // ---- Overflow ----

    [Test]
    public void Enqueue_OverCapacity_DropsExcessData()
    {
        using var player = Create(); // capacity = 4096 shorts
        // Fill the buffer exactly with value 1
        var fillSamples = Enumerable.Repeat((short)1, 4096).ToArray();
        player.Enqueue(fillSamples, sampleCount: 2048); // 2048 × 2 = 4096 shorts

        // Try to add more with value 2 — should all be dropped
        player.Enqueue(new short[] { 2, 2 }, sampleCount: 1);

        // Read 4097 shorts — should get 4096 ones then silence (not twos)
        short[] data = ShortView(ReadAll(player, shortCount: 4097));
        Assert.That(data.Take(4096), Is.All.EqualTo((short)1));
        Assert.That(data[4096], Is.EqualTo(0)); // silence, not 2
    }

    // ---- Pause / resume ----

    [Test]
    public void SetPaused_True_ReadReturnsSilence()
    {
        using var player = Create();
        player.Enqueue(new short[] { 99, 99 }, sampleCount: 1);

        player.SetPaused(true);

        byte[] buf = ReadAll(player, shortCount: 2);
        Assert.That(buf, Is.All.EqualTo((byte)0));
    }

    [Test]
    public void SetPaused_True_DrainsBuffer_SoResumeStartsFresh()
    {
        using var player = Create();
        player.Enqueue(new short[] { 42, 42 }, sampleCount: 1);
        player.SetPaused(true);  // drains
        player.SetPaused(false); // resume

        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.EqualTo(0)); // old data is gone
    }

    [Test]
    public void SetPaused_False_AfterNewEnqueue_ReturnsNewData()
    {
        using var player = Create();
        player.SetPaused(true);
        player.SetPaused(false);

        player.Enqueue(new short[] { 55, 66 }, sampleCount: 1);

        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.EqualTo(55));
        Assert.That(data[1], Is.EqualTo(66));
    }

    // ---- Offset ----

    [Test]
    public void Read_WithOffset_WritesAtCorrectPosition()
    {
        using var player = Create();
        player.Enqueue(new short[] { 7, 8 }, sampleCount: 1);

        byte[] buf = new byte[10];
        player.Read(buf, offset: 2, count: 4); // write 2 shorts at offset 2

        short[] fromOffset = ShortView(buf[2..6]);
        Assert.That(fromOffset[0], Is.EqualTo(7));
        Assert.That(fromOffset[1], Is.EqualTo(8));

        // Bytes before offset are untouched
        Assert.That(buf[0], Is.EqualTo(0));
        Assert.That(buf[1], Is.EqualTo(0));
    }
}
