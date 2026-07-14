using NEShim.Platform;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class MenuScaleTests
{
    // Tests don't run on real Steam Deck hardware, so ComputeScale always exercises the
    // resolution-relative branch here — matches the existing PlatformDetectorTests convention.

    [Test]
    public void ComputeScale_AtReferenceResolution_ReturnsOne()
    {
        Assert.That(MenuScale.ComputeScale(1024, 672), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ComputeScale_DoubleReferenceResolution_ReturnsTwo()
    {
        Assert.That(MenuScale.ComputeScale(2048, 1344), Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void ComputeScale_HalfReferenceResolution_ReturnsHalf()
    {
        Assert.That(MenuScale.ComputeScale(512, 336), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ComputeScale_WiderThanReferenceAspect_ClampsToHeightRatio()
    {
        // 1920x672 is much wider than the 1024x672 reference but the same height — the height
        // ratio (1.0) is the binding constraint, not the much larger width ratio, so text
        // doesn't overflow a vertically-constrained window.
        Assert.That(MenuScale.ComputeScale(1920, 672), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ComputeScale_TallerThanReferenceAspect_ClampsToWidthRatio()
    {
        Assert.That(MenuScale.ComputeScale(1024, 2000), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ComputeScale_TypicalFullHdFullscreen_ScalesUpFromDefaultWindowedSize()
    {
        // The app's default windowed launch is 1024x672 (see Program.cs); a common 1920x1080
        // fullscreen target should scale text up rather than leaving it pinned to the same
        // absolute pixel size (the reported bug this class fixes).
        float scale = MenuScale.ComputeScale(1920, 1080);
        Assert.That(scale, Is.GreaterThan(1f));
    }

    [Test]
    public void ComputeScale_SmallerWindow_ScalesDownBelowOne()
    {
        Assert.That(MenuScale.ComputeScale(800, 600), Is.LessThan(1f));
    }

    // ---- UpdateViewport ----
    // MenuScale.Scale is a static, process-global mutable field with no reset hook, and every
    // other test file in the suite (e.g. GameCarouselRendererTests' ScaledPtSize assertions)
    // implicitly relies on it defaulting to 1.0. Calling UpdateViewport with the reference
    // resolution itself exercises the assignment without ever leaving Scale in a polluted,
    // non-1.0 state for tests that run afterward — see MenuScale's own doc comment.

    [Test]
    public void UpdateViewport_AtReferenceResolution_SetsScaleToOne()
    {
        MenuScale.UpdateViewport(1024, 672);

        Assert.That(MenuScale.Scale, Is.EqualTo(1f).Within(0.001f));
    }
}
