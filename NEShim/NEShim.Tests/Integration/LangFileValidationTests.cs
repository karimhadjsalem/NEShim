using System.Text.Json;

namespace NEShim.Tests.Integration;

/// <summary>
/// Validates the real <c>lang/*.json</c> files shipped alongside the exe (copied into this test
/// project's own output via the NEShim.csproj ProjectReference's Content items — the same
/// AppContext.BaseDirectory/lang path LocalizationLoader reads from at runtime). Integration
/// test: crosses the file system boundary. This is deliberately NOT covered by
/// LocalizationLoaderTests (which only exercises ad-hoc temp JSON, not the real shipped files) —
/// it exists specifically to catch a language file silently falling behind english.json, which
/// LocalizationLoader's per-key English fallback means would otherwise ship unnoticed (as
/// happened with the four "Change Game" keys before this test was added).
/// </summary>
[TestFixture]
internal class LangFileValidationTests
{
    private static readonly string LangDir = Path.Combine(AppContext.BaseDirectory, "lang");

    // Mirrors LanguageRegistry's 10 built-in languages (see CLAUDE.md / README).
    private static readonly string[] Languages =
    {
        "english", "french", "german", "japanese", "korean",
        "latam", "portuguese", "russian", "schinese", "spanish",
    };

    [TestCaseSource(nameof(Languages))]
    public void LangFile_Exists(string language)
    {
        Assert.That(File.Exists(Path.Combine(LangDir, $"{language}.json")), Is.True,
            $"Missing lang file for '{language}'.");
    }

    [TestCaseSource(nameof(Languages))]
    public void LangFile_ParsesAsValidJson(string language)
    {
        string path = Path.Combine(LangDir, $"{language}.json");
        Assert.That(() => JsonDocument.Parse(File.ReadAllText(path)), Throws.Nothing,
            $"'{language}.json' is not valid JSON.");
    }

    [TestCaseSource(nameof(Languages))]
    public void LangFile_HasEveryKeyPresentInEnglish(string language)
    {
        var englishKeys = LoadKeys("english");
        var keys = LoadKeys(language);
        var missing = englishKeys.Except(keys).ToList();

        Assert.That(missing, Is.Empty,
            $"'{language}.json' is missing keys present in english.json: {string.Join(", ", missing)}");
    }

    [TestCaseSource(nameof(Languages))]
    public void LangFile_HasNoKeysAbsentFromEnglish(string language)
    {
        // Catches typos in a translated file's own key names (a misspelled key silently falls
        // back to the English default for the CORRECT key, while the misspelled one sits unused).
        var englishKeys = LoadKeys("english");
        var keys = LoadKeys(language);
        var extra = keys.Except(englishKeys).ToList();

        Assert.That(extra, Is.Empty,
            $"'{language}.json' has keys not present in english.json (likely a typo): {string.Join(", ", extra)}");
    }

    private static HashSet<string> LoadKeys(string language)
    {
        string path = Path.Combine(LangDir, $"{language}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    }
}
