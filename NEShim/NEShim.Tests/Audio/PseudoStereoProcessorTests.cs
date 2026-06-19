using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class PseudoStereoProcessorTests
{
    // Use a short delay so tests need few warmup samples.
    private const int TestSampleRate = 44100;
    private const int TestDelayMs    = 5;
    private static int DelayFrames   => TestSampleRate * TestDelayMs / 1000; // 220

    private static PseudoStereoProcessor CreateShort() =>
        new(sampleRate: TestSampleRate, delayMs: TestDelayMs);

    private static (short L, short R) RunAndGetLast(PseudoStereoProcessor proc, short input, int count)
    {
        (short L, short R) result = default;
        for (int i = 0; i < count; i++)
            result = proc.Process(input);
        return result;
    }

    // ---- Process ----

    [Test]
    public void Process_LargeInput_ReturnsNonZeroOutput()
    {
        var proc = CreateShort();
        var (L, R) = RunAndGetLast(proc, 10000, DelayFrames + 500);
        Assert.That(L, Is.Not.EqualTo(0));
        Assert.That(R, Is.Not.EqualTo(0));
    }

    [Test]
    public void Process_ZeroInput_ReturnsZero()
    {
        var proc = CreateShort();
        var (L, R) = RunAndGetLast(proc, 0, 1000);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }

    [Test]
    public void Process_BeforeDelayFilled_RChannelIsZero()
    {
        // Very first sample: ring is all zeros, so delayed = 0 → R = 0
        var proc = new PseudoStereoProcessor(sampleRate: TestSampleRate, delayMs: TestDelayMs);
        var (_, R) = proc.Process(8000);
        Assert.That(R, Is.EqualTo(0));
    }

    [Test]
    public void Process_AfterDelayFilled_LAndRDiffer()
    {
        var proc = CreateShort();
        // Alternating input so signal passes HP filters and L/R represent different moments
        (short L, short R) last = default;
        for (int i = 0; i < DelayFrames + 100; i++)
            last = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        Assert.That(last.L, Is.Not.EqualTo(last.R));
    }

    [Test]
    public void Process_LChannelLouder_ThanRChannel()
    {
        var proc = CreateShort();
        // Alternating signal passes HP; after warmup L (0.6) should generally exceed |R| (0.4)
        int louderCount = 0;
        for (int i = 0; i < DelayFrames + 500; i++)
        {
            var (L, R) = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
            if (i > DelayFrames && Math.Abs(L) > Math.Abs(R))
                louderCount++;
        }
        Assert.That(louderCount, Is.GreaterThan(200));
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsDelayBuffer()
    {
        var proc = CreateShort();
        RunAndGetLast(proc, 8000, DelayFrames + 50);
        proc.ResetState();
        var (_, R) = proc.Process(8000);
        Assert.That(R, Is.EqualTo(0)); // ring was zeroed, so delayed = 0
    }

    [Test]
    public void ResetState_ClearsFilterState()
    {
        var proc = CreateShort();
        RunAndGetLast(proc, 8000, 500);
        proc.ResetState();
        var (L, R) = proc.Process(0);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }

    // ---- Constructor edge case ----

    [Test]
    public void Constructor_ZeroDelay_BothChannelsAreNonZero()
    {
        var proc = new PseudoStereoProcessor(sampleRate: TestSampleRate, delayMs: 0);
        // delayMs=0 → ring length clamped to 1, so 1-sample delay
        // After a few samples with alternating input both channels should be nonzero
        (short L, short R) last = default;
        for (int i = 0; i < 10; i++)
            last = proc.Process(i % 2 == 0 ? (short)8000 : (short)-8000);
        Assert.That(last.L, Is.Not.EqualTo(0));
        Assert.That(last.R, Is.Not.EqualTo(0));
    }
}
