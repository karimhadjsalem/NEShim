using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NEShim.Achievements;
using NEShim.SealAchievements;

// ── Usage ────────────────────────────────────────────────────────────────────
//   seal-achievements [path/to/achievements.json]
//   seal-achievements --gen-key
//
// With no arguments, looks for achievements.json in the current directory.
// --gen-key  prints a new random HMAC key and exits (copy it into AchievementSigner.cs).
// ─────────────────────────────────────────────────────────────────────────────

if (args.Length == 1 && args[0] == "--gen-key")
{
    string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    Console.WriteLine("Generated key (paste into AchievementSigner.HmacKeyBase64):");
    Console.WriteLine(key);
    return 0;
}

string path = args.Length >= 1
    ? args[0]
    : Path.Combine(Directory.GetCurrentDirectory(), "achievements.json");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    Console.Error.WriteLine("Usage: seal-achievements [path/to/achievements.json]");
    return 1;
}

var options = new JsonSerializerOptions
{
    WriteIndented               = true,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
};

Dictionary<string, GameAchievementConfig>? configs;
try
{
    string json = File.ReadAllText(path);
    configs = JsonSerializer.Deserialize<Dictionary<string, GameAchievementConfig>>(json, options);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to parse {path}: {ex.Message}");
    return 1;
}

if (configs is null || configs.Count == 0)
{
    Console.Error.WriteLine("No entries found in the file.");
    return 1;
}

foreach (var (romHash, config) in configs)
    Console.WriteLine($"\nROM {romHash[..Math.Min(12, romHash.Length)]}...  ({config.Achievements.Count} achievement(s))");

var result = SealingService.Seal(configs);

// Print per-achievement results after sealing so we can show final sig status.
foreach (var (_, config) in configs)
    foreach (var def in config.Achievements)
        Console.WriteLine(string.IsNullOrWhiteSpace(def.SteamId)
            ? "  [skip]   (no SteamId)"
            : $"  [sealed] {def.SteamId}");

string output = JsonSerializer.Serialize(configs, options);
File.WriteAllText(path, output);

Console.WriteLine($"\nDone. {result.Sealed} sealed, {result.Skipped} skipped → {path}");
return 0;
