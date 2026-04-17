namespace NEShim.Achievements;

/// <summary>
/// Describes a single Steam achievement and the memory condition that triggers it.
/// </summary>
internal sealed record AchievementDef
{
    /// <summary>Steam achievement API name (e.g. "ACH_WIN_ONE_GAME").</summary>
    public string SteamId { get; init; } = "";

    /// <summary>NES memory address to watch (use NES bus address, e.g. 0x00FF).</summary>
    public int Address { get; init; }

    /// <summary>
    /// Number of bytes to read starting at <see cref="Address"/>. Supported: 1, 2, 3, 4.
    /// Bytes are assembled into a single integer before comparison.
    /// </summary>
    public int Bytes { get; init; } = 1;

    /// <summary>
    /// When true, the first byte at <see cref="Address"/> is the most significant byte.
    /// Defaults to false (little-endian, NES native). Set to true for BCD scores where
    /// the most significant digit is stored at the lowest address.
    /// </summary>
    public bool BigEndian { get; init; } = false;

    /// <summary>
    /// How to interpret the raw bytes before comparison.
    /// "binary" — standard integer (default).
    /// "bcd"    — binary-coded decimal; each nibble is one decimal digit.
    /// </summary>
    public string Encoding { get; init; } = "binary";

    /// <summary>
    /// Comparison operator applied as: <c>readValue {comparison} Value</c>.
    /// Supported: "equals", "greaterOrEqual", "greaterThan".
    /// </summary>
    public string Comparison { get; init; } = "equals";

    /// <summary>The threshold value used in the comparison.</summary>
    public long Value { get; init; }
}
