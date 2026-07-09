using System.Runtime.InteropServices;
using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Converts PNG images (from files or embedded-resource streams) into SDL_Surface* handles
/// using SDL3_image (IMG_Load / IMG_Load_IO). SDL_image decodes the PNG and returns a
/// surface in native format; SDL.CreateTextureFromSurface converts as needed at blit time.
/// </summary>
internal static class SdlSurfaceLoader
{
    internal static IntPtr LoadFromStream(Stream stream)
    {
        try
        {
            byte[] buffer = ReadToBytes(stream);
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr io = SDL.IOFromConstMem(handle.AddrOfPinnedObject(), (UIntPtr)buffer.Length);
                if (io == IntPtr.Zero)
                {
                    Logger.Log($"[SdlSurfaceLoader] IOFromConstMem failed: {SDL.GetError()}");
                    return IntPtr.Zero;
                }
                IntPtr surface = Image.LoadIO(io, true);
                if (surface == IntPtr.Zero)
                    Logger.Log($"[SdlSurfaceLoader] LoadIO failed: {SDL.GetError()}");
                return surface;
            }
            finally
            {
                handle.Free();
            }
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
            IntPtr surface = Image.Load(path);
            if (surface == IntPtr.Zero)
                Logger.Log($"[SdlSurfaceLoader] Failed to load '{path}': {SDL.GetError()}");
            return surface;
        }
        catch (Exception ex)
        {
            Logger.Log($"[SdlSurfaceLoader] Failed to load '{path}': {ex.Message}");
            return IntPtr.Zero;
        }
    }

    private static byte[] ReadToBytes(Stream stream)
    {
        if (stream is MemoryStream ms) return ms.ToArray();
        using var tmp = new MemoryStream();
        stream.CopyTo(tmp);
        return tmp.ToArray();
    }
}
