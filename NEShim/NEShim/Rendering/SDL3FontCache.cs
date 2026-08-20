using NEShim.Platform;
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
    private static readonly string[] WindowsCjkFallbackFilenames =
    [
        "meiryo.ttc",    // Japanese
        "malgun.ttf",    // Korean
        "msyh.ttc",      // Simplified Chinese (Microsoft YaHei)
        "msgothic.ttc",  // Japanese fallback
        "gulim.ttc",     // Korean fallback
        "simsun.ttc",    // Simplified Chinese fallback
    ];

    // Best-effort: Noto Sans CJK is packaged under several different file layouts across
    // distros (a single combined .ttc, or split per-region .otf files), unlike the Windows CJK
    // fonts above, which are always at fixed, well-known filenames. Every plausible packaging is
    // tried; any that aren't found on a given system are silently skipped, same as Windows.
    private static readonly string[] LinuxCjkFallbackFilenames =
    [
        "NotoSansCJK-Regular.ttc",
        "NotoSansCJKjp-Regular.otf",
        "NotoSansCJKkr-Regular.otf",
        "NotoSansCJKsc-Regular.otf",
        "NotoSansJP-Regular.otf",
        "NotoSansKR-Regular.otf",
        "NotoSansSC-Regular.otf",
    ];

    // No Windows-style fixed system font path exists on Linux — Arch-based distros (SteamOS)
    // and Debian-based ones (Ubuntu/WSL2) package fonts under different subdirectory layouts
    // entirely. Rather than guess at either, candidate filenames are searched for recursively
    // under these roots (see FindLinuxFontFile). DejaVu Sans ships even in minimal Ubuntu/WSL2
    // images; Liberation Sans is Valve's own metric-compatible Arial substitute and ships on
    // SteamOS — tried in this order regardless of the requested family, since the app only ever
    // requests "Segoe UI" or "Arial" and any reasonable UI sans-serif is an acceptable visual
    // substitute on Linux.
    private static readonly string[] LinuxFontSearchRoots =
    [
        "/usr/share/fonts",
        "/usr/local/share/fonts",
    ];

    // Wine (and therefore Proton) always maps the host filesystem root to a built-in Z:
    // drive. A stock Proton prefix's C:\windows\Fonts does not ship real Microsoft font
    // files (segoeui.ttf/arial.ttf are proprietary and not redistributed by Wine/Valve),
    // so OpenFirstWindows fails every candidate there — TTF.OpenFont never runs, every
    // DrawText call gets a Zero font handle and silently no-ops, and only non-text drawing
    // (panel background, selection highlight rects) shows up. Reusing the same Linux
    // font search via this drive gets real glyphs without needing winetricks corefonts.
    private static readonly string[] WineHostFontSearchRoots =
    [
        "Z:\\usr\\share\\fonts",
        "Z:\\usr\\local\\share\\fonts",
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

    // Applied uniformly on top of the 96/72 * 1.4 GDI+/FreeType conversion below, on every
    // platform: combined with MenuScale.Scale (which legitimately grows well past 1.0 at any
    // fullscreen resolution above the 1024x672 reference — see MenuScale's doc comment), the
    // uncorrected effective point size read as too large in side-by-side testing on both Windows
    // and native Linux at 1920x1080. Empirically tuned, not derived from font metrics — re-tune
    // if it still looks off at other resolutions.
    private const float FontSizeCorrection = 0.75f;

    private IntPtr OpenWithFallbacks(string family, float ptSize, bool bold, bool italic)
    {
        // SDL_ttf defaults to 72 DPI. Multiply by 96/72 to match GDI+ pixel height,
        // then by 1.4 to compensate for FreeType's thinner strokes vs GDI+ ClearType.
        float effectivePt = ptSize * (96f / 72f) * 1.4f * FontSizeCorrection;

        // OperatingSystem.IsWindows() is true under Proton too — it's a real Windows PE,
        // just translated by Wine. PlatformDetector.IsWine narrows that down to the actual
        // Proton case, where real Microsoft font files usually aren't present under
        // C:\windows\Fonts (see WineHostFontSearchRoots doc comment above).
        bool isWine = OperatingSystem.IsWindows() && PlatformDetector.IsWine;

        IntPtr primary = OperatingSystem.IsWindows()
            ? OpenFirstWindows(CandidateFilenames(family, bold, italic), effectivePt)
            : OpenFirstFromRoots(LinuxSansCandidateFilenames(bold, italic), LinuxFontSearchRoots, effectivePt);
        if (primary == IntPtr.Zero && isWine)
            primary = OpenFirstFromRoots(LinuxSansCandidateFilenames(bold, italic), WineHostFontSearchRoots, effectivePt);
        if (primary == IntPtr.Zero) return IntPtr.Zero;

        if (OperatingSystem.IsWindows())
        {
            foreach (var filename in WindowsCjkFallbackFilenames)
                AddFallbackIfFound(primary, WindowsFontPath(filename), effectivePt);
        }
        if (!OperatingSystem.IsWindows() || isWine)
        {
            var roots = isWine ? WineHostFontSearchRoots : LinuxFontSearchRoots;
            foreach (var filename in LinuxCjkFallbackFilenames)
                AddFallbackIfFound(primary, FindFontFile(filename, roots), effectivePt);
        }

        return primary;
    }

    private void AddFallbackIfFound(IntPtr primary, string? path, float effectivePt)
    {
        if (path is null || !File.Exists(path)) return;
        var fallback = TTF.OpenFont(path, effectivePt);
        if (fallback == IntPtr.Zero) return;
        TTF.AddFallbackFont(primary, fallback);
        _fallbackHandles.Add(fallback);
    }

    private static string WindowsFontPath(string filename) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", filename);

    private static IntPtr OpenFirstWindows(string[] filenames, float effectivePt)
    {
        foreach (var filename in filenames)
        {
            string path = WindowsFontPath(filename);
            if (!File.Exists(path)) continue;
            var handle = TTF.OpenFont(path, effectivePt);
            if (handle != IntPtr.Zero) return handle;
        }
        return IntPtr.Zero;
    }

    private static IntPtr OpenFirstFromRoots(string[] filenames, string[] roots, float effectivePt)
    {
        foreach (var filename in filenames)
        {
            string? path = FindFontFile(filename, roots);
            if (path is null) continue;
            var handle = TTF.OpenFont(path, effectivePt);
            if (handle != IntPtr.Zero) return handle;
        }
        return IntPtr.Zero;
    }

    private static string? FindFontFile(string filename, string[] roots)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                string? match = Directory.EnumerateFiles(root, filename, SearchOption.AllDirectories).FirstOrDefault();
                if (match is not null) return match;
            }
            catch (UnauthorizedAccessException) { } // unreadable subdirectory — skip it, try the next root
        }
        return null;
    }

    internal static string[] LinuxSansCandidateFilenames(bool bold, bool italic) => (bold, italic) switch
    {
        (true,  true)  => ["DejaVuSans-BoldOblique.ttf", "LiberationSans-BoldItalic.ttf"],
        (true,  false) => ["DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf"],
        (false, true)  => ["DejaVuSans-Oblique.ttf", "LiberationSans-Italic.ttf"],
        (false, false) => ["DejaVuSans.ttf", "LiberationSans-Regular.ttf"],
    };

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
