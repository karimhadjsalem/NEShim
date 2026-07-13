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

    /// <summary>
    /// Decoded multi-frame animation (GIF/WEBP/APNG). <see cref="Frames"/> surfaces are owned
    /// by <see cref="NativeAnimation"/> as a group — free them via <see cref="FreeAnimation"/>,
    /// never via <c>SDL.DestroySurface</c> individually.
    /// </summary>
    internal readonly struct AnimationHandle
    {
        internal IntPtr NativeAnimation { get; init; }
        internal IReadOnlyList<IntPtr> Frames { get; init; }
        internal IReadOnlyList<int> DelaysMs { get; init; }
    }

    /// <summary>
    /// Decodes an animated image (GIF/WEBP/APNG) via SDL3_image's animation loader. Returns
    /// null when the file isn't animation-capable (e.g. a plain PNG/JPG) — <c>Image.LoadAnimation</c>
    /// itself returns zero in that case, so no format sniffing is needed here.
    /// </summary>
    internal static AnimationHandle? LoadAnimationFromFile(string path)
    {
        try
        {
            IntPtr anim = Image.LoadAnimation(path);
            if (anim == IntPtr.Zero) return null;

            var native = Marshal.PtrToStructure<Image.Animation>(anim);
            if (native.Count <= 0)
            {
                Image.FreeAnimation(anim);
                return null;
            }

            var frames = new IntPtr[native.Count];
            Marshal.Copy(native.Frames, frames, 0, native.Count);
            var delays = new int[native.Count];
            Marshal.Copy(native.Delays, delays, 0, native.Count);

            return new AnimationHandle { NativeAnimation = anim, Frames = frames, DelaysMs = delays };
        }
        catch (Exception ex)
        {
            Logger.Log($"[SdlSurfaceLoader] Failed to load animation '{path}': {ex.Message}");
            return null;
        }
    }

    internal static void FreeAnimation(IntPtr nativeAnimation) => Image.FreeAnimation(nativeAnimation);
}
