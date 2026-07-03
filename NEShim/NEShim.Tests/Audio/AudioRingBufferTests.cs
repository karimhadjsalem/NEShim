using NEShim.Audio;
using NUnit.Framework;

namespace NEShim.Tests.Audio;

[TestFixture]
public class AudioRingBufferTests
{
    [Test]
    public void NewBuffer_AvailableIsZero()
    {
        var buffer = new AudioRingBuffer(100);
        Assert.That(buffer.Available, Is.EqualTo(0));
    }

    [Test]
    public void NewBuffer_CapacityMatchesConstructorArgument()
    {
        var buffer = new AudioRingBuffer(64);
        Assert.That(buffer.Capacity, Is.EqualTo(64));
    }

    [Test]
    public void Enqueue_IncreasesAvailableByCount()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Enqueue(new short[] { 1, 2, 3, 4 }, 4);
        Assert.That(buffer.Available, Is.EqualTo(4));
    }

    [Test]
    public void TryDequeueMonoL_WhenEmpty_ReturnsFalse()
    {
        var buffer = new AudioRingBuffer(100);
        bool result = buffer.TryDequeueMonoL(out _);
        Assert.That(result, Is.False);
    }

    [Test]
    public void TryDequeueMonoL_WhenOnlyOneSampleAvailable_ReturnsFalse()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Enqueue(new short[] { 10 }, 1);
        bool result = buffer.TryDequeueMonoL(out _);
        Assert.That(result, Is.False);
    }

    [Test]
    public void TryDequeueMonoL_ReturnsTrueAndReadsLeftSample()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Enqueue(new short[] { 10, 20 }, 2); // L=10, R=20
        bool result = buffer.TryDequeueMonoL(out short sample);
        Assert.That(result, Is.True);
        Assert.That(sample, Is.EqualTo(10));
    }

    [Test]
    public void TryDequeueMonoL_ConsumesStereoPair_ReducesAvailableByTwo()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Enqueue(new short[] { 10, 20, 30, 40 }, 4);
        buffer.TryDequeueMonoL(out _);
        Assert.That(buffer.Available, Is.EqualTo(2));
    }

    [Test]
    public void TryDequeueMonoL_ReturnsConsecutiveLeftSamples()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Enqueue(new short[] { 1, 2, 3, 4 }, 4); // pairs: (1,2), (3,4)
        buffer.TryDequeueMonoL(out short first);
        buffer.TryDequeueMonoL(out short second);
        Assert.That(first,  Is.EqualTo(1));
        Assert.That(second, Is.EqualTo(3));
    }

    [Test]
    public void Enqueue_WhenFull_DropsExcessSamples()
    {
        var buffer = new AudioRingBuffer(4);
        buffer.Enqueue(new short[] { 1, 2, 3, 4 }, 4);
        buffer.Enqueue(new short[] { 5, 6 }, 2); // should be dropped — buffer is full
        Assert.That(buffer.Available, Is.EqualTo(4));
    }

    [Test]
    public void Drain_SetsAvailableToZero()
    {
        var buffer = new AudioRingBuffer(100);
        buffer.Enqueue(new short[] { 1, 2, 3, 4 }, 4);
        buffer.Drain();
        Assert.That(buffer.Available, Is.EqualTo(0));
    }

    [Test]
    public void Drain_AllowsSubsequentEnqueueToStartFresh()
    {
        var buffer = new AudioRingBuffer(4);
        buffer.Enqueue(new short[] { 1, 2, 3, 4 }, 4);
        buffer.Drain();
        buffer.Enqueue(new short[] { 5, 6 }, 2);
        buffer.TryDequeueMonoL(out short sample);
        Assert.That(sample, Is.EqualTo(5));
    }

    [Test]
    public void WrapAround_HandledCorrectly()
    {
        // capacity=4: fill to 4, consume one pair (advances readPos), then add 2 more (wraps writePos)
        var buffer = new AudioRingBuffer(4);
        buffer.Enqueue(new short[] { 1, 2, 3, 4 }, 4);   // buffer: [1,2,3,4] writePos=0 readPos=0
        buffer.TryDequeueMonoL(out _);                     // consumes (1,2); readPos=2, available=2
        buffer.Enqueue(new short[] { 5, 6 }, 2);           // writes [5,6] at indices 0,1 (wrap); available=4

        buffer.TryDequeueMonoL(out short second); // reads index 2 → 3, skips index 3 → 4
        buffer.TryDequeueMonoL(out short third);  // reads index 0 → 5 (wrapped), skips index 1 → 6

        Assert.That(second, Is.EqualTo(3));
        Assert.That(third,  Is.EqualTo(5));
    }

    [Test]
    public void Enqueue_PartialCountRespected()
    {
        var buffer = new AudioRingBuffer(100);
        // Array has 6 elements but we only enqueue 2
        buffer.Enqueue(new short[] { 10, 20, 30, 40, 50, 60 }, 2);
        Assert.That(buffer.Available, Is.EqualTo(2));
        buffer.TryDequeueMonoL(out short sample);
        Assert.That(sample, Is.EqualTo(10));
    }
}
