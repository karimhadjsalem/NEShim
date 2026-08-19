using SDL3;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class LogoRendererTests
{
    private static readonly SDL.Rect FullHdBounds = new() { X = 0, Y = 0, W = 1920, H = 1080 };
    private static readonly SDL.Rect HdBounds     = new() { X = 0, Y = 0, W = 1280, H = 720  };

    [Test]
    public void ComputeDisplayRect_LargeSquareImage_FitsWithinMarginBounds()
    {
        var rect = LogoRenderer.ComputeDisplayRect(4000, 4000, FullHdBounds);

        Assert.That(rect.W, Is.LessThanOrEqualTo((int)(FullHdBounds.W * 0.8f) + 1));
        Assert.That(rect.H, Is.LessThanOrEqualTo((int)(FullHdBounds.H * 0.8f) + 1));
    }

    [Test]
    public void ComputeDisplayRect_WideImage_ConstrainedByWidth()
    {
        var rect = LogoRenderer.ComputeDisplayRect(8000, 1000, FullHdBounds);

        Assert.That(rect.W, Is.LessThanOrEqualTo((int)(FullHdBounds.W * 0.8f) + 1));
    }

    [Test]
    public void ComputeDisplayRect_TallImage_ConstrainedByHeight()
    {
        var rect = LogoRenderer.ComputeDisplayRect(1000, 8000, FullHdBounds);

        Assert.That(rect.H, Is.LessThanOrEqualTo((int)(FullHdBounds.H * 0.8f) + 1));
    }

    [Test]
    public void ComputeDisplayRect_ResultIsCenteredInBounds()
    {
        var rect = LogoRenderer.ComputeDisplayRect(3840, 2160, FullHdBounds);

        int boundsCenter = FullHdBounds.W / 2;
        int rectCenter   = rect.X + rect.W / 2;
        Assert.That(rectCenter, Is.EqualTo(boundsCenter).Within(1));
    }

    [Test]
    public void ComputeDisplayRect_ResultStaysWithinMarginnedBounds()
    {
        var rect = LogoRenderer.ComputeDisplayRect(3840, 2160, FullHdBounds);

        int marginX = (int)(FullHdBounds.W * 0.1f);
        int marginY = (int)(FullHdBounds.H * 0.1f);
        Assert.That(rect.X,          Is.GreaterThanOrEqualTo(marginX - 1));
        Assert.That(rect.Y,          Is.GreaterThanOrEqualTo(marginY - 1));
        Assert.That(rect.X + rect.W, Is.LessThanOrEqualTo(FullHdBounds.W - marginX + 1));
        Assert.That(rect.Y + rect.H, Is.LessThanOrEqualTo(FullHdBounds.H - marginY + 1));
    }

    [Test]
    public void ComputeDisplayRect_SmallImage_ScalesUpToFitMargin()
    {
        var rect = LogoRenderer.ComputeDisplayRect(100, 100, FullHdBounds);

        Assert.That(rect.W, Is.GreaterThan(100));
        Assert.That(rect.H, Is.GreaterThan(100));
    }

    [Test]
    public void ComputeDisplayRect_PreservesAspectRatio()
    {
        const int imageW = 1920, imageH = 540; // 16:4.5 (wide)
        var rect = LogoRenderer.ComputeDisplayRect(imageW, imageH, HdBounds);

        float imageAspect = (float)imageW / imageH;
        float rectAspect  = (float)rect.W  / rect.H;
        Assert.That(rectAspect, Is.EqualTo(imageAspect).Within(0.01f));
    }
}
