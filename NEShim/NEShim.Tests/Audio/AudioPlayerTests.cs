using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class AudioPlayerTests
{
    // bufferFrames=1 → capacity = max(1×800×2, 4096) = 4096 shorts
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
        Assert.That(player.WaveFormat.SampleRate,   Is.EqualTo(44100));
        Assert.That(player.WaveFormat.BitsPerSample, Is.EqualTo(16));
        Assert.That(player.WaveFormat.Channels,     Is.EqualTo(2));
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
    // Note: the NES hardware output filter chain (HP@37Hz, HP@39Hz, LP@14kHz) is applied
    // inside Read(), so output values differ from raw ring-buffer values.
    // Tests verify structural behavior (non-zero, silence, ordering, stereo) rather than
    // exact post-filter values.

    [Test]
    public void Enqueue_LargeValue_ThenRead_OutputIsNonZero()
    {
        using var player = Create();
        // Use a large value so the filter output is clearly non-zero.
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.Not.EqualTo(0)); // filtered L value
        Assert.That(data[1], Is.Not.EqualTo(0)); // filtered R value (same)
    }

    [Test]
    public void Enqueue_ThenRead_LAndRChannels_AreEqual()
    {
        // NES is mono: L is filtered, R is a copy — both must match.
        using var player = Create();
        player.Enqueue(new short[] { 8000, 8000 }, sampleCount: 1);
        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.EqualTo(data[1]));
    }

    [Test]
    public void Read_PartialDrain_FillsRemainderWithSilence()
    {
        using var player = Create();
        player.Enqueue(new short[] { 8000, 8000 }, sampleCount: 1); // 1 stereo pair

        // Request 4 shorts: first 2 have filtered data, last 2 are silence
        short[] data = ShortView(ReadAll(player, shortCount: 4));
        Assert.That(data[0], Is.Not.EqualTo(0)); // data present
        Assert.That(data[2], Is.EqualTo(0));     // silence
        Assert.That(data[3], Is.EqualTo(0));     // silence
    }

    [Test]
    public void Enqueue_MultipleFrames_ReadInFifoOrder()
    {
        // First frame: silence (zeros). Second frame: large values.
        // FIFO order means the silent pair appears first in output.
        using var player = Create();
        player.Enqueue(new short[] { 0, 0 },         sampleCount: 1); // frame 1 — silence
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1); // frame 2 — signal

        short[] data = ShortView(ReadAll(player, shortCount: 4));
        // Frame 1 (zeros): filter(0) from zero state = 0
        Assert.That(data[0], Is.EqualTo(0));
        Assert.That(data[1], Is.EqualTo(0));
        // Frame 2 (10000): non-zero after filter
        Assert.That(data[2], Is.Not.EqualTo(0));
        Assert.That(data[3], Is.Not.EqualTo(0));
    }

    [Test]
    public void Enqueue_SampleCountClampsToArrayLength()
    {
        using var player = Create();
        // sampleCount×2 = 10 but array has only 2 shorts → clamped to 2
        player.Enqueue(new short[] { 8000, 8000 }, sampleCount: 5);

        short[] data = ShortView(ReadAll(player, shortCount: 4));
        Assert.That(data[0], Is.Not.EqualTo(0)); // 2 shorts were stored
        Assert.That(data[2], Is.EqualTo(0));     // nothing beyond the 2 stored shorts
        Assert.That(data[3], Is.EqualTo(0));
    }

    // ---- Overflow ----

    [Test]
    public void Enqueue_OverCapacity_DropsExcessData()
    {
        using var player = Create(); // capacity = 4096 shorts

        // Fill with zeros (filter(0)=0, so output is verifiably zero)
        player.Enqueue(new short[4096], sampleCount: 2048);

        // Overflow: try to add a large non-zero value — must be silently dropped
        player.Enqueue(new short[] { 20000, 20000 }, sampleCount: 1);

        // Read 4097 shorts: all from zeros (0), plus 1 silence (0)
        // The overflow value (20000) must never appear
        short[] data = ShortView(ReadAll(player, shortCount: 4097));
        Assert.That(data, Is.All.EqualTo((short)0));
    }

    // ---- Filter behaviour ----

    [Test]
    public void OutputFilter_BlocksDcBias()
    {
        // A constant input drives the high-pass filters to zero over time.
        // After many samples of the same value, the output should decay toward 0.
        using var player = Create();

        // Pump 500 identical samples (enough for HP to charge up and then settle)
        var constantData = Enumerable.Repeat((short)5000, 1000).ToArray();
        player.Enqueue(constantData, sampleCount: 500);

        // Read and discard first 900 shorts (let filter settle)
        ReadAll(player, shortCount: 900);

        // The last 100 shorts should be close to zero (HP has removed the DC)
        player.Enqueue(constantData, sampleCount: 500);
        ReadAll(player, shortCount: 900);
        short[] tail = ShortView(ReadAll(player, shortCount: 100));
        Assert.That(tail.All(v => Math.Abs(v) < 1000), Is.True,
            "Constant input should have near-zero output once HP filter settles");
    }

    [Test]
    public void OutputFilter_PreservesLowFrequencyAcSignals()
    {
        // A low-frequency AC signal (below HP cutoff is fine above) should pass through.
        // We use 1 kHz (well within the pass band of all three filters).
        using var player = Create();
        const float frequency = 1000f;
        const float sampleRate = 44100f;
        const int   warmupCount = 200; // warm up filter with 100 stereo pairs
        const int   measureCount = 100;

        var samples = new short[(warmupCount + measureCount) * 2];
        for (int n = 0; n < warmupCount + measureCount; n++)
        {
            short val = (short)(8000 * Math.Sin(2 * Math.PI * frequency * n / sampleRate));
            samples[n * 2]     = val;
            samples[n * 2 + 1] = val;
        }
        player.Enqueue(samples, sampleCount: warmupCount + measureCount);

        // Discard warmup
        ReadAll(player, shortCount: warmupCount * 2);

        // Measure: output should still have substantial amplitude (> 4000 peak)
        short[] measured = ShortView(ReadAll(player, shortCount: measureCount * 2));
        short peak = measured.Select(Math.Abs).Max();
        Assert.That(peak, Is.GreaterThan(4000),
            "1 kHz signal should pass through the filter chain with reasonable amplitude");
    }

    // ---- Pause / resume ----

    [Test]
    public void SetPaused_True_ReadReturnsSilence()
    {
        using var player = Create();
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        player.SetPaused(true);
        byte[] buf = ReadAll(player, shortCount: 2);
        Assert.That(buf, Is.All.EqualTo((byte)0));
    }

    [Test]
    public void SetPaused_True_DrainsBuffer_SoResumeStartsFromSilence()
    {
        using var player = Create();
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        player.SetPaused(true);  // drains
        player.SetPaused(false); // resume

        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.EqualTo(0)); // old data is gone
    }

    [Test]
    public void SetPaused_False_AfterNewEnqueue_OutputIsNonZero()
    {
        using var player = Create();
        player.SetPaused(true);
        player.SetPaused(false);
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);

        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.Not.EqualTo(0));
    }

    // ---- Offset ----

    [Test]
    public void Read_WithOffset_WritesAtCorrectBytePosition()
    {
        using var player = Create();
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);

        byte[] buf = new byte[10];
        player.Read(buf, offset: 2, count: 4); // 2 shorts starting at byte 2

        // Bytes 0-1 (before the offset) must be untouched
        Assert.That(buf[0], Is.EqualTo(0));
        Assert.That(buf[1], Is.EqualTo(0));

        // Bytes 2-5 should hold the filtered non-zero stereo pair
        short[] fromOffset = ShortView(buf[2..6]);
        Assert.That(fromOffset[0], Is.Not.EqualTo(0));
    }

    // ---- SetVolume ----

    [Test]
    public void SetVolume_Zero_ReadReturnsSilence()
    {
        using var player = Create();
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        player.SetVolume(0f);
        byte[] buf = ReadAll(player, shortCount: 2);
        Assert.That(buf, Is.All.EqualTo((byte)0));
    }

    [Test]
    public void SetVolume_Half_ReducesAmplitude()
    {
        // With full volume the filtered output is some value X; at 0.5 it should be ~X/2.
        using var full = Create();
        using var half = Create();

        full.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        half.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        half.SetVolume(0.5f);

        short[] fullData = ShortView(ReadAll(full, shortCount: 2));
        short[] halfData = ShortView(ReadAll(half, shortCount: 2));

        // Half volume should be strictly less in magnitude than full volume
        Assert.That(Math.Abs(halfData[0]), Is.LessThan(Math.Abs(fullData[0])));
    }

    [Test]
    public void SetVolume_Full_MatchesDefaultOutput()
    {
        using var def  = Create(); // default volume is 1.0
        using var full = Create();
        full.SetVolume(1.0f);

        def.Enqueue(new short[]  { 10000, 10000 }, sampleCount: 1);
        full.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);

        short[] defData  = ShortView(ReadAll(def,  shortCount: 2));
        short[] fullData = ShortView(ReadAll(full, shortCount: 2));
        Assert.That(defData[0], Is.EqualTo(fullData[0]));
    }

    // ---- SetProcessor ----

    [Test]
    public void SetProcessor_SoundScrubber_OutputIsNonZero()
    {
        using var player = new AudioPlayer(bufferFrames: 1, new SoundScrubberProcessor());
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        short[] data = ShortView(ReadAll(player, shortCount: 2));
        Assert.That(data[0], Is.Not.EqualTo(0));
    }

    [Test]
    public void SetProcessor_SwapMidBuffer_DoesNotCrash()
    {
        using var player = Create();
        player.Enqueue(new short[] { 10000, 10000 }, sampleCount: 1);
        player.SetProcessor(new SoundScrubberProcessor()); // swap before read
        // Should read without throwing; output may differ but must be valid
        Assert.DoesNotThrow(() => ReadAll(player, shortCount: 2));
    }

    [Test]
    public void SetProcessor_ResetsProcessorState_ToAvoidPop()
    {
        // After SetProcessor, the first sample should match a fresh processor,
        // not carry over state from whatever was running before.
        using var player = Create();

        // Drive the default processor to a non-zero state
        for (int i = 0; i < 100; i++)
            player.Enqueue(new short[] { 5000, 5000 }, sampleCount: 1);
        ReadAll(player, shortCount: 200);

        // Swap to a fresh scrubber (state reset should happen inside SetProcessor)
        var scrubber = new SoundScrubberProcessor();
        player.SetProcessor(scrubber);

        // The scrubber's first output should match what a standalone fresh scrubber produces
        var reference = new SoundScrubberProcessor();
        var (refL, _) = reference.Process(8000);

        player.Enqueue(new short[] { 8000, 8000 }, sampleCount: 1);
        short[] data = ShortView(ReadAll(player, shortCount: 2));

        Assert.That(data[0], Is.EqualTo(refL));
    }
}
