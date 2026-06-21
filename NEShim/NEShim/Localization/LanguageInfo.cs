namespace NEShim.Localization;

/// <summary>
/// Metadata for a single supported UI language.
/// </summary>
internal sealed record LanguageInfo(
    string   Code,            // e.g. "english", "french"
    string   NativeName,      // displayed in its own language, e.g. "Français"
    string[] CulturePrefixes  // ISO 639-1 codes that map to this language
);
