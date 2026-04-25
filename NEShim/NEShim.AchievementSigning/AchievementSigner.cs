using System.Security.Cryptography;
using System.Text;

namespace NEShim.Achievements;

/// <summary>
/// Signs and verifies <see cref="AchievementDef"/> trigger fields using ECDSA-P256.
///
/// The private key lives only on the publisher's build machine and is passed to
/// <see cref="ComputeSig"/> by the seal-achievements tool — it never ships with the game.
///
/// The public key is resolved at runtime by <c>AchievementConfigLoader</c> using this precedence:
///   1. <see cref="EmbeddedPublicKeyBase64"/> — set this constant at build time for maximum
///      security; the key cannot be overridden by editing a config file.
///   2. <c>achievementPublicKey</c> in config.json — the pre-built release path; no rebuild needed.
///   3. Neither set → achievements are disabled.
///
/// There is no default key. To generate a keypair: seal-achievements --gen-keypair
/// </summary>
public static class AchievementSigner
{
    // ECDSA-P256 public key, SubjectPublicKeyInfo DER format, base64-encoded.
    // Set this to your public key at build time to bake it into the binary.
    // When non-null, this key takes precedence over achievementPublicKey in config.json —
    // the key cannot be changed without recompiling. Leave null to use config.json instead.
    public const string? EmbeddedPublicKeyBase64 = null;

    private static string Canonical(AchievementDef def) =>
        FormattableString.Invariant(
            $"{def.SteamId}|{def.Address}|{def.Bytes}|{def.BigEndian}|{def.Encoding}|{def.Comparison}|{def.Value}");

    /// <summary>
    /// Signs the trigger fields of <paramref name="def"/> with <paramref name="privateKeyBase64"/>
    /// (SEC1 DER format, base64-encoded) and returns the ECDSA-P256 signature as a base64 string.
    /// Called only by the seal-achievements build tool — never at runtime.
    /// </summary>
    public static string ComputeSig(AchievementDef def, string privateKeyBase64)
    {
        byte[] privKeyBytes = Convert.FromBase64String(privateKeyBase64);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ecdsa.ImportECPrivateKey(privKeyBytes, out _);
        byte[] sig = ecdsa.SignData(
            Encoding.UTF8.GetBytes(Canonical(def)),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(sig);
    }

    /// <summary>
    /// Returns true if <paramref name="def"/>'s <see cref="AchievementDef.Sig"/> is a valid
    /// ECDSA-P256 signature over its trigger fields, verified with <paramref name="publicKeyBase64"/>
    /// (SubjectPublicKeyInfo DER format, base64-encoded).
    /// </summary>
    public static bool Verify(AchievementDef def, string publicKeyBase64)
    {
        if (string.IsNullOrEmpty(def.Sig)) return false;

        try
        {
            byte[] sig    = Convert.FromBase64String(def.Sig);
            byte[] pubKey = Convert.FromBase64String(publicKeyBase64);
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ecdsa.ImportSubjectPublicKeyInfo(pubKey, out _);
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(Canonical(def)),
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
