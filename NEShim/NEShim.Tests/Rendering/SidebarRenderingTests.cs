using System.Drawing;
using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

/// <summary>
/// Tests for sidebar cover-scale rendering.
/// Uses real Bitmap/Graphics objects — no file I/O, no audio, safe to run headless.
/// </summary>
[TestFixture]
internal class SidebarRenderingTests
{
    // ---- ComputeSidebarCover: geometry unit tests ----

    [Test]
    public void ComputeSidebarCover_IdenticalSizes_FullImageSrcAndUnchangedDst()
    {
        var dest = new Rectangle(0, 0, 100, 200);
        var (src, dst) = GamePanel.ComputeSidebarCover(new Size(100, 200), dest);

        Assert.That(src.X,      Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Y,      Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Width,  Is.EqualTo(100f).Within(0.01f));
        Assert.That(src.Height, Is.EqualTo(200f).Within(0.01f));
        Assert.That(dst,        Is.EqualTo(dest));
    }

    [Test]
    public void ComputeSidebarCover_WideImageNarrowDest_CropsSidesNotTopBottom()
    {
        // 200×100 image → 100×100 dest: scale by height (scale=1), crop sides
        var (src, _) = GamePanel.ComputeSidebarCover(new Size(200, 100), new Rectangle(0, 0, 100, 100));

        Assert.That(src.Y,      Is.EqualTo(0f).Within(0.01f));   // no vertical crop
        Assert.That(src.Height, Is.EqualTo(100f).Within(0.01f)); // full image height used
        Assert.That(src.X,      Is.EqualTo(50f).Within(0.01f));  // 50px cropped from each side
        Assert.That(src.Width,  Is.EqualTo(100f).Within(0.01f)); // 100px wide center strip
    }

    [Test]
    public void ComputeSidebarCover_TallImageWideDest_CropsTopBottomNotSides()
    {
        // 100×200 image → 100×100 dest: scale by width (scale=1), crop top/bottom
        var (src, _) = GamePanel.ComputeSidebarCover(new Size(100, 200), new Rectangle(0, 0, 100, 100));

        Assert.That(src.X,      Is.EqualTo(0f).Within(0.01f));   // no horizontal crop
        Assert.That(src.Width,  Is.EqualTo(100f).Within(0.01f)); // full image width used
        Assert.That(src.Y,      Is.EqualTo(50f).Within(0.01f));  // 50px cropped from each side
        Assert.That(src.Height, Is.EqualTo(100f).Within(0.01f)); // 100px tall center strip
    }

    [Test]
    public void ComputeSidebarCover_SmallImageLargeDest_ScalesUpWithNoRemainder()
    {
        // 50×100 image → 100×200 dest: same aspect ratio, scale=2, no cropping
        var (src, _) = GamePanel.ComputeSidebarCover(new Size(50, 100), new Rectangle(0, 0, 100, 200));

        Assert.That(src.X,      Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Y,      Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Width,  Is.EqualTo(50f).Within(0.01f));
        Assert.That(src.Height, Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_SmallWideImageTallDest_ScalesUpAndCropsSides()
    {
        // 50×20 image → 100×200 dest: scale by height (200/20=10), srcW = 100/10 = 10, crop 20px each side
        var (src, _) = GamePanel.ComputeSidebarCover(new Size(50, 20), new Rectangle(0, 0, 100, 200));

        Assert.That(src.Width,  Is.EqualTo(10f).Within(0.01f));
        Assert.That(src.Height, Is.EqualTo(20f).Within(0.01f));
        Assert.That(src.X,      Is.EqualTo(20f).Within(0.01f)); // (50 - 10) / 2
        Assert.That(src.Y,      Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_DstAlwaysEqualsPassedDest()
    {
        var dest = new Rectangle(40, 0, 60, 480);
        var (_, dst) = GamePanel.ComputeSidebarCover(new Size(100, 200), dest);

        Assert.That(dst, Is.EqualTo(dest));
    }

    [Test]
    public void ComputeSidebarCover_SrcIsHorizontallyCenteredOnImage()
    {
        var imageSize = new Size(300, 100);
        var dest      = new Rectangle(0, 0, 100, 100);
        var (src, _)  = GamePanel.ComputeSidebarCover(imageSize, dest);

        float centerX = src.X + src.Width / 2f;
        Assert.That(centerX, Is.EqualTo(imageSize.Width / 2f).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_SrcIsVerticallyCenteredOnImage()
    {
        var imageSize = new Size(100, 300);
        var dest      = new Rectangle(0, 0, 100, 100);
        var (src, _)  = GamePanel.ComputeSidebarCover(imageSize, dest);

        float centerY = src.Y + src.Height / 2f;
        Assert.That(centerY, Is.EqualTo(imageSize.Height / 2f).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_SrcWidthFillsDest_AfterScaling()
    {
        // srcW * scale == destW for any input
        var imageSize = new Size(73, 150);
        var dest      = new Rectangle(0, 0, 48, 480);
        var (src, _)  = GamePanel.ComputeSidebarCover(imageSize, dest);

        float scale = Math.Max((float)dest.Width / imageSize.Width, (float)dest.Height / imageSize.Height);
        Assert.That(src.Width * scale, Is.EqualTo(dest.Width).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_SrcHeightFillsDest_AfterScaling()
    {
        var imageSize = new Size(73, 150);
        var dest      = new Rectangle(0, 0, 48, 480);
        var (src, _)  = GamePanel.ComputeSidebarCover(imageSize, dest);

        float scale = Math.Max((float)dest.Width / imageSize.Width, (float)dest.Height / imageSize.Height);
        Assert.That(src.Height * scale, Is.EqualTo(dest.Height).Within(0.01f));
    }

    [Test]
    public void ComputeSidebarCover_SrcStaysWithinImageBounds()
    {
        var imageSize = new Size(80, 200);
        var dest      = new Rectangle(0, 0, 48, 480);
        var (src, _)  = GamePanel.ComputeSidebarCover(imageSize, dest);

        Assert.That(src.X,              Is.GreaterThanOrEqualTo(0f));
        Assert.That(src.Y,              Is.GreaterThanOrEqualTo(0f));
        Assert.That(src.X + src.Width,  Is.LessThanOrEqualTo(imageSize.Width  + 0.01f));
        Assert.That(src.Y + src.Height, Is.LessThanOrEqualTo(imageSize.Height + 0.01f));
    }

    // ---- DrawSidebar: pixel integration tests ----

    private static Color GetPixel(Bitmap bmp, int x, int y) => bmp.GetPixel(x, y);

    private static Bitmap SolidBitmap(int width, int height, Color color)
    {
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }

    [Test]
    public void DrawSidebar_SolidImage_CoversEntireDestWithNoGaps()
    {
        // A solid red image drawn into a black canvas should leave no black pixels in the dest area.
        var dest = new Rectangle(10, 0, 50, 200);

        using var canvas = new Bitmap(dest.X + dest.Width, dest.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g      = Graphics.FromImage(canvas);
        g.Clear(Color.Black);

        using var sidebar = SolidBitmap(80, 300, Color.Red);
        GamePanel.DrawSidebar(g, sidebar, dest);

        // Check all four corners and center of the dest area
        foreach (var (x, y) in new[] {
            (dest.Left,        0),
            (dest.Right - 1,   0),
            (dest.Left,        dest.Bottom - 1),
            (dest.Right - 1,   dest.Bottom - 1),
            (dest.Left + dest.Width / 2, dest.Height / 2),
        })
        {
            var pixel = GetPixel(canvas, x, y);
            Assert.That(pixel.R, Is.GreaterThan(200), $"Expected red at ({x},{y}), got {pixel}");
            Assert.That(pixel.B, Is.LessThan(50),     $"Expected red at ({x},{y}), got {pixel}");
        }
    }

    [Test]
    public void DrawSidebar_WideImageNarrowDest_DrawsCenterRegion()
    {
        // Image: 300×100, left third blue | center third green | right third red.
        // Dest: 100×100 — scale=1, srcX=100, so exactly the green center strip is drawn.
        using var image = new Bitmap(300, 100, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image))
        {
            g.FillRectangle(Brushes.Blue,  0,   0, 100, 100);
            g.FillRectangle(Brushes.Green, 100, 0, 100, 100);
            g.FillRectangle(Brushes.Red,   200, 0, 100, 100);
        }

        var dest = new Rectangle(0, 0, 100, 100);
        using var canvas = new Bitmap(100, 100, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var cg     = Graphics.FromImage(canvas);
        cg.Clear(Color.Black);
        GamePanel.DrawSidebar(cg, image, dest);

        // Center pixel of dest should be green (from center strip of image)
        var center = GetPixel(canvas, 50, 50);
        Assert.That(center.G, Is.GreaterThan(100), $"Expected green at center, got {center}");
        Assert.That(center.R, Is.LessThan(50),     $"Expected green at center, got {center}");
        Assert.That(center.B, Is.LessThan(50),     $"Expected green at center, got {center}");
    }

    [Test]
    public void DrawSidebar_TallImageShortDest_DrawsCenterRegion()
    {
        // Image: 100×300, top third blue | center third green | bottom third red.
        // Dest: 100×100 — scale=1, srcY=100, so exactly the green center strip is drawn.
        using var image = new Bitmap(100, 300, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image))
        {
            g.FillRectangle(Brushes.Blue,  0, 0,   100, 100);
            g.FillRectangle(Brushes.Green, 0, 100, 100, 100);
            g.FillRectangle(Brushes.Red,   0, 200, 100, 100);
        }

        var dest = new Rectangle(0, 0, 100, 100);
        using var canvas = new Bitmap(100, 100, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var cg     = Graphics.FromImage(canvas);
        cg.Clear(Color.Black);
        GamePanel.DrawSidebar(cg, image, dest);

        var center = GetPixel(canvas, 50, 50);
        Assert.That(center.G, Is.GreaterThan(100), $"Expected green at center, got {center}");
        Assert.That(center.R, Is.LessThan(50),     $"Expected green at center, got {center}");
        Assert.That(center.B, Is.LessThan(50),     $"Expected green at center, got {center}");
    }

    [Test]
    public void DrawSidebar_SmallImage_ScalesUpToFillDest()
    {
        // A 10×10 red image drawn into a 100×100 dest should fill it completely.
        var dest = new Rectangle(0, 0, 100, 100);

        using var canvas  = new Bitmap(100, 100, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g       = Graphics.FromImage(canvas);
        g.Clear(Color.Black);

        using var sidebar = SolidBitmap(10, 10, Color.Red);
        GamePanel.DrawSidebar(g, sidebar, dest);

        foreach (var (x, y) in new[] { (0, 0), (99, 0), (0, 99), (99, 99), (50, 50) })
        {
            var pixel = GetPixel(canvas, x, y);
            Assert.That(pixel.R, Is.GreaterThan(200), $"Expected red at ({x},{y}), got {pixel}");
        }
    }
}
