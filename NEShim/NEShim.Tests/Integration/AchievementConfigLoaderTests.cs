using System.IO;
using System.Text.Json;
using NEShim.Achievements;

namespace NEShim.Tests.Integration;

/// <summary>
/// Integration tests for AchievementConfigLoader — these cross the file system boundary
/// and are kept separate from unit tests per the project testing guidelines.
/// </summary>
[TestFixture]
internal class AchievementConfigLoaderTests
{
    // Test keypair A — signs defs that should verify successfully.
    private const string TestPrivateKeyBase64 =
        "MHcCAQEEIJX+aCzo2G6R5dUkmZWSRbUDpJMqj57dNvMZBNRhdjoqoAoGCCqGSM49AwEHoUQDQgAE" +
        "aAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3S" +
        "LZyvfmHJhw==";
    private const string TestPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaAlvnWP1jf2S6o45HLmZB0se6yQFFdTU3B/IZWrG" +
        "1UrpLxMjW3kP5m6l5ZK6wo2JjZ2AA7Y0JK3SLZyvfmHJhw==";

    // Test keypair B public key — used for wrong-key rejection tests.
    private const string OtherPublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAgCiqeoxm0UuLd9EiZt/ONVA6SybkplDzznY8s1f" +
        "YPCo1hbCAiaFWf3bJl33Sz2qXdYvH+UqQDaSQio5nP3SpQ==";

    private const string RomHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private string _tempFile = null!;

    [SetUp]
    public void SetUp()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"achievements_{Guid.NewGuid()}.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // ---- Helpers ----

    private GameAchievementConfig? Load(string configPublicKey = "", string? embeddedKey = null) =>
        AchievementConfigLoader.LoadFrom(RomHash, configPublicKey, _tempFile, embeddedKey);

    private void WriteFile(Dictionary<string, object> data) =>
        File.WriteAllText(_tempFile, JsonSerializer.Serialize(data));

    private AchievementDef SealedDef(string steamId = "ACH_TEST", long value = 1)
    {
        var def = new AchievementDef
        {
            SteamId    = steamId,
            Address    = 0xFF,
            Bytes      = 1,
            Encoding   = "binary",
            Comparison = "equals",
            Value      = value,
        };
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);
        return def;
    }

    private void WriteFileWithDefs(params AchievementDef[] defs)
    {
        var payload = new Dictionary<string, object>
        {
            [RomHash] = new
            {
                memoryDomain = "System Bus",
                achievements = defs,
            }
        };
        File.WriteAllText(_tempFile, JsonSerializer.Serialize(payload));
    }

    // ---- File access ----

    [Test]
    public void LoadFrom_WhenFileNotFound_ReturnsNull()
    {
        File.Delete(_tempFile); // ensure it does not exist

        var result = Load(TestPublicKeyBase64);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void LoadFrom_WhenJsonIsInvalid_ReturnsNull()
    {
        File.WriteAllText(_tempFile, "{ not valid json !!!");

        var result = Load(TestPublicKeyBase64);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void LoadFrom_WhenHashNotInFile_ReturnsNull()
    {
        WriteFile(new Dictionary<string, object>
        {
            ["DIFFERENTHASH"] = new { memoryDomain = "System Bus", achievements = Array.Empty<object>() }
        });

        var result = Load(TestPublicKeyBase64);

        Assert.That(result, Is.Null);
    }

    // ---- Key precedence ----

    [Test]
    public void LoadFrom_WhenBothKeysAbsent_ReturnsNull()
    {
        WriteFileWithDefs(SealedDef());

        var result = Load(configPublicKey: "", embeddedKey: null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void LoadFrom_WithConfigKeyOnly_LoadsVerifiedDefinitions()
    {
        WriteFileWithDefs(SealedDef("ACH_A"), SealedDef("ACH_B"));

        var result = Load(configPublicKey: TestPublicKeyBase64, embeddedKey: null);

        Assert.That(result,                    Is.Not.Null);
        Assert.That(result!.Achievements.Count, Is.EqualTo(2));
    }

    [Test]
    public void LoadFrom_WithEmbeddedKeyOnly_LoadsVerifiedDefinitions()
    {
        WriteFileWithDefs(SealedDef());

        // embeddedKey is set; configPublicKey is empty — embedded key must be used.
        var result = Load(configPublicKey: "", embeddedKey: TestPublicKeyBase64);

        Assert.That(result,                    Is.Not.Null);
        Assert.That(result!.Achievements.Count, Is.EqualTo(1));
    }

    [Test]
    public void LoadFrom_EmbeddedKeyTakesPrecedenceOverConfigKey()
    {
        // Defs are signed with TestPrivateKeyBase64 (matches TestPublicKeyBase64).
        // configPublicKey is the wrong key; embeddedKey is the correct key.
        // If embedded key takes precedence, the defs should load.
        WriteFileWithDefs(SealedDef());

        var result = Load(configPublicKey: OtherPublicKeyBase64, embeddedKey: TestPublicKeyBase64);

        Assert.That(result,                    Is.Not.Null);
        Assert.That(result!.Achievements.Count, Is.EqualTo(1));
    }

    [Test]
    public void LoadFrom_WhenConfigKeyIsCorrectButEmbeddedKeyIsWrong_RejectsAllDefs()
    {
        // Defs are signed with TestPrivateKeyBase64 (matches TestPublicKeyBase64).
        // embeddedKey is the wrong key and takes precedence — all defs must be rejected.
        WriteFileWithDefs(SealedDef());

        var result = Load(configPublicKey: TestPublicKeyBase64, embeddedKey: OtherPublicKeyBase64);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void LoadFrom_WithWrongConfigKey_RejectsAllDefinitions()
    {
        WriteFileWithDefs(SealedDef());

        var result = Load(configPublicKey: OtherPublicKeyBase64, embeddedKey: null);

        Assert.That(result, Is.Null);
    }

    // ---- Signature verification ----

    [Test]
    public void LoadFrom_DropsSigsInvalidatedByFieldTampering()
    {
        // Seal with value=100, then write with value=999 — the stored sig no longer matches.
        var tampered = SealedDef(value: 100) with { Value = 999 };
        WriteFileWithDefs(tampered);

        var result = Load(TestPublicKeyBase64);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void LoadFrom_LoadsOnlyValidDefsWhenMixedSigs()
    {
        var valid   = SealedDef("ACH_GOOD");
        var invalid = SealedDef("ACH_BAD") with { Value = 999 }; // tamper after sealing
        WriteFileWithDefs(valid, invalid);

        var result = Load(TestPublicKeyBase64);

        Assert.That(result,                        Is.Not.Null);
        Assert.That(result!.Achievements.Count,     Is.EqualTo(1));
        Assert.That(result.Achievements[0].SteamId, Is.EqualTo("ACH_GOOD"));
    }

    [Test]
    public void LoadFrom_WhenAllDefsLackSigs_ReturnsNull()
    {
        var def = new AchievementDef
        {
            SteamId    = "ACH_UNSIGNED",
            Address    = 0xFF,
            Bytes      = 1,
            Encoding   = "binary",
            Comparison = "equals",
            Value      = 1,
            Sig        = null,
        };
        WriteFileWithDefs(def);

        var result = Load(TestPublicKeyBase64);

        Assert.That(result, Is.Null);
    }

    // ---- Returned data ----

    [Test]
    public void LoadFrom_ReturnsCorrectMemoryDomain()
    {
        WriteFileWithDefs(SealedDef());

        var result = Load(TestPublicKeyBase64);

        Assert.That(result!.MemoryDomain, Is.EqualTo("System Bus"));
    }

    [Test]
    public void LoadFrom_PreservesDefFieldsOnLoad()
    {
        var def = new AchievementDef
        {
            SteamId    = "ACH_SCORE",
            Address    = 0x0071,
            Bytes      = 3,
            BigEndian  = true,
            Encoding   = "bcd",
            Comparison = "greaterOrEqual",
            Value      = 10000,
        };
        def.Sig = AchievementSigner.ComputeSig(def, TestPrivateKeyBase64);
        WriteFileWithDefs(def);

        var loaded = Load(TestPublicKeyBase64)!.Achievements[0];

        Assert.That(loaded.SteamId,    Is.EqualTo("ACH_SCORE"));
        Assert.That(loaded.Address,    Is.EqualTo(0x0071));
        Assert.That(loaded.Bytes,      Is.EqualTo(3));
        Assert.That(loaded.BigEndian,  Is.True);
        Assert.That(loaded.Encoding,   Is.EqualTo("bcd"));
        Assert.That(loaded.Comparison, Is.EqualTo("greaterOrEqual"));
        Assert.That(loaded.Value,      Is.EqualTo(10000));
    }
}
