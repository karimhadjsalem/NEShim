using System.Drawing;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class LogoRendererTests
{
    private static readonly Rectangle FullHdBounds = new(0, 0, 1920, 1080);
    private static readonly Rectangle HdBounds     = new(0, 0, 1280,  720);

    [Test]
    public void ComputeDisplayRect_LargeSquareImage_FitsWithinMarginBounds()
    {
        var rect = LogoRenderer.ComputeDisplayRect(new Size(4000, 4000), FullHdBounds);

        Assert.That(rect.Width,  Is.LessThanOrEqualTo((int)(FullHdBounds.Width  * 0.8f) + 1));
        Assert.That(rect.Height, Is.LessThanOrEqualTo((int)(FullHdBounds.Height * 0.8f) + 1));
    }

    [Test]
    public void ComputeDisplayRect_WideImage_ConstrainedByWidth()
    {
        var rect = LogoRenderer.ComputeDisplayRect(new Size(8000, 1000), FullHdBounds);

        Assert.That(rect.Width, Is.LessThanOrEqualTo((int)(FullHdBounds.Width * 0.8f) + 1));
    }

    [Test]
    public void ComputeDisplayRect_TallImage_ConstrainedByHeight()
    {
        var rect = LogoRenderer.ComputeDisplayRect(new Size(1000, 8000), FullHdBounds);

        Assert.That(rect.Height, Is.LessThanOrEqualTo((int)(FullHdBounds.Height * 0.8f) + 1));
    }

    [Test]
    public void ComputeDisplayRect_ResultIsCenteredInBounds()
    {
        var rect = LogoRenderer.ComputeDisplayRect(new Size(3840, 2160), FullHdBounds);

        int boundsCenter = FullHdBounds.Width / 2;
        int rectCenter   = rect.X + rect.Width / 2;
        Assert.That(rectCenter, Is.EqualTo(boundsCenter).Within(1));
    }

    [Test]
    public void ComputeDisplayRect_ResultStaysWithinMarginnedBounds()
    {
        var rect = LogoRenderer.ComputeDisplayRect(new Size(3840, 2160), FullHdBounds);

        int marginX = (int)(FullHdBounds.Width  * 0.1f);
        int marginY = (int)(FullHdBounds.Height * 0.1f);
        Assert.That(rect.Left,   Is.GreaterThanOrEqualTo(marginX - 1));
        Assert.That(rect.Top,    Is.GreaterThanOrEqualTo(marginY - 1));
        Assert.That(rect.Right,  Is.LessThanOrEqualTo(FullHdBounds.Width  - marginX + 1));
        Assert.That(rect.Bottom, Is.LessThanOrEqualTo(FullHdBounds.Height - marginY + 1));
    }

    [Test]
    public void ComputeDisplayRect_SmallImage_ScalesUpToFitMargin()
    {
        var rect = LogoRenderer.ComputeDisplayRect(new Size(100, 100), FullHdBounds);

        Assert.That(rect.Width,  Is.GreaterThan(100));
        Assert.That(rect.Height, Is.GreaterThan(100));
    }

    [Test]
    public void ComputeDisplayRect_PreservesAspectRatio()
    {
        var image = new Size(1920, 540); // 16:4.5 (wide)
        var rect  = LogoRenderer.ComputeDisplayRect(image, HdBounds);

        float imageAspect = (float)image.Width / image.Height;
        float rectAspect  = (float)rect.Width  / rect.Height;
        Assert.That(rectAspect, Is.EqualTo(imageAspect).Within(0.01f));
    }
}
