using System.Security.Cryptography;
using System.Text;

namespace NEShim.Achievements;

/// <summary>
/// Signs and verifies <see cref="AchievementDef"/> trigger fields using HMAC-SHA256.
///
/// The HMAC key is embedded in this binary. Changing any trigger field (address, value,
/// comparison, etc.) without re-running SealAchievements will produce a signature mismatch
/// and the achievement will be silently ignored at runtime.
///
/// To regenerate the key before shipping:
///   dotnet run --project NEShim.SealAchievements -- --gen-key
/// Copy the printed value into the HmacKeyBase64 constant below, then re-seal all configs.
/// </summary>
public static class AchievementSigner
{
    // 32-byte (256-bit) HMAC key, base64-encoded.
    // IMPORTANT: Replace this with your own generated key before shipping if you want a new key.
    // Run: dotnet run --project NEShim/NEShim.SealAchievements -- --gen-key
    private const string HmacKeyBase64 = "Kf9pXzQ3mNb8LvuYR+2tHoEJ0gWIasDcCe4rMjViywU=";

    private static readonly byte[] Key = Convert.FromBase64String(HmacKeyBase64);

    /// <summary>
    /// Computes the HMAC-SHA256 signature for the trigger fields of <paramref name="def"/>.
    /// The <see cref="AchievementDef.Sig"/> field is excluded from the computation.
    /// </summary>
    public static string ComputeSig(AchievementDef def)
    {
        string canonical = FormattableString.Invariant(
            $"{def.SteamId}|{def.Address}|{def.Bytes}|{def.BigEndian}|{def.Encoding}|{def.Comparison}|{def.Value}");

        using var hmac = new HMACSHA256(Key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Returns true if <paramref name="def"/>'s <see cref="AchievementDef.Sig"/> matches
    /// the expected HMAC for its trigger fields.
    /// </summary>
    public static bool Verify(AchievementDef def)
    {
        if (string.IsNullOrEmpty(def.Sig)) return false;

        string expected = ComputeSig(def);

        try
        {
            // Constant-time comparison to prevent timing attacks.
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(def.Sig),
                Convert.FromBase64String(expected));
        }
        catch (FormatException)
        {
            return false; // malformed base64 in the Sig field
        }
    }
}
