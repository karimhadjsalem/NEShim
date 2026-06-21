using System.Globalization;

namespace NEShim.Localization;

/// <summary>
/// Resolves the UI language from the OS's current UI culture (CultureInfo.CurrentUICulture).
/// Returns null when the culture does not map to a supported language.
/// </summary>
internal sealed class CultureInfoLanguageResolver : ILanguageResolver
{
    public string? Resolve()
    {
        var culture = CultureInfo.CurrentUICulture;
        Logger.Log($"[Localization] CultureInfoResolver: current UI culture is '{culture.Name}'.");

        var match = LanguageRegistry.FindByCulture(culture);
        if (match is null)
        {
            Logger.Log($"[Localization] CultureInfoResolver: no mapping for '{culture.Name}' — skipping.");
            return null;
        }

        Logger.Log($"[Localization] CultureInfoResolver: mapped '{culture.Name}' → '{match.Code}'.");
        return match.Code;
    }
}
