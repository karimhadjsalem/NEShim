namespace NEShim.Achievements;

/// <summary>
/// Verifies ECDSA-P256 signatures on every <see cref="AchievementDef"/> in a config dictionary.
/// I/O-free — operates entirely on the in-memory data structure so it can be unit tested.
/// </summary>
public static class ValidationService
{
    public readonly record struct ValidationResult(int Valid, int Failed);

    /// <summary>
    /// Verifies the signature of every achievement definition in <paramref name="configs"/>
    /// against <paramref name="publicKeyBase64"/> and invokes <paramref name="onResult"/> for each.
    /// </summary>
    public static ValidationResult Validate(
        Dictionary<string, GameAchievementConfig> configs,
        string publicKeyBase64,
        Action<string, bool> onResult)
    {
        int valid = 0, failed = 0;

        foreach (var (_, config) in configs)
        {
            foreach (var def in config.Achievements)
            {
                bool ok = AchievementSigner.Verify(def, publicKeyBase64);
                onResult(def.SteamId, ok);
                if (ok) valid++; else failed++;
            }
        }

        return new ValidationResult(valid, failed);
    }
}
