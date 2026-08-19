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

    // ---- ComputeDownscaledSize ----

    [Test]
    public void ComputeDownscaledSize_WithinBounds_ReturnsUnchanged()
    {
        var (w, h) = AnimatedImagePlayer.ComputeDownscaledSize(800, 600, maxWidth: 1920, maxHeight: 1080);
        Assert.That((w, h), Is.EqualTo((800, 600)));
    }

    [Test]
    public void ComputeDownscaledSize_ExceedsBounds_ScalesDownPreservingAspectRatio()
    {
        var (w, h) = AnimatedImagePlayer.ComputeDownscaledSize(3840, 2160, maxWidth: 1920, maxHeight: 1080);
        Assert.That((w, h), Is.EqualTo((1920, 1080)));
    }

    [Test]
    public void ComputeDownscaledSize_NeverUpscales()
    {
        var (w, h) = AnimatedImagePlayer.ComputeDownscaledSize(100, 100, maxWidth: 1920, maxHeight: 1080);
        Assert.That((w, h), Is.EqualTo((100, 100)));
    }

    [Test]
    public void ComputeDownscaledSize_WidthIsBindingConstraint_HeightShrinksProportionally()
    {
        // A very wide source is bound by width, not height.
        var (w, h) = AnimatedImagePlayer.ComputeDownscaledSize(4000, 100, maxWidth: 2000, maxHeight: 1080);
        Assert.That(w, Is.EqualTo(2000));
        Assert.That(h, Is.EqualTo(50));
    }

    // ---- NeedsDownscale ----

    [Test]
    public void NeedsDownscale_AllFramesWithinBounds_ReturnsFalse()
    {
        var sizes = new[] { (1920, 1080), (1920, 1080) };
        Assert.That(AnimatedImagePlayer.NeedsDownscale(sizes, maxWidth: 1920, maxHeight: 1080), Is.False);
    }

    [Test]
    public void NeedsDownscale_OneFrameExceedsBounds_ReturnsTrue()
    {
        var sizes = new[] { (1920, 1080), (3840, 2160) };
        Assert.That(AnimatedImagePlayer.NeedsDownscale(sizes, maxWidth: 1920, maxHeight: 1080), Is.True);
    }

    [Test]
    public void NeedsDownscale_EmptyFrameList_ReturnsFalse()
    {
        Assert.That(AnimatedImagePlayer.NeedsDownscale(Array.Empty<(int, int)>(), maxWidth: 1920, maxHeight: 1080), Is.False);
    }
}
