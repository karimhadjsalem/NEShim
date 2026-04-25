using NEShim.Achievements;

namespace NEShim.Tests.Achievements;

[TestFixture]
internal class AchievementSignerTests
{
    // Test keypair A — used for signing and positive verification in all round-trip tests.
    // Generated with seal-achievements --gen-keypair; test-only, not production keys.
    private const string TestPrivateKeyBase64 =
        "MHcCAQEEIJX+aCzo2G6R5dUkmZWSRbUDpJMqj57dNvMZBNRhdjoqoAoGCCqGSM49AwEHoUQDQgAE" +
        "aAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3S" +
        "LZyvfmHJhw==";
    private const string TestPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG" +
        "1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3SLZyvfmHJhw==";

    // Test keypair B public key — used only in wrong-key rejection tests.
    private const string OtherPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAgCiqeoxm0UuLd9EiZt/ONVA6SybkplDzznY8s1f" +
        "YPCo1hbCAiaFWf3bJl33Sz2qXdYvH+UqQDaSQio5nP3SpQ==";

    private static AchievementDef ValidDef(
        string steamId    = "ACH_TEST",
        int    address    = 0xFF,
        int    bytes      = 1,
        bool   bigEndian  = false,
        string encoding   = "binary",
        string comparison = "equals",
        long   value      = 1) =>
        new()
        {
            SteamId    = steamId,
            Address    = address,
            Bytes      = bytes,
            BigEndian  = bigEndian,
            Encoding   = encoding,
            Comparison = comparison,
            Value      = value,
        };

    // ---- ComputeSig ----

    [Test]
    public void ComputeSig_ReturnsNonEmptyBase64String()
    {
        string sig = AchievementSigner.ComputeSig(ValidDef(), TestPrivateKeyBase64);

        Assert.That(sig, Is.Not.Null.And.Not.Empty);
        Assert.DoesNotThrow(() => Convert.FromBase64String(sig));
    }

    [Test]
    public void ComputeSig_ProducesFixed64ByteSignature()
    {
        string sig = AchievementSigner.ComputeSig(ValidDef(), TestPrivateKeyBase64);

        Assert.That(Convert.FromBase64String(sig).Length, Is.EqualTo(64));
    }

    [Test]
    public void ComputeSig_DiffersWhenSteamIdChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(steamId: "ACH_A"), TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(steamId: "ACH_B"), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenAddressChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(address: 0x00), TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(address: 0x01), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenValueChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(value: 1), TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(value: 2), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenComparisonChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(comparison: "equals"),         TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(comparison: "greaterOrEqual"), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenEncodingChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(encoding: "binary"), TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(encoding: "bcd"),    TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenBytesChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(bytes: 1), TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(bytes: 2), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenBigEndianChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(bigEndian: false), TestPrivateKeyBase64);
        string sig2 = AchievementSigner.ComputeSig(ValidDef(bigEndian: true),  TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    // ---- Verify ----

    [Test]
    public void Verify_ReturnsTrueForMatchingSig()
    {
        var def = ValidDef();
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.True);
    }

    [Test]
    public void Verify_ReturnsFalseForNullSig()
    {
        var def = ValidDef();
        def.Sig = null;

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForEmptySig()
    {
        var def = ValidDef();
        def.Sig = "";

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenAddressModifiedAfterSigning()
    {
        var def = ValidDef(address: 0x10);
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        var tampered = def with { Address = 0x00 };

        Assert.That(AchievementSigner.Verify(tampered, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenValueModifiedAfterSigning()
    {
        var def = ValidDef(value: 10000);
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        var tampered = def with { Value = 0 };

        Assert.That(AchievementSigner.Verify(tampered, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenComparisonModifiedAfterSigning()
    {
        var def = ValidDef(comparison: "greaterOrEqual");
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        var tampered = def with { Comparison = "equals" };

        Assert.That(AchievementSigner.Verify(tampered, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForMalformedBase64Sig()
    {
        var def = ValidDef();
        def.Sig = "!!!not-valid-base64!!!";

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForTamperedSig()
    {
        var def = ValidDef();
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        byte[] sigBytes = Convert.FromBase64String(def.Sig);
        sigBytes[0] ^= 0xFF;
        def.Sig = Convert.ToBase64String(sigBytes);

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForWrongPublicKey()
    {
        var def = ValidDef();
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        Assert.That(AchievementSigner.Verify(def, OtherPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForHmacFormattedSig()
    {
        // A legacy HMAC-SHA256 sig is 32 bytes (44 base64 chars); ECDSA-P256 expects 64 bytes.
        // Feeding an HMAC sig to the new verifier must return false gracefully, not throw.
        var def = ValidDef();
        def.Sig = Convert.ToBase64String(new byte[32]); // 32-byte placeholder simulating HMAC

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForTruncatedSig()
    {
        var def = ValidDef();
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);

        // Drop one byte from the end to produce a 63-byte sig
        byte[] sigBytes = Convert.FromBase64String(def.Sig);
        def.Sig = Convert.ToBase64String(sigBytes[..63]);

        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.False);
    }
}
