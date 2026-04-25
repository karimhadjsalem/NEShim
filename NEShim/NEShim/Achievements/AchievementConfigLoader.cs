using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NEShim.Achievements;

/// <summary>
/// Loads per-game achievement definitions from achievements.json, keyed by ROM SHA1 hash.
/// Any definition whose ECDSA-P256 signature does not verify is silently dropped — it will
/// never fire in the game. Run seal-achievements to stamp valid signatures after editing the file.
/// Key resolution precedence: AchievementSigner.EmbeddedPublicKeyBase64 (binary-embedded, highest
/// priority) → achievementPublicKey in config.json → neither configured (returns null, no achievements fire).
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
///         "sig": "...base64 ECDSA-P256 signature written by seal-achievements..."
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
    /// signature-verified definitions, or null if none is configured or no key is set.
    /// Pass <paramref name="configPublicKey"/> from <c>AppConfig.AchievementPublicKey</c>.
    /// Key precedence: <see cref="AchievementSigner.EmbeddedPublicKeyBase64"/> (binary-embedded,
    /// set at build time) → <paramref name="configPublicKey"/> (config.json) → null (disabled).
    /// </summary>
    internal static GameAchievementConfig? Load(string romHash, string configPublicKey) =>
        LoadFrom(romHash, configPublicKey, ConfigPath, AchievementSigner.EmbeddedPublicKeyBase64);

    /// <summary>
    /// Full-parameter overload used by integration tests. Allows the file path and embedded
    /// key to be supplied explicitly so tests can exercise all key-precedence branches without
    /// touching the real <c>achievements.json</c> or the compile-time constant.
    /// </summary>
    internal static GameAchievementConfig? LoadFrom(
        string  romHash,
        string  configPublicKey,
        string  configPath,
        string? embeddedKey)
    {
        Logger.Log($"[Achievements] Loading for ROM hash: {romHash}");

        if (!File.Exists(configPath))
        {
            Logger.Log($"[Achievements] achievements.json not found at: {configPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(configPath);
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

            // Binary-embedded key takes precedence; config.json key is the fallback.
            string? publicKey =
                !string.IsNullOrEmpty(embeddedKey)     ? embeddedKey :
                !string.IsNullOrEmpty(configPublicKey) ? configPublicKey :
                null;

            if (publicKey is null)
            {
                Logger.Log("[Achievements] No signing key configured. " +
                           "Set achievementPublicKey in config.json or embed a key at build time. " +
                           "No achievements will fire.");
                return null;
            }

            int total = config.Achievements.Count;
            config.Achievements = config.Achievements
                .Where(def =>
                {
                    bool valid = AchievementSigner.Verify(def, publicKey);
                    if (!valid)
                        Logger.Log(
                            $"[Achievements] Rejected '{def.SteamId}' — missing or invalid signature. Run seal-achievements to fix.");
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
