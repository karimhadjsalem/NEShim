using System.Text;
using System.Text.Json;

namespace NEShim.Localization;

/// <summary>
/// Loads a <see cref="LocalizationData"/> from the <c>lang/</c> folder next to the executable.
/// Falls back to English when the requested language file does not exist,
/// and falls back to default English strings when no language file can be read at all.
/// </summary>
internal static class LocalizationLoader
{
    private static readonly JsonSerializerOptions DeserializeOptions =
        new() { PropertyNameCaseInsensitive = true };

    public static LocalizationData Load(string langDir, string language)
    {
        var path = Path.Combine(langDir, $"{language}.json");
        if (File.Exists(path))
            return LoadFrom(path);

        Logger.Log($"[Localization] '{language}.json' not found in '{langDir}'.");

        if (!language.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = Path.Combine(langDir, "english.json");
            if (File.Exists(fallback))
            {
                Logger.Log("[Localization] Falling back to english.json.");
                return LoadFrom(fallback);
            }
            Logger.Log("[Localization] english.json not found either — using built-in English defaults.");
        }
        else
        {
            Logger.Log("[Localization] Using built-in English defaults.");
        }

        return new LocalizationData();
    }

    internal static LocalizationData LoadFrom(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<LocalizationData>(json, DeserializeOptions)
                       ?? new LocalizationData();
            Logger.Log($"[Localization] Loaded '{Path.GetFileName(path)}'.");
            return data;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Localization] Failed to read '{path}': {ex.Message} — using built-in English defaults.");
            return new LocalizationData();
        }
    }
}
