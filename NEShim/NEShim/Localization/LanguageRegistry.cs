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
        new("schinese",   "中文（简体）",               ["zh"]),
        new("portuguese", "Português",                ["pt"]),
    };

    public static LanguageInfo? FindByCode(string code) =>
        AllLanguages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

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
