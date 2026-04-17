using NEShim.Achievements;
using NEShim.SealAchievements;

namespace NEShim.Tests.SealAchievements;

[TestFixture]
internal class SealingServiceTests
{
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

        SealingService.Seal(configs);

        foreach (var def in configs["ROMHASH"].Achievements)
            Assert.That(AchievementSigner.Verify(def), Is.True, $"{def.SteamId} should verify");
    }

    // ---- Skipping ----

    [Test]
    public void Seal_SkipsDefsWithNullSteamId()
    {
        var def     = MakeDef() with { SteamId = null! };
        var configs = SingleConfig(def);

        var result = SealingService.Seal(configs);

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(def.Sig, Is.Null);
    }

    [Test]
    public void Seal_SkipsDefsWithEmptySteamId()
    {
        var def     = MakeDef() with { SteamId = "" };
        var configs = SingleConfig(def);

        var result = SealingService.Seal(configs);

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(def.Sig, Is.Null);
    }

    [Test]
    public void Seal_SkipsDefsWithWhitespaceSteamId()
    {
        var def     = MakeDef() with { SteamId = "   " };
        var configs = SingleConfig(def);

        var result = SealingService.Seal(configs);

        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(def.Sig, Is.Null);
    }

    // ---- Counts ----

    [Test]
    public void Seal_ReturnsCorrectSealedCount()
    {
        var configs = SingleConfig(MakeDef("ACH_A"), MakeDef("ACH_B"), MakeDef("ACH_C"));

        var result = SealingService.Seal(configs);

        Assert.That(result.Sealed, Is.EqualTo(3));
    }

    [Test]
    public void Seal_ReturnsCorrectSkippedCount()
    {
        var configs = SingleConfig(
            MakeDef("ACH_A"),
            MakeDef() with { SteamId = "" },
            MakeDef() with { SteamId = null! });

        var result = SealingService.Seal(configs);

        Assert.That(result.Sealed,  Is.EqualTo(1));
        Assert.That(result.Skipped, Is.EqualTo(2));
    }

    // ---- Idempotency ----

    [Test]
    public void Seal_IsIdempotent_ResealProducesSameValidSig()
    {
        var configs = SingleConfig(MakeDef());

        SealingService.Seal(configs);
        string firstSig = configs["ROMHASH"].Achievements[0].Sig!;

        SealingService.Seal(configs);
        string secondSig = configs["ROMHASH"].Achievements[0].Sig!;

        Assert.That(secondSig, Is.EqualTo(firstSig));
        Assert.That(AchievementSigner.Verify(configs["ROMHASH"].Achievements[0]), Is.True);
    }

    // ---- Edge cases ----

    [Test]
    public void Seal_HandlesEmptyAchievementsList()
    {
        var configs = SingleConfig(); // no defs

        var result = SealingService.Seal(configs);

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

        var result = SealingService.Seal(configs);

        Assert.That(result.Sealed, Is.EqualTo(3));
        Assert.That(AchievementSigner.Verify(configs["HASH_A"].Achievements[0]), Is.True);
        Assert.That(AchievementSigner.Verify(configs["HASH_B"].Achievements[0]), Is.True);
        Assert.That(AchievementSigner.Verify(configs["HASH_B"].Achievements[1]), Is.True);
    }

    [Test]
    public void Seal_SealedSigIsInvalidatedBySubsequentFieldChange()
    {
        var def     = MakeDef(value: 10000);
        var configs = SingleConfig(def);
        SealingService.Seal(configs);

        // Produce a tampered copy: keep the original sig but change the trigger value.
        // The with-expression copies Sig from def, so the tampered def carries a sig
        // that was signed for value=10000 — verify must reject it.
        var tampered = def with { Value = 0 };

        Assert.That(AchievementSigner.Verify(tampered), Is.False);
    }
}
