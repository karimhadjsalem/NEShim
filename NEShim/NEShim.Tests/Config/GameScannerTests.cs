using NEShim.Config;

namespace NEShim.Tests.Config;

/// <summary>
/// Pure unit tests for <see cref="GameScanner.IsDlcClaimTrusted"/> — the DLC trust-tier decision
/// isolated from directory scanning. Boundary-crossing tests for <see cref="GameScanner.Scan"/>
/// itself (real temp-directory file I/O) live in <c>NEShim.Tests/Integration/GameScannerTests.cs</c>.
/// </summary>
[TestFixture]
internal class GameScannerTests
{
    // ---- No trust data at all ----

    [Test]
    public void IsDlcClaimTrusted_NoMapAndSigningDisabled_TrustsAnyClaimedValue()
    {
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: null, signingEnabled: false, signatureValid: false);
        Assert.That(trusted, Is.True);
    }

    // ---- Unsigned soft cross-check ----

    [Test]
    public void IsDlcClaimTrusted_Unsigned_GameIdAbsentFromMap_TrustedAsDeclared()
    {
        var map = new Dictionary<string, uint> { ["other-game"] = 999 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: false, signatureValid: false);
        Assert.That(trusted, Is.True);
    }

    [Test]
    public void IsDlcClaimTrusted_Unsigned_GameIdPresentAndMatches_Trusted()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 12345 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: false, signatureValid: false);
        Assert.That(trusted, Is.True);
    }

    [Test]
    public void IsDlcClaimTrusted_Unsigned_GameIdPresentButMismatches_NotTrusted()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 999 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: false, signatureValid: false);
        Assert.That(trusted, Is.False);
    }

    [Test]
    public void IsDlcClaimTrusted_Unsigned_BundledGameListedWithZero_MatchesZeroClaim()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 0 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 0,
            trustedDlcAppIds: map, signingEnabled: false, signatureValid: false);
        Assert.That(trusted, Is.True);
    }

    // ---- Signed mode: fails closed ----

    [Test]
    public void IsDlcClaimTrusted_Signed_SignatureInvalid_NotTrusted_EvenWithMatchingMapEntry()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 12345 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: true, signatureValid: false);
        Assert.That(trusted, Is.False);
    }

    [Test]
    public void IsDlcClaimTrusted_Signed_ValidSignature_GameIdPresentAndMatches_Trusted()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 12345 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: true, signatureValid: true);
        Assert.That(trusted, Is.True);
    }

    [Test]
    public void IsDlcClaimTrusted_Signed_ValidSignature_GameIdPresentButMismatches_NotTrusted()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 999 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: true, signatureValid: true);
        Assert.That(trusted, Is.False);
    }

    [Test]
    public void IsDlcClaimTrusted_Signed_ValidSignature_GameIdAbsentFromMap_NotTrusted()
    {
        // Once signed, the map must be complete — absence is rejected, not trusted through
        // (unlike the unsigned soft-check tier).
        var map = new Dictionary<string, uint> { ["other-game"] = 999 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 12345,
            trustedDlcAppIds: map, signingEnabled: true, signatureValid: true);
        Assert.That(trusted, Is.False);
    }

    [Test]
    public void IsDlcClaimTrusted_Signed_ValidSignature_BundledGameListedWithZero_Trusted()
    {
        var map = new Dictionary<string, uint> { ["kaaz"] = 0 };
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 0,
            trustedDlcAppIds: map, signingEnabled: true, signatureValid: true);
        Assert.That(trusted, Is.True);
    }

    [Test]
    public void IsDlcClaimTrusted_Signed_ValidSignature_NullMap_NotTrusted()
    {
        // Defensive: signatureValid should never be true with a null map in practice (Scan
        // only computes signatureValid when trustedDlcAppIds is non-null), but the trust
        // decision itself must not assume that invariant holds.
        bool trusted = GameScanner.IsDlcClaimTrusted("kaaz", claimedAppId: 0,
            trustedDlcAppIds: null, signingEnabled: true, signatureValid: true);
        Assert.That(trusted, Is.False);
    }
}
