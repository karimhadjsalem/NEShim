using System.Globalization;

namespace NEShim.Localization;

/// <summary>
/// Central registry of all UI languages the app supports.
/// Order here determines order in the Language settings screen.
/// </summary>
internal static class LanguageRegistry
{
    public static IReadOnlyList<LanguageInfo> AllLanguages { get; } = new LanguageInfo[]
    {
        new("english",    "English",                  ["en"]),
        new("french",     "Français",                 ["fr"]),
        new("german",     "Deutsch",                  ["de"]),
        new("spanish",    "Español",                  ["es"]),
        new("latam",      "Español (Latinoamérica)",  ["es-MX", "es-AR", "es-CO", "es-CL", "es-PE", "es-VE", "es-US", "es-419"]),
        new("japanese",   "日本語",                    ["ja"]),
        new("korean",     "한국어",                    ["ko"]),
        new("russian",    "Русский",                  ["ru"]),
        // Explicit Simplified Chinese culture names only — "zh" alone would also match
        // zh-Hant/zh-TW (Traditional Chinese) since both share TwoLetterISOLanguageName "zh".
        new("schinese",   "中文（简体）",               ["zh-CN", "zh-SG", "zh-Hans", "zh-Hans-CN", "zh-Hans-SG"]),
        new("portuguese", "Português",                ["pt"]),
    };

    // Steam API codes that differ from our internal language codes.
    // "koreana" is Steam's legacy code for Korean; "brazilian" is Brazilian Portuguese.
    private static readonly Dictionary<string, string> _steamCodeAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["koreana"]  = "korean",
            ["brazilian"] = "portuguese",
        };

    public static LanguageInfo? FindByCode(string code) =>
        AllLanguages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Looks up a language by the code Steam's GetCurrentGameLanguage() returns.
    /// Handles known Steam quirks (e.g. "koreana" → korean, "brazilian" → portuguese).
    /// Returns null for Steam languages we don't support (e.g. "tchinese").
    /// </summary>
    public static LanguageInfo? FindBySteamCode(string steamCode)
    {
        var direct = FindByCode(steamCode);
        if (direct != null) return direct;

        if (_steamCodeAliases.TryGetValue(steamCode, out string? alias))
            return FindByCode(alias);

        return null;
    }

    public static LanguageInfo? FindByCulture(CultureInfo culture)
    {
        // Try exact culture name first (e.g. "es-MX" → latam before "es" → spanish).
        string fullName  = culture.Name;
        string twoLetter = culture.TwoLetterISOLanguageName;

        var exact = AllLanguages.FirstOrDefault(l =>
            l.CulturePrefixes.Any(p => p.Equals(fullName, StringComparison.OrdinalIgnoreCase)));
        if (exact != null) return exact;

        return AllLanguages.FirstOrDefault(l =>
            l.CulturePrefixes.Any(p => p.Equals(twoLetter, StringComparison.OrdinalIgnoreCase)));
    }
}
