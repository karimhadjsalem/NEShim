using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NEShim.Achievements;

/// <summary>
/// Loads per-game achievement definitions from achievements.json, keyed by ROM SHA1 hash.
///
/// achievements.json format:
/// <code>
/// {
///   "SHA1HASHOFROM...": {
///     "memoryDomain": "System Bus",
///     "achievements": [
///       {
///         "steamId": "ACH_FIRST_WIN",
///         "address": 255,
///         "bytes": 1,
///         "encoding": "binary",
///         "comparison": "equals",
///         "value": 1
///       }
///     ]
///   }
/// }
/// </code>
/// </summary>
internal static class AchievementConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
    };

    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "achievements.json");

    /// <summary>
    /// Returns the achievement config for the given ROM SHA1 hash, or null if none is configured.
    /// </summary>
    internal static GameAchievementConfig? Load(string romHash)
    {
        if (!File.Exists(ConfigPath)) return null;

        try
        {
            string json = File.ReadAllText(ConfigPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, GameAchievementConfig>>(json, _options);
            if (dict is null) return null;
            return dict.TryGetValue(romHash, out var config) ? config : null;
        }
        catch
        {
            return null;
        }
    }
}
