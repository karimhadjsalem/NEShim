namespace NEShim.Achievements;

/// <summary>
/// Achievement configuration for a single game (one entry in achievements.json).
/// </summary>
internal sealed class GameAchievementConfig
{
    /// <summary>
    /// BizHawk memory domain name to read from.
    /// Use "System Bus" (full 64KB NES address space) for standard NES RAM addresses.
    /// Defaults to "System Bus".
    /// </summary>
    public string MemoryDomain { get; set; } = "System Bus";

    /// <summary>All achievements for this game.</summary>
    public List<AchievementDef> Achievements { get; set; } = [];
}
