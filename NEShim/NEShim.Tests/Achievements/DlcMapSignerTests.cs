using NEShim.Achievements;

namespace NEShim.Tests.Achievements;

[TestFixture]
internal class DlcMapSignerTests
{
    // Same test keypair as AchievementSignerTests — generated with seal-achievements
    // --gen-keypair; test-only, not production keys.
    private const string TestPrivateKeyBase64 =
        "MHcCAQEEIJX+aCzo2G6R5dUkmZWSRbUDpJMqj57dNvMZBNRhdjoqoAoGCCqGSM49AwEHoUQDQgAE" +
        "aAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3S" +
        "LZyvfmHJhw==";
    private const string TestPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG" +
        "1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3SLZyvfmHJhw==";

    private const string OtherPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAgCiqeoxm0UuLd9EiZt/ONVA6SybkplDzznY8s1f" +
        "YPCo1hbCAiaFWf3bJl33Sz2qXdYvH+UqQDaSQio5nP3SpQ==";

    private static Dictionary<string, uint> Map(params (string gameId, uint appId)[] entries) =>
        entries.ToDictionary(e => e.gameId, e => e.appId);

    // ---- ComputeSig ----

    [Test]
    public void ComputeSig_ReturnsNonEmptyBase64String()
    {
        string sig = DlcMapSigner.ComputeSig(Map(("kaaz", 123)), TestPrivateKeyBase64);

        Assert.That(sig, Is.Not.Null.And.Not.Empty);
        Assert.DoesNotThrow(() => Convert.FromBase64String(sig));
    }

    [Test]
    public void ComputeSig_ProducesFixed64ByteSignature()
    {
        string sig = DlcMapSigner.ComputeSig(Map(("kaaz", 123)), TestPrivateKeyBase64);

        Assert.That(Convert.FromBase64String(sig).Length, Is.EqualTo(64));
    }

    [Test]
    public void ComputeSig_DiffersWhenAppIdChanges()
    {
        string sig1 = DlcMapSigner.ComputeSig(Map(("kaaz", 100)), TestPrivateKeyBase64);
        string sig2 = DlcMapSigner.ComputeSig(Map(("kaaz", 200)), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenGameIdChanges()
    {
        string sig1 = DlcMapSigner.ComputeSig(Map(("kaaz", 100)), TestPrivateKeyBase64);
        string sig2 = DlcMapSigner.ComputeSig(Map(("zelda", 100)), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_DiffersWhenEntryAdded()
    {
        string sig1 = DlcMapSigner.ComputeSig(Map(("kaaz", 100)), TestPrivateKeyBase64);
        string sig2 = DlcMapSigner.ComputeSig(Map(("kaaz", 100), ("zelda", 200)), TestPrivateKeyBase64);

        Assert.That(sig1, Is.Not.EqualTo(sig2));
    }

    [Test]
    public void ComputeSig_KeyOrderDoesNotAffectSignature()
    {
        // ECDSA signing is non-deterministic (random per-signature nonce), so compare
        // verification outcomes rather than raw signature bytes — a signature computed over one
        // key ordering must still verify against the other, equivalent ordering, proving
        // Canonical's sort makes the signed payload order-independent.
        var mapA = Map(("kaaz", 100), ("zelda", 200));
        var mapB = Map(("zelda", 200), ("kaaz", 100));

        string sigA = DlcMapSigner.ComputeSig(mapA, TestPrivateKeyBase64);

        Assert.That(DlcMapSigner.Verify(mapB, sigA, TestPublicKeyBase64), Is.True);
    }

    // ---- Verify ----

    [Test]
    public void Verify_ReturnsTrueForMatchingSig()
    {
        var map = Map(("kaaz", 100), ("zelda", 200));
        string sig = DlcMapSigner.ComputeSig(map, TestPrivateKeyBase64);

        Assert.That(DlcMapSigner.Verify(map, sig, TestPublicKeyBase64), Is.True);
    }

    [Test]
    public void Verify_ReturnsFalseForNullSig()
    {
        var map = Map(("kaaz", 100));

        Assert.That(DlcMapSigner.Verify(map, null!, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForEmptySig()
    {
        var map = Map(("kaaz", 100));

        Assert.That(DlcMapSigner.Verify(map, "", TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForEmptyPublicKey()
    {
        var map = Map(("kaaz", 100));
        string sig = DlcMapSigner.ComputeSig(map, TestPrivateKeyBase64);

        Assert.That(DlcMapSigner.Verify(map, sig, ""), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForMalformedSig()
    {
        var map = Map(("kaaz", 100));

        Assert.That(DlcMapSigner.Verify(map, "not-valid-base64!!!", TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseForMalformedPublicKey()
    {
        var map = Map(("kaaz", 100));
        string sig = DlcMapSigner.ComputeSig(map, TestPrivateKeyBase64);

        Assert.That(DlcMapSigner.Verify(map, sig, "not-valid-base64!!!"), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWithWrongPublicKey()
    {
        var map = Map(("kaaz", 100));
        string sig = DlcMapSigner.ComputeSig(map, TestPrivateKeyBase64);

        Assert.That(DlcMapSigner.Verify(map, sig, OtherPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenAppIdTamperedAfterSigning()
    {
        var original = Map(("kaaz", 100));
        string sig = DlcMapSigner.ComputeSig(original, TestPrivateKeyBase64);

        // The exact attack this class defends against: downgrading a signed nonzero appId to 0.
        var tampered = Map(("kaaz", 0));

        Assert.That(DlcMapSigner.Verify(tampered, sig, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenEntryRemovedAfterSigning()
    {
        var original = Map(("kaaz", 100), ("zelda", 200));
        string sig = DlcMapSigner.ComputeSig(original, TestPrivateKeyBase64);

        var tampered = Map(("kaaz", 100)); // "zelda" entry stripped out

        Assert.That(DlcMapSigner.Verify(tampered, sig, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_ReturnsFalseWhenEntryAddedAfterSigning()
    {
        var original = Map(("kaaz", 100));
        string sig = DlcMapSigner.ComputeSig(original, TestPrivateKeyBase64);

        var tampered = Map(("kaaz", 100), ("pirated-game", 0)); // extra entry smuggled in

        Assert.That(DlcMapSigner.Verify(tampered, sig, TestPublicKeyBase64), Is.False);
    }

    [Test]
    public void Verify_EmptyMap_ValidSignature_ReturnsTrue()
    {
        var map = new Dictionary<string, uint>();
        string sig = DlcMapSigner.ComputeSig(map, TestPrivateKeyBase64);

        Assert.That(DlcMapSigner.Verify(map, sig, TestPublicKeyBase64), Is.True);
    }
}
