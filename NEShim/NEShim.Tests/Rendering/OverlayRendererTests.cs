using System.Drawing;
using System.Drawing.Imaging;
using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class OverlayRendererTests
{
    // ---- ComputeSidebarCover ----
    // ComputeSidebarCover(imageSize, dest) → (src, dst)
    // Scales so the image covers dest completely (cover mode), centred.

    [Test]
    public void ComputeSidebarCover_Dst_EqualsInputDest()
    {
        var dest = new Rectangle(10, 20, 200, 300);
        var (_, dst) = OverlayRenderer.ComputeSidebarCover(new Size(400, 600), dest);
        Assert.That(dst, Is.EqualTo(dest));
    }

    [Test]
    public void ComputeSidebarCover_ImageSameAsDest_SrcEqualsFullImage()
    {
        var size = new Size(200, 300);
        var dest = new Rectangle(0, 0, 200, 300);
        var (src, _) = OverlayRenderer.ComputeSidebarCover(size, dest);
        Assert.That(src.X, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Y, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Width,  Is.EqualTo(200f).Within(0.01f));
        Assert.That(src.Height, Is.EqualTo(300f).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_WiderImage_SrcWidthLessThanImageWidth()
    {
        // Image 400×200, dest 100×200 → scale by height (200/200=1.0), srcW = 100/1.0 = 100 < 400
        var (src, _) = OverlayRenderer.ComputeSidebarCover(new Size(400, 200), new Rectangle(0, 0, 100, 200));
        Assert.That(src.Width, Is.LessThan(400f));
    }

    [Test]
    public void ComputeSidebarCover_TallerImage_SrcHeightLessThanImageHeight()
    {
        // Image 200×400, dest 200×100 → scale by width (200/200=1.0), srcH = 100/1.0 = 100 < 400
        var (src, _) = OverlayRenderer.ComputeSidebarCover(new Size(200, 400), new Rectangle(0, 0, 200, 100));
        Assert.That(src.Height, Is.LessThan(400f));
    }

    [Test]
    public void ComputeSidebarCover_SrcRegion_IsCentred_WhenImageWiderThanDest()
    {
        // Image 400×200, dest 100×200 → scale = max(100/400, 200/200) = max(0.25, 1.0) = 1.0
        // srcW = 100/1.0=100, srcH=200/1.0=200; srcX=(400-100)/2=150, srcY=0
        var (src, _) = OverlayRenderer.ComputeSidebarCover(new Size(400, 200), new Rectangle(0, 0, 100, 200));
        Assert.That(src.X, Is.EqualTo(150f).Within(0.1f));
        Assert.That(src.Y, Is.EqualTo(0f).Within(0.1f));
    }

    [Test]
    public void ComputeSidebarCover_SrcRegion_IsCentred_WhenImageTallerThanDest()
    {
        // Image 200×400, dest 200×100 → scale = max(200/200, 100/400) = 1.0
        // srcW=200, srcH=100; srcX=0, srcY=(400-100)/2=150
        var (src, _) = OverlayRenderer.ComputeSidebarCover(new Size(200, 400), new Rectangle(0, 0, 200, 100));
        Assert.That(src.X, Is.EqualTo(0f).Within(0.1f));
        Assert.That(src.Y, Is.EqualTo(150f).Within(0.1f));
    }

    [Test]
    public void ComputeSidebarCover_ScaleByWidth_WhenDestIsWider()
    {
        // Image 100×200, dest 200×100 → need to scale up: scale = max(200/100, 100/200) = max(2,0.5) = 2
        // srcW=200/2=100, srcH=100/2=50; srcX=(100-100)/2=0, srcY=(200-50)/2=75
        var (src, _) = OverlayRenderer.ComputeSidebarCover(new Size(100, 200), new Rectangle(0, 0, 200, 100));
        Assert.That(src.Width,  Is.EqualTo(100f).Within(0.1f));
        Assert.That(src.Height, Is.EqualTo(50f).Within(0.1f));
        Assert.That(src.X,      Is.EqualTo(0f).Within(0.1f));
        Assert.That(src.Y,      Is.EqualTo(75f).Within(0.1f));
    }

    // ---- ToastDurationSeconds / AchievementDurationSeconds constants ----

    [Test]
    public void ToastDurationSeconds_IsPositive()
        => Assert.That(OverlayRenderer.ToastDurationSeconds, Is.GreaterThan(0));

    [Test]
    public void AchievementDurationSeconds_IsPositive()
        => Assert.That(OverlayRenderer.AchievementDurationSeconds, Is.GreaterThan(0));

    [Test]
    public void AchievementDurationSeconds_IsGreaterThanToast()
        => Assert.That(OverlayRenderer.AchievementDurationSeconds,
                       Is.GreaterThan(OverlayRenderer.ToastDurationSeconds));

    // ---- Draw* smoke tests — verify no exception on each rendering path ----

    private static (Bitmap bmp, Graphics g, Rectangle rect) MakeCanvas()
    {
        var bmp  = new Bitmap(640, 480, PixelFormat.Format32bppArgb);
        var g    = Graphics.FromImage(bmp);
        var rect = new Rectangle(0, 0, 640, 480);
        return (bmp, g, rect);
    }

    [Test]
    public void DrawFps_DoesNotThrow()
    {
        var (bmp, g, rect) = MakeCanvas();
        using (bmp) using (g)
            Assert.That(() => OverlayRenderer.DrawFps(g, rect, 60.0f), Throws.Nothing);
    }

    [Test]
    public void DrawFps_FractionalValue_DoesNotThrow()
    {
        var (bmp, g, rect) = MakeCanvas();
        using (bmp) using (g)
            Assert.That(() => OverlayRenderer.DrawFps(g, rect, 59.94f), Throws.Nothing);
    }

    [Test]
    public void DrawToast_ShortMessage_DoesNotThrow()
    {
        var (bmp, g, rect) = MakeCanvas();
        using (bmp) using (g)
            Assert.That(() => OverlayRenderer.DrawToast(g, rect, "Saved!"), Throws.Nothing);
    }

    [Test]
    public void DrawToast_LongMessage_DoesNotThrow()
    {
        var (bmp, g, rect) = MakeCanvas();
        using (bmp) using (g)
            Assert.That(() => OverlayRenderer.DrawToast(g, rect, "A rather long toast message for testing"), Throws.Nothing);
    }

    [Test]
    public void DrawAchievementNotification_DoesNotThrow()
    {
        var (bmp, g, rect) = MakeCanvas();
        using (bmp) using (g)
            Assert.That(() => OverlayRenderer.DrawAchievementNotification(g, rect, "Speed Runner"), Throws.Nothing);
    }

    [Test]
    public void DrawAchievementNotification_LongName_DoesNotThrow()
    {
        var (bmp, g, rect) = MakeCanvas();
        using (bmp) using (g)
            Assert.That(() => OverlayRenderer.DrawAchievementNotification(g, rect, "Completed the entire game without saving"), Throws.Nothing);
    }

    [Test]
    public void DrawSidebar_DoesNotThrow()
    {
        var (canvas, g, rect) = MakeCanvas();
        using var sidebar = new Bitmap(200, 400, PixelFormat.Format32bppArgb);
        using (canvas) using (g)
            Assert.That(() => OverlayRenderer.DrawSidebar(g, sidebar, rect), Throws.Nothing);
    }
}
