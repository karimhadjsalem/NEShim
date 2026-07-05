using NEShim.Achievements;
using NEShim.Emulation;
using NEShim.GameLoop;
using NSubstitute;

namespace NEShim.Tests.GameLoop;

[TestFixture]
internal class AchievementProcessorTests
{
    [Test]
    public void Tick_WhenNullManager_DoesNotThrow()
    {
        var processor = new AchievementProcessor(null);
        Assert.DoesNotThrow(() => processor.Tick());
    }

    [Test]
    public void Tick_WhenManagerSet_InvokesManagerTick()
    {
        var domain = Substitute.For<IMemoryDomain>();
        domain.PeekByte(0).Returns((byte)1);
        var domains = new Dictionary<string, IMemoryDomain> { ["System Bus"] = domain };
        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_TEST",
                    Address    = 0,
                    Bytes      = 1,
                    Encoding   = "binary",
                    Comparison = "equals",
                    Value      = 1,
                }
            ]
        };

        bool unlocked = false;
        var manager = new AchievementManager(domains, config, () => true, _ => unlocked = true);
        var processor = new AchievementProcessor(manager);

        processor.Tick();

        Assert.That(unlocked, Is.True);
    }

    [Test]
    public void Tick_WhenManagerSet_DoesNotFireTwiceForSameAchievement()
    {
        var domain = Substitute.For<IMemoryDomain>();
        domain.PeekByte(0).Returns((byte)1);
        var domains = new Dictionary<string, IMemoryDomain> { ["System Bus"] = domain };
        var config = new GameAchievementConfig
        {
            MemoryDomain = "System Bus",
            Achievements =
            [
                new AchievementDef
                {
                    SteamId    = "ACH_TEST",
                    Address    = 0,
                    Bytes      = 1,
                    Encoding   = "binary",
                    Comparison = "equals",
                    Value      = 1,
                }
            ]
        };

        int unlockCount = 0;
        var manager = new AchievementManager(domains, config, () => true, _ => unlockCount++);
        var processor = new AchievementProcessor(manager);

        processor.Tick();
        processor.Tick();

        Assert.That(unlockCount, Is.EqualTo(1));
    }
}
