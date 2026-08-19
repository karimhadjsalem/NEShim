using System.Security.Cryptography;
using System.Text;

namespace NEShim.Achievements;

/// <summary>
/// Signs and verifies the multi-game shell manifest's <c>gameDlcAppIds</c> map (a
/// gameId → steamDlcAppId trust anchor, see AppConfig.GameDlcAppIds) using ECDSA-P256 — the same
/// algorithm and key format as <see cref="AchievementSigner"/>, applied to a different payload
/// (a shell-level map, not a per-achievement trigger). A dedicated class rather than reusing
/// AchievementSigner directly, because this MUST be a separate keypair from achievement signing
/// — never reuse the same keypair for both. They protect different things (gameplay-trigger
/// integrity vs. DLC ownership integrity); a leaked or rotated key for one must not force
/// touching the other, and generating a second free keypair (`pub-utils --gen-keypair`)
/// costs nothing.
///
/// The private key lives only on the publisher's build machine — never ships with the game.
/// Unlike AchievementSigner's public key (which has a config.json fallback, needed because
/// there's no single correct key across N different games' achievements.json files),
/// <see cref="EmbeddedPublicKeyBase64"/> is deliberately compile-time-only with NO config
/// fallback: this map is one shell-level payload, not per-game, so a single compiled key always
/// works, and a config-driven key would let a tampered install simply supply its own matching
/// keypair alongside a forged map — defeating the signature entirely. Its mere presence is what
/// opts a build into DLC-ownership signing at all — see <c>GameScanner.Scan</c> for the
/// fail-closed verification semantics this enables: no embedded key means this entirely skips
/// (today's unsigned soft cross-check applies instead, if <c>gameDlcAppIds</c> is set at all); a
/// configured key with a missing or invalid signature rejects every scanned game, rather than
/// silently falling back to trusting each game's own unsigned claim.
/// </summary>
public static class DlcMapSigner
{
    // ECDSA-P256 public key, SubjectPublicKeyInfo DER format, base64-encoded. Set this to your
    // public key at build time to bake it into the binary — this is the ONLY way to configure
    // it; there is no config.json equivalent (see the class doc comment for why). Leave null to
    // skip DLC-map signing (games/multigame.json's gameDlcAppIds, if set at all, is then only
    // the weaker unsigned soft cross-check).
    //
    // IMPORTANT: this MUST be a DIFFERENT keypair from AchievementSigner.EmbeddedPublicKeyBase64
    // / achievementPublicKey — do not reuse the achievement-signing key here. Generate a fresh
    // one: pub-utils --gen-keypair
    public const string? EmbeddedPublicKeyBase64 = null;

    /// <summary>
    /// Deterministic serialization of the map, sorted by gameId so source JSON key ordering
    /// never affects the signed payload.
    /// </summary>
    private static string Canonical(IReadOnlyDictionary<string, uint> gameDlcAppIds) =>
        string.Join("|", gameDlcAppIds
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => FormattableString.Invariant($"{kv.Key}={kv.Value}")));

    /// <summary>
    /// Signs <paramref name="gameDlcAppIds"/> with <paramref name="privateKeyBase64"/> (SEC1 DER
    /// format, base64-encoded) and returns the ECDSA-P256 signature as a base64 string. Called
    /// only by the pub-utils build tool — never at runtime.
    /// </summary>
    public static string ComputeSig(IReadOnlyDictionary<string, uint> gameDlcAppIds, string privateKeyBase64)
    {
        byte[] privKeyBytes = Convert.FromBase64String(privateKeyBase64);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ecdsa.ImportECPrivateKey(privKeyBytes, out _);
        byte[] sig = ecdsa.SignData(
            Encoding.UTF8.GetBytes(Canonical(gameDlcAppIds)),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(sig);
    }

    /// <summary>
    /// Returns true if <paramref name="signatureBase64"/> is a valid ECDSA-P256 signature over
    /// <paramref name="gameDlcAppIds"/>, verified with <paramref name="publicKeyBase64"/>
    /// (SubjectPublicKeyInfo DER format, base64-encoded). False for any malformed input
    /// (exceptions are swallowed, not propagated) — a corrupt signature/key must verify as
    /// "invalid", never throw past this boundary.
    /// </summary>
    public static bool Verify(IReadOnlyDictionary<string, uint> gameDlcAppIds, string signatureBase64, string publicKeyBase64)
    {
        if (string.IsNullOrEmpty(signatureBase64) || string.IsNullOrEmpty(publicKeyBase64)) return false;

        try
        {
            byte[] sig    = Convert.FromBase64String(signatureBase64);
            byte[] pubKey = Convert.FromBase64String(publicKeyBase64);
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ecdsa.ImportSubjectPublicKeyInfo(pubKey, out _);
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(Canonical(gameDlcAppIds)),
                sig,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch
        {
            return false;
        }
    }
}
