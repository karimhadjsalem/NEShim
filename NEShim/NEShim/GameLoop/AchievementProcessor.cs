using NEShim.Achievements;

namespace NEShim.GameLoop;

internal sealed class AchievementProcessor : IAchievementProcessor
{
    private readonly AchievementManager? _achievements;

    public AchievementProcessor(AchievementManager? achievements)
    {
        _achievements = achievements;
    }

    public void Tick() => _achievements?.Tick();
}
