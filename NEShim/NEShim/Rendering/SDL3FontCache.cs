using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Caches TTF_Font handles, opened lazily by (family, ptSize, bold, italic) key.
/// Each opened font gets CJK fallback fonts attached via TTF_AddFallbackFont so that
/// Japanese, Korean, and Simplified Chinese glyphs render correctly even when the
/// primary font (e.g. Segoe UI) does not cover those scripts.
/// Owns all handles — primary and fallback — and closes them in Dispose().
/// </summary>
internal sealed class SDL3FontCache : IDisposable
{
    private readonly Dictionary<(string family, float ptSize, bool bold, bool italic), IntPtr> _cache = new();
    private readonly List<IntPtr> _fallbackHandles = new();
    private bool _disposed;

    // Tried in order; first file found on disk is opened as a fallback.
    // Each covers a different script gap left by Western fonts (Segoe UI / Arial):
    //   Meiryo       — Japanese hiragana, katakana, kanji
    //   Malgun Gothic — Korean hangul
    //   Microsoft YaHei — Simplified Chinese
    // Fallback fonts for fonts that may not be present on all Windows installations.
    private static readonly string[] CjkFallbackFilenames =
    [
        "meiryo.ttc",    // Japanese
        "malgun.ttf",    // Korean
        "msyh.ttc",      // Simplified Chinese (Microsoft YaHei)
        "msgothic.ttc",  // Japanese fallback
        "gulim.ttc",     // Korean fallback
        "simsun.ttc",    // Simplified Chinese fallback
    ];

    internal SDL3FontCache()
    {
        TTF.Init();
    }

    internal IntPtr Get(string fontFamily, float ptSize, bool bold, bool italic = false)
    {
        var key = (fontFamily, ptSize, bold, italic);
        if (_cache.TryGetValue(key, out var handle))
            return handle;

        handle = OpenWithFallbacks(fontFamily, ptSize, bold, italic);
        _cache[key] = handle;
        return handle;
    }

    private IntPtr OpenWithFallbacks(string family, float ptSize, bool bold, bool italic)
    {
        string windowsFonts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

        // SDL_ttf defaults to 72 DPI. Multiply by 96/72 to match GDI+ pixel height,
        // then by 1.4 to compensate for FreeType's thinner strokes vs GDI+ ClearType.
        float effectivePt = ptSize * (96f / 72f) * 1.4f;

        IntPtr primary = OpenFirst(CandidateFilenames(family, bold, italic), windowsFonts, effectivePt);
        if (primary == IntPtr.Zero) return IntPtr.Zero;

        foreach (var filename in CjkFallbackFilenames)
        {
            string path = Path.Combine(windowsFonts, filename);
            if (!File.Exists(path)) continue;
            var fallback = TTF.OpenFont(path, effectivePt);
            if (fallback == IntPtr.Zero) continue;
            TTF.AddFallbackFont(primary, fallback);
            _fallbackHandles.Add(fallback);
        }

        return primary;
    }

    private static IntPtr OpenFirst(string[] filenames, string fontsDir, float effectivePt)
    {
        foreach (var filename in filenames)
        {
            string path = Path.Combine(fontsDir, filename);
            if (!File.Exists(path)) continue;
            var handle = TTF.OpenFont(path, effectivePt);
            if (handle != IntPtr.Zero) return handle;
        }
        return IntPtr.Zero;
    }

    internal static string[] CandidateFilenames(string family, bool bold, bool italic) =>
        family.ToLowerInvariant() switch
        {
            "segoe ui" => (bold, italic) switch
            {
                (true,  true)  => ["segoeuiz.ttf", "segoeuib.ttf"],
                (true,  false) => ["segoeuib.ttf"],
                (false, true)  => ["segoeuii.ttf", "segoeui.ttf"],
                (false, false) => ["segoeui.ttf"],
            },
            "arial" => (bold, italic) switch
            {
                (true,  true)  => ["arialbi.ttf", "arialbd.ttf"],
                (true,  false) => ["arialbd.ttf"],
                (false, true)  => ["ariali.ttf", "arial.ttf"],
                (false, false) => ["arial.ttf"],
            },
            _ => (bold, italic) switch
            {
                (true,  true)  => ["segoeuiz.ttf", "segoeuib.ttf", "arialbi.ttf", "arialbd.ttf"],
                (true,  false) => ["segoeuib.ttf", "arialbd.ttf"],
                (false, true)  => ["segoeuii.ttf", "segoeui.ttf", "ariali.ttf", "arial.ttf"],
                (false, false) => ["segoeui.ttf", "arial.ttf"],
            },
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var handle in _cache.Values)
            if (handle != IntPtr.Zero) TTF.CloseFont(handle);
        foreach (var handle in _fallbackHandles)
            if (handle != IntPtr.Zero) TTF.CloseFont(handle);
        _cache.Clear();
        _fallbackHandles.Clear();
        TTF.Quit();
    }
}
