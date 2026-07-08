using System.Reflection;
using NEShim.Rendering;
using SDL3;

namespace NEShim.Localization;

/// <summary>
/// Loads flag PNG images from embedded resources at startup.
/// Images are expected at resource name "NEShim.Assets.flags.{code}.png".
/// Returns IntPtr.Zero gracefully when an image is not present — the Language screen
/// degrades to text-only for that row.
/// </summary>
internal static class FlagImageLoader
{
    private static readonly Assembly Assembly = typeof(FlagImageLoader).Assembly;

    // One entry per language in LanguageRegistry.AllLanguages (same order).
    // The Auto row has no flag; it is excluded here.
    private static readonly string[] ResourceKeys =
        LanguageRegistry.AllLanguages.Select(l => l.Code).ToArray();

    private static readonly IntPtr[] _images = Load();

    /// <summary>
    /// Returns the flag SDL surface for a language by its 1-based position in the Language screen
    /// (index 1 = first language, index N = last language). Index 0 (Auto) and indices
    /// beyond the language list return IntPtr.Zero.
    /// </summary>
    public static IntPtr Get(int index)
    {
        // index 0 = Auto (no flag); index 1..N maps to ResourceKeys[0..N-1]
        int resourceIndex = index - 1;
        if ((uint)resourceIndex >= (uint)_images.Length) return IntPtr.Zero;
        return _images[resourceIndex];
    }

    public static void Dispose()
    {
        for (int i = 0; i < _images.Length; i++)
        {
            if (_images[i] != IntPtr.Zero)
                SDL.DestroySurface(_images[i]);
            _images[i] = IntPtr.Zero;
        }
    }

    private static IntPtr[] Load()
    {
        var images = new IntPtr[ResourceKeys.Length];
        for (int i = 0; i < ResourceKeys.Length; i++)
        {
            string resourceName = $"NEShim.Assets.flags.{ResourceKeys[i]}.png";
            try
            {
                using var stream = Assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    Logger.Log($"[Localization] FlagImageLoader: resource '{resourceName}' not found — icon omitted.");
                    continue;
                }
                images[i] = SdlSurfaceLoader.LoadFromStream(stream);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Localization] FlagImageLoader: failed to load '{resourceName}': {ex.Message}");
            }
        }
        return images;
    }
}
