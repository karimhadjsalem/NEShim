using System.Drawing;
using System.Drawing.Drawing2D;

namespace NEShim.Rendering;

/// <summary>Pixel-perfect scaling — preserves hard pixel edges (NES original look).</summary>
internal sealed class NearestNeighborScaler : IGraphicsScaler
{
    public void Configure(Graphics g)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode   = PixelOffsetMode.Half;
    }
}
