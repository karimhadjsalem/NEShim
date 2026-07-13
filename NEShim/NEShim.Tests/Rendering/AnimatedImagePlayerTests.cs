using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class AnimatedImagePlayerTests
{
    // ---- ComputeFrameIndex ----

    [Test]
    public void ComputeFrameIndex_SingleFrame_AlwaysReturnsZero()
    {
        var delays = new[] { 100 };
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(0, delays), Is.EqualTo(0));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(5000, delays), Is.EqualTo(0));
    }

    [Test]
    public void ComputeFrameIndex_MultiFrame_ReturnsCorrectFrameAtEachBoundary()
    {
        var delays = new[] { 100, 200, 300 }; // cumulative: [0,100) -> 0, [100,300) -> 1, [300,600) -> 2

        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(0, delays), Is.EqualTo(0));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(99, delays), Is.EqualTo(0));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(100, delays), Is.EqualTo(1));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(299, delays), Is.EqualTo(1));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(300, delays), Is.EqualTo(2));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(599, delays), Is.EqualTo(2));
    }

    [Test]
    public void ComputeFrameIndex_ElapsedBeyondTotalDuration_WrapsViaModulo()
    {
        var delays = new[] { 100, 200, 300 }; // total 600
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(600, delays), Is.EqualTo(0));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(700, delays), Is.EqualTo(1));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(1200, delays), Is.EqualTo(0));
    }

    [Test]
    public void ComputeFrameIndex_ZeroOrNegativeDelay_FlooredToOneMs_NoInfiniteLoop()
    {
        var delays = new[] { 0, -5, 10 }; // treated as [1, 1, 10] => total 12
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(0, delays), Is.EqualTo(0));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(1, delays), Is.EqualTo(1));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(2, delays), Is.EqualTo(2));
        Assert.That(AnimatedImagePlayer.ComputeFrameIndex(11, delays), Is.EqualTo(2));
    }
}
