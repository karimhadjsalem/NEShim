using NEShim.Rendering;
using SDL3;

namespace NEShim.UI;

internal static class LogoRenderer
{
    private const float MarginFraction = 0.8f; // logo fits within 80% of the panel; 10% margin each side

    internal static void Draw(SDL3PaintContext ctx, SDL.Rect bounds, IntPtr logoSurface, float alpha)
    {
        ctx.Clear(new SDL.Color { R = 0, G = 0, B = 0, A = 255 });
        if (logoSurface == IntPtr.Zero) return;
        var (logoW, logoH) = SDL3PaintContext.GetSurfaceSize(logoSurface);
        var dest = ComputeDisplayRect(logoW, logoH, bounds);
        ctx.BlitSurfaceAlpha(logoSurface, dest, alpha);
    }

    internal static SDL.Rect ComputeDisplayRect(int imageW, int imageH, SDL.Rect bounds)
    {
        int maxW    = (int)(bounds.W * MarginFraction);
        int maxH    = (int)(bounds.H * MarginFraction);
        float scale = Math.Min((float)maxW / imageW, (float)maxH / imageH);
        int destW   = (int)(imageW * scale);
        int destH   = (int)(imageH * scale);
        int destX   = bounds.X + (bounds.W - destW) / 2;
        int destY   = bounds.Y + (bounds.H - destH) / 2;
        return new SDL.Rect { X = destX, Y = destY, W = destW, H = destH };
    }
}
