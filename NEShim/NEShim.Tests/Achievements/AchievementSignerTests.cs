using NEShim.Achievements;

namespace NEShim.Tests.Achievements;

[TestFixture]
internal class AchievementSignerTests
{
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
        string sig = AchievementSigner.ComputeSig(ValidDef());

        Assert.That(sig, Is.Not.Null.And.Not.Empty);
        Assert.DoesNotThrow(() => Convert.FromBase64String(sig));
    }

    [Test]
    public void ComputeSig_IsDeterministicForSameDef()
    {
        var def = ValidDef();

        string sig1 = AchievementSigner.ComputeSig(def);
        string sig2 = AchievementSigner.ComputeSig(def);

        Assert.That(sig1, Is.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenSteamIdChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(steamId: "ACH_A"));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(steamId: "ACH_B"));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenAddressChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(address: 0x00));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(address: 0x01));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenValueChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(value: 1));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(value: 2));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenComparisonChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(comparison: "equals"));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(comparison: "greaterOrEqual"));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenEncodingChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(encoding: "binary"));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(encoding: "bcd"));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenBytesChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(bytes: 1));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(bytes: 2));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenBigEndianChanges()
    {
        string sig1 = AchievementSigner.ComputeSig(ValidDef(bigEndian: false));
        string sig2 = AchievementSigner.ComputeSig(ValidDef(bigEndian: true));

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    // ---- Verify ----

    [Test]
    public void Verify_ReturnsTrueForMatchingSig()
    {
        var def = ValidDef();
        def.Sig = AchievementSigner.ComputeSig(def);

        Assert.That(AchievementSigner.Verify(def), Is.True);
    }

    [Test]
    public void Verify_ReturnsFalseForNullSig()
    {
        var def = ValidDef();
        def.Sig = null;

        Assert.That(AchievementSigner.Verify(def), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForEmptySig()
    {
        var def = ValidDef();
        def.Sig = "";

        Assert.That(AchievementSigner.Verify(def), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenAddressModifiedAfterSigning()
    {
        var def = ValidDef(address: 0x10);
        def.Sig = AchievementSigner.ComputeSig(def);

        // Simulate tampering: change the address without re-signing
        var tampered = def with { Address = 0x00 };

        Assert.That(AchievementSigner.Verify(tampered), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenValueModifiedAfterSigning()
    {
        var def = ValidDef(value: 10000);
        def.Sig = AchievementSigner.ComputeSig(def);

        var tampered = def with { Value = 0 };

        Assert.That(AchievementSigner.Verify(tampered), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenComparisonModifiedAfterSigning()
    {
        var def = ValidDef(comparison: "greaterOrEqual");
        def.Sig = AchievementSigner.ComputeSig(def);

        var tampered = def with { Comparison = "equals" };

        Assert.That(AchievementSigner.Verify(tampered), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForMalformedBase64Sig()
    {
        var def = ValidDef();
        def.Sig = "!!!not-valid-base64!!!";

        Assert.That(AchievementSigner.Verify(def), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForTamperedSig()
    {
        var def = ValidDef();
        def.Sig = AchievementSigner.ComputeSig(def);

        // Flip the first byte of the base64-decoded sig
        byte[] sigBytes = Convert.FromBase64String(def.Sig);
        sigBytes[0] ^= 0xFF;
        def.Sig = Convert.ToBase64String(sigBytes);

        Assert.That(AchievementSigner.Verify(def), Is.False);
    }
}
