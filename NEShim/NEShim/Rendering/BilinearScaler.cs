using System.Drawing;
using System.Drawing.Drawing2D;

namespace NEShim.Rendering;

/// <summary>
/// Subtle bilinear filtering — softens pixel edges slightly without the halos of bicubic.
/// Appropriate for NES pixel art on modern high-DPI displays.
/// </summary>
internal sealed class BilinearScaler : IGraphicsScaler
{
    public void Configure(Graphics g)
    {
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.PixelOffsetMode   = PixelOffsetMode.Default;
    }
}
