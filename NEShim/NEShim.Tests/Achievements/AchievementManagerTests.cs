using BizHawk.Emulation.Common;
using NEShim.Achievements;
using NSubstitute;

namespace NEShim.Tests.Achievements;

[TestFixture]
internal class AchievementManagerTests
{
    private IMemoryDomains _domains = null!;
    private MemoryDomain   _domain  = null!;

    [SetUp]
    public void SetUp()
    {
        _domain  = Substitute.For<MemoryDomain>();
        _domains = Substitute.For<IMemoryDomains>();
        _domains["System Bus"].Returns(_domain);
        _domains.MainMemory.Returns(_domain);
    }

    // ---- Factory helpers ----

    private AchievementManager CreateManager(
        GameAchievementConfig config,
        bool                  statsReady,
        out List<string>      unlocked)
    {
        var ids = new List<string>();
        unlocked = ids;
        return new AchievementManager(_domains, config, () => statsReady, id => ids.Add(id));
    }

    private static GameAchievementConfig SingleEquals(
        int address, byte value, string steamId = "ACH_TEST") =>
        new()
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = steamId,
                    Address    = address,
                    Bytes      = 1,
                    Encoding   = "binary",
                    Comparison = "equals",
                    Value      = value,
                }
            ]
        };

    // ---- Stats readiness ----

    [Test]
    public void Tick_WhenStatsNotReady_DoesNotUnlock()
    {
        _domain.PeekByte(0).Returns((byte)1);
        var manager = CreateManager(SingleEquals(0, 1), statsReady: false, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.Empty);
    }

    // ---- Equals comparison ----

    [Test]
    public void Tick_WhenAddressEqualsValue_UnlocksAchievement()
    {
        _domain.PeekByte(0xFF).Returns((byte)1);
        var manager = CreateManager(SingleEquals(0xFF, 1), statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.EqualTo(new[] { "ACH_TEST" }));
    }

    [Test]
    public void Tick_WhenValueDoesNotMatch_DoesNotUnlock()
    {
        _domain.PeekByte(0xFF).Returns((byte)0);
        var manager = CreateManager(SingleEquals(0xFF, 1), statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.Empty);
    }

    // ---- Session dedup ----

    [Test]
    public void Tick_WhenAlreadyFiredThisSession_DoesNotUnlockAgain()
    {
        _domain.PeekByte(0).Returns((byte)1);
        var manager = CreateManager(SingleEquals(0, 1), statsReady: true, out var unlocked);

        manager.Tick(); // fires
        manager.Tick(); // value still 1 — should not fire again

        Assert.That(unlocked.Count, Is.EqualTo(1));
    }

    // ---- GreaterOrEqual ----

    [Test]
    public void Tick_GreaterOrEqual_FiresAtExactThreshold()
    {
        _domain.PeekByte(0).Returns((byte)100);
        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_SCORE",
                    Address    = 0,
                    Bytes      = 1,
                    Encoding   = "binary",
                    Comparison = "greaterOrEqual",
                    Value      = 100,
                }
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Has.Count.EqualTo(1));
    }

    [Test]
    public void Tick_GreaterOrEqual_DoesNotFireBelowThreshold()
    {
        _domain.PeekByte(0).Returns((byte)99);
        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_SCORE",
                    Address    = 0,
                    Bytes      = 1,
                    Encoding   = "binary",
                    Comparison = "greaterOrEqual",
                    Value      = 100,
                }
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.Empty);
    }

    // ---- GreaterThan ----

    [Test]
    public void Tick_GreaterThan_DoesNotFireAtExactThreshold()
    {
        _domain.PeekByte(0).Returns((byte)50);
        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_SCORE",
                    Address    = 0,
                    Bytes      = 1,
                    Encoding   = "binary",
                    Comparison = "greaterThan",
                    Value      = 50,
                }
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.Empty);
    }

    // ---- BCD decoding ----

    [Test]
    public void Tick_BcdEncoding_DecodesCorrectly()
    {
        // Big-endian BCD bytes [0x12, 0x34, 0x56] → 123456
        _domain.PeekByte(0x0072).Returns((byte)0x12);
        _domain.PeekByte(0x0073).Returns((byte)0x34);
        _domain.PeekByte(0x0074).Returns((byte)0x56);

        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_SCORE",
                    Address    = 0x0072,
                    Bytes      = 3,
                    BigEndian  = true,
                    Encoding   = "bcd",
                    Comparison = "greaterOrEqual",
                    Value      = 123456,
                }
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Has.Count.EqualTo(1));
    }

    [Test]
    public void Tick_BcdEncoding_DoesNotFireWhenBelowThreshold()
    {
        // Big-endian BCD [0x12, 0x34, 0x55] → 123455 (one below 123456)
        _domain.PeekByte(0x0072).Returns((byte)0x12);
        _domain.PeekByte(0x0073).Returns((byte)0x34);
        _domain.PeekByte(0x0074).Returns((byte)0x55);

        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_SCORE",
                    Address    = 0x0072,
                    Bytes      = 3,
                    BigEndian  = true,
                    Encoding   = "bcd",
                    Comparison = "greaterOrEqual",
                    Value      = 123456,
                }
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.Empty);
    }

    // ---- Multi-byte binary (little-endian) ----

    [Test]
    public void Tick_MultiByteLE_ReadsCorrectly()
    {
        // 16-bit LE: addr=0x10 → 0xE8 (LSB), addr=0x11 → 0x03 (MSB) → 0x03E8 = 1000
        _domain.PeekByte(0x10).Returns((byte)0xE8);
        _domain.PeekByte(0x11).Returns((byte)0x03);

        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_SCORE",
                    Address    = 0x10,
                    Bytes      = 2,
                    BigEndian  = false,
                    Encoding   = "binary",
                    Comparison = "equals",
                    Value      = 1000,
                }
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Has.Count.EqualTo(1));
    }

    // ---- Multiple achievements in one config ----

    [Test]
    public void Tick_MultipleAchievements_FiresOnlyMatchingOnes()
    {
        _domain.PeekByte(0x00).Returns((byte)1); // matches first
        _domain.PeekByte(0x01).Returns((byte)0); // does not match second

        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef { SteamId = "ACH_A", Address = 0x00, Bytes = 1, Encoding = "binary", Comparison = "equals", Value = 1 },
                new AchievementDef { SteamId = "ACH_B", Address = 0x01, Bytes = 1, Encoding = "binary", Comparison = "equals", Value = 1 },
            ]
        };
        var manager = CreateManager(config, statsReady: true, out var unlocked);

        manager.Tick();

        Assert.That(unlocked, Is.EqualTo(new[] { "ACH_A" }));
    }
}
