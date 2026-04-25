using NEShim.Achievements;
using NEShim.SealAchievements;

namespace NEShim.Tests.SealAchievements;

[TestFixture]
internal class SealingServiceTests
{
    // Same test keypairs as AchievementSignerTests — test-only keys, not production.
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

    private static AchievementDef MakeDef(string steamId = "ACH_TEST", long value = 1) =>
        new()
        {
            SteamId    = steamId,
            Address    = 0xFF,
            Bytes      = 1,
            Encoding   = "binary",
            Comparison = "equals",
            Value      = value,
        };

    private static Dictionary<string, GameAchievementConfig> SingleConfig(
        params AchievementDef[] defs) =>
        new()
        {
            ["ROMHASH"] = new GameAchievementConfig { Achievements = [..defs] }
        };

    // ---- Signature correctness ----

    [Test]
    public void Seal_StampsAllDefsWithValidSignatures()
    {
        var configs = SingleConfig(MakeDef("ACH_A"), MakeDef("ACH_B"));

        SealingService.Seal(configs, TestPrivateKeyBase64);

        foreach (var def in configs["ROMHASH"].Achievements)
            Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.True, $"{def.SteamId} should verify");
    }

    [Test]
    public void Seal_ProducedSig_FailsVerificationWithDifferentPublicKey()
    {
        var configs = SingleConfig(MakeDef());

        SealingService.Seal(configs, TestPrivateKeyBase64);

        var def = configs["ROMHASH"].Achievements[0];
        Assert.That(AchievementSigner.Verify(def, OtherPublicKeyBase64), Is.False);
    }

    // ---- Skipping ----

    [Test]
    public void Seal_SkipsDefsWithNullSteamId()
    {
        var def     = MakeDef() with { SteamId = null! };
        var configs = SingleConfig(def);

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(def.Sig, Is.Null);
    }

    [Test]
    public void Seal_SkipsDefsWithEmptySteamId()
    {
        var def     = MakeDef() with { SteamId = "" };
        var configs = SingleConfig(def);

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(def.Sig, Is.Null);
    }

    [Test]
    public void Seal_SkipsDefsWithWhitespaceSteamId()
    {
        var def     = MakeDef() with { SteamId = "   " };
        var configs = SingleConfig(def);

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(def.Sig, Is.Null);
    }

    // ---- Counts ----

    [Test]
    public void Seal_ReturnsCorrectSealedCount()
    {
        var configs = SingleConfig(MakeDef("ACH_A"), MakeDef("ACH_B"), MakeDef("ACH_C"));

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Sealed, Is.EqualTo(3));
    }

    [Test]
    public void Seal_ReturnsCorrectSkippedCount()
    {
        var configs = SingleConfig(
            MakeDef("ACH_A"),
            MakeDef() with { SteamId = "" },
            MakeDef() with { SteamId = null! });

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Sealed,  Is.EqualTo(1));
        Assert.That(result.Skipped, Is.EqualTo(2));
    }

    // ---- Re-sealing ----

    [Test]
    public void Seal_Reseal_OverwritesSigWithNewValidSig()
    {
        // ECDSA-P256 is non-deterministic; the two sigs may differ but both must verify.
        var configs = SingleConfig(MakeDef());

        SealingService.Seal(configs, TestPrivateKeyBase64);
        SealingService.Seal(configs, TestPrivateKeyBase64);

        var def = configs["ROMHASH"].Achievements[0];
        Assert.That(AchievementSigner.Verify(def, TestPublicKeyBase64), Is.True);
    }

    // ---- Edge cases ----

    [Test]
    public void Seal_HandlesEmptyAchievementsList()
    {
        var configs = SingleConfig(); // no defs

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Sealed,  Is.EqualTo(0));
        Assert.That(result.Skipped, Is.EqualTo(0));
    }

    [Test]
    public void Seal_HandlesMultipleRomHashes()
    {
        var configs = new Dictionary<string, GameAchievementConfig>
        {
            ["HASH_A"] = new() { Achievements = [MakeDef("ACH_1")] },
            ["HASH_B"] = new() { Achievements = [MakeDef("ACH_2"), MakeDef("ACH_3")] },
        };

        var result = SealingService.Seal(configs, TestPrivateKeyBase64);

        Assert.That(result.Sealed, Is.EqualTo(3));
        Assert.That(AchievementSigner.Verify(configs["HASH_A"].Achievements[0], TestPublicKeyBase64), Is.True);
        Assert.That(AchievementSigner.Verify(configs["HASH_B"].Achievements[0], TestPublicKeyBase64), Is.True);
        Assert.That(AchievementSigner.Verify(configs["HASH_B"].Achievements[1], TestPublicKeyBase64), Is.True);
    }

    [Test]
    public void Seal_SealedSigIsInvalidatedBySubsequentFieldChange()
    {
        var def     = MakeDef(value: 10000);
        var configs = SingleConfig(def);
        SealingService.Seal(configs, TestPrivateKeyBase64);

        // Tampered copy: keep the original sig but change the trigger value.
        var tampered = def with { Value = 0 };

        Assert.That(AchievementSigner.Verify(tampered, TestPublicKeyBase64), Is.False);
    }
}
