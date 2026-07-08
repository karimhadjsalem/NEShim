using System.Drawing;
using System.Drawing.Imaging;
using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Converts PNG images (from files or embedded-resource streams) into SDL_Surface* handles
/// using System.Drawing.Bitmap as a decoder. Surfaces use PixelFormat.ARGB8888 (packed,
/// A=bits31-24 B=bits7-0), which on little-endian stores bytes [B,G,R,A] — matching
/// GDI+ Format32bppArgb and D3D11 B8G8R8A8_UNorm with no byte-swap needed.
/// </summary>
internal static class SdlSurfaceLoader
{
    internal static IntPtr LoadFromStream(Stream stream)
    {
        try
        {
            using var bitmap = new Bitmap(stream);
            return FromBitmap(bitmap);
        }
        catch (Exception ex)
        {
            Logger.Log($"[SdlSurfaceLoader] Failed to load from stream: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    internal static IntPtr LoadFromFile(string path)
    {
        try
        {
            using var bitmap = new Bitmap(path);
            return FromBitmap(bitmap);
        }
        catch (Exception ex)
        {
            Logger.Log($"[SdlSurfaceLoader] Failed to load '{path}': {ex.Message}");
            return IntPtr.Zero;
        }
    }

    private static unsafe IntPtr FromBitmap(Bitmap bmp)
    {
        Bitmap? converted = bmp.PixelFormat != PixelFormat.Format32bppArgb
            ? bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppArgb)
            : null;
        Bitmap source = converted ?? bmp;
        try
        {
            IntPtr surface = SDL.CreateSurface(source.Width, source.Height, SDL.PixelFormat.ARGB8888);
            if (surface == IntPtr.Zero) return IntPtr.Zero;

            var bitmapData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                SDL.Surface* surf = (SDL.Surface*)surface;
                if (surf->Pixels == IntPtr.Zero)
                {
                    SDL.DestroySurface(surface);
                    return IntPtr.Zero;
                }
                int sourceStride = Math.Abs(bitmapData.Stride);
                int destStride   = surf->Pitch;
                byte* dest = (byte*)surf->Pixels;
                byte* src  = (byte*)bitmapData.Scan0;
                for (int row = 0; row < source.Height; row++)
                    Buffer.MemoryCopy(src + row * sourceStride, dest + row * destStride, destStride, destStride);
            }
            finally
            {
                source.UnlockBits(bitmapData);
            }
            return surface;
        }
        finally
        {
            converted?.Dispose();
        }
    }
}
