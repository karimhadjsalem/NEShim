using System.Drawing;
using System.Drawing.Drawing2D;
using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class GraphicsScalerTests
{
    // Uses a real Bitmap/Graphics — no I/O, no audio, safe to run headless.

    [Test]
    public void NearestNeighborScaler_Configure_SetsNearestNeighborAndHalf()
    {
        using var bmp = new Bitmap(1, 1);
        using var g   = Graphics.FromImage(bmp);

        new NearestNeighborScaler().Configure(g);

        Assert.That(g.InterpolationMode, Is.EqualTo(InterpolationMode.NearestNeighbor));
        Assert.That(g.PixelOffsetMode,   Is.EqualTo(PixelOffsetMode.Half));
    }

    [Test]
    public void BilinearScaler_Configure_SetsBilinearAndDefault()
    {
        using var bmp = new Bitmap(1, 1);
        using var g   = Graphics.FromImage(bmp);

        new BilinearScaler().Configure(g);

        Assert.That(g.InterpolationMode, Is.EqualTo(InterpolationMode.Bilinear));
        Assert.That(g.PixelOffsetMode,   Is.EqualTo(PixelOffsetMode.Default));
    }

    [Test]
    public void NearestNeighborScaler_And_BilinearScaler_HaveDifferentModes()
    {
        using var bmp = new Bitmap(1, 1);
        using var g   = Graphics.FromImage(bmp);

        new NearestNeighborScaler().Configure(g);
        var nearestMode = g.InterpolationMode;

        new BilinearScaler().Configure(g);
        var bilinearMode = g.InterpolationMode;

        Assert.That(nearestMode, Is.Not.EqualTo(bilinearMode));
    }
}
