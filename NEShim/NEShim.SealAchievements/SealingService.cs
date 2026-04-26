using NEShim.Achievements;

namespace NEShim.SealAchievements;

/// <summary>
/// Stamps ECDSA-P256 signatures onto every <see cref="AchievementDef"/> in a config dictionary.
/// I/O-free — operates entirely on the in-memory data structure so it can be unit tested.
/// </summary>
internal static class SealingService
{
    internal readonly record struct SealResult(int Sealed, int Skipped);

    /// <summary>
    /// Iterates every achievement definition in <paramref name="configs"/>, computes its
    /// ECDSA-P256 signature using <paramref name="privateKeyBase64"/>, and writes it to
    /// <see cref="AchievementDef.Sig"/>.
    /// Definitions with a null or whitespace <see cref="AchievementDef.SteamId"/> are skipped.
    /// </summary>
    internal static SealResult Seal(
        Dictionary<string, GameAchievementConfig> configs,
        string privateKeyBase64)
    {
        int sealedCount  = 0;
        int skippedCount = 0;

        foreach (var (_, config) in configs)
        {
            foreach (var def in config.Achievements)
            {
                if (string.IsNullOrWhiteSpace(def.SteamId))
                {
                    skippedCount++;
                    continue;
                }

                def.Sig = AchievementSigner.ComputeSig(def, privateKeyBase64);
                sealedCount++;
            }
        }

        return new SealResult(sealedCount, skippedCount);
    }
}
