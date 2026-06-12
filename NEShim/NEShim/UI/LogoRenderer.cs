using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace NEShim.UI;

internal static class LogoRenderer
{
    private const float MarginFraction = 0.8f; // logo fits within 80% of the panel; 10% margin each side

    internal static void Draw(Graphics g, Rectangle bounds, Bitmap logo, float alpha)
    {
        g.Clear(Color.Black);
        var dest = ComputeDisplayRect(logo.Size, bounds);
        g.CompositingMode   = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var cm = new ColorMatrix { Matrix33 = alpha };
        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(cm);
        g.DrawImage(logo, dest, 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, attrs);
    }

    internal static Rectangle ComputeDisplayRect(Size imageSize, Rectangle bounds)
    {
        int maxW  = (int)(bounds.Width  * MarginFraction);
        int maxH  = (int)(bounds.Height * MarginFraction);
        float scale = Math.Min((float)maxW / imageSize.Width, (float)maxH / imageSize.Height);
        int destW = (int)(imageSize.Width  * scale);
        int destH = (int)(imageSize.Height * scale);
        int destX = bounds.X + (bounds.Width  - destW) / 2;
        int destY = bounds.Y + (bounds.Height - destH) / 2;
        return new Rectangle(destX, destY, destW, destH);
    }
}
