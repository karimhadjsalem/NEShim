using NEShim.Audio;

namespace NEShim.Tests.Audio;

[TestFixture]
internal class NesFilterProcessorTests
{
    private static NesFilterProcessor Create() => new();

    // ---- Interface contract ----

    [Test]
    public void Process_ReturnsTuple_WithLAndR()
    {
        var proc = Create();
        var (L, R) = proc.Process(10000);
        // Just verify we get a result; value is filter-dependent
        Assert.That(L, Is.EqualTo(R)); // NES is mono → both channels identical
    }

    [Test]
    public void Process_LargeInput_OutputIsNonZero()
    {
        var proc = Create();
        var (L, _) = proc.Process(10000);
        Assert.That(L, Is.Not.EqualTo(0));
    }

    [Test]
    public void Process_ZeroInput_OutputIsZero()
    {
        var proc = Create();
        var (L, R) = proc.Process(0);
        Assert.That(L, Is.EqualTo(0));
        Assert.That(R, Is.EqualTo(0));
    }

    // ---- DC blocking (high-pass behaviour) ----

    [Test]
    public void Process_ConstantInput_DecaysTowardZero()
    {
        // Drive HP filters with a constant signal; output should approach 0 over time.
        var proc = Create();

        // Warm up filter
        for (int i = 0; i < 500; i++)
            proc.Process(5000);

        short[] tail = new short[100];
        for (int i = 0; i < tail.Length; i++)
            tail[i] = proc.Process(5000).L;

        Assert.That(tail.All(v => Math.Abs(v) < 1000), Is.True,
            "Constant input should decay toward zero once HP filter settles");
    }

    // ---- AC signal pass-through ----

    [Test]
    public void Process_1kHzSine_OutputHasSubstantialAmplitude()
    {
        const float freq       = 1000f;
        const float sampleRate = 44100f;
        const int   warmup     = 200;
        const int   measure    = 100;

        var proc = Create();

        // Warmup
        for (int n = 0; n < warmup; n++)
        {
            short s = (short)(8000 * Math.Sin(2 * Math.PI * freq * n / sampleRate));
            proc.Process(s);
        }

        // Measure peak
        short peak = 0;
        for (int n = warmup; n < warmup + measure; n++)
        {
            short s = (short)(8000 * Math.Sin(2 * Math.PI * freq * n / sampleRate));
            short out1 = proc.Process(s).L;
            if (Math.Abs(out1) > Math.Abs(peak)) peak = out1;
        }

        Assert.That(Math.Abs(peak), Is.GreaterThan(4000),
            "1 kHz signal should pass through with reasonable amplitude");
    }

    // ---- ResetState ----

    [Test]
    public void ResetState_ClearsFilterMemory_SoNextSampleStartsFromZero()
    {
        var proc = Create();

        // Drive filter to a non-zero state
        for (int i = 0; i < 100; i++)
            proc.Process(5000);

        proc.ResetState();

        // After reset, a large constant input should produce the same output as a fresh processor
        var fresh = Create();
        const short input = 8000;
        var (resetL, _)  = proc.Process(input);
        var (freshL, _)  = fresh.Process(input);

        Assert.That(resetL, Is.EqualTo(freshL));
    }
}
