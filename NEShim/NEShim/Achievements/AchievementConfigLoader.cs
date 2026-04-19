using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NEShim.Achievements;

/// <summary>
/// Loads per-game achievement definitions from achievements.json, keyed by ROM SHA1 hash.
/// Any definition whose HMAC-SHA256 signature does not match is silently dropped — it will
/// never fire in the game. Run SealAchievements to stamp valid signatures after editing the file.
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
///         "value": 1,
///         "sig": "...base64 HMAC written by SealAchievements..."
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
    /// Returns the achievement config for the given ROM SHA1 hash with only
    /// signature-verified definitions, or null if none is configured.
    /// </summary>
    internal static GameAchievementConfig? Load(string romHash)
    {
        Logger.Log($"[Achievements] Loading for ROM hash: {romHash}");

        if (!File.Exists(ConfigPath))
        {
            Logger.Log($"[Achievements] achievements.json not found at: {ConfigPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, GameAchievementConfig>>(json, _options);

            if (dict is null)
            {
                Logger.Log("[Achievements] achievements.json deserialized to null — file may be empty or malformed.");
                return null;
            }

            if (!dict.TryGetValue(romHash, out var config))
            {
                Logger.Log($"[Achievements] No entry for hash '{romHash}' in achievements.json. Known hashes: {string.Join(", ", dict.Keys)}");
                return null;
            }

            int total = config.Achievements.Count;
            config.Achievements = config.Achievements
                .Where(def =>
                {
                    bool valid = AchievementSigner.Verify(def);
                    if (!valid)
                        Logger.Log(
                            $"[Achievements] Rejected '{def.SteamId}' — missing or invalid signature. Run SealAchievements to fix.");
                    return valid;
                })
                .ToList();

            int loaded = config.Achievements.Count;
            Logger.Log($"[Achievements] Loaded {loaded}/{total} definitions for hash '{romHash}'.");

            if (loaded == 0)
            {
                Logger.Log("[Achievements] All definitions were rejected — no achievements will fire.");
                return null;
            }

            return config;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Achievements] Failed to load achievements.json: {ex.Message}");
            return null;
        }
    }
}
