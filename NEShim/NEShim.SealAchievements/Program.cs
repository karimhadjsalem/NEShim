using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NEShim.Achievements;
using NEShim.SealAchievements;

// ── Usage ────────────────────────────────────────────────────────────────────
//   seal-achievements --gen-keypair
//   seal-achievements --key-file <private_key_file> [path/to/achievements.json]
//   seal-achievements --key <base64_private_key>    [path/to/achievements.json]
//   seal-achievements --key-env <ENV_VAR>           [path/to/achievements.json]
//
// --gen-keypair  generates a new ECDSA-P256 keypair and exits.
//                Embed the public key in AchievementSigner.DefaultPublicKeyBase64 (source build)
//                or set achievementPublicKey in config.json (pre-built release).
//                Store the private key outside source control.
//
// --key          path to a file containing the base64-encoded private key.
// --key-env      name of an environment variable holding the base64-encoded private key.
// ─────────────────────────────────────────────────────────────────────────────

if (args.Length == 1 && args[0] == "--gen-keypair")
{
    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    string privateKey = Convert.ToBase64String(ecdsa.ExportECPrivateKey());
    string publicKey  = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
    Console.WriteLine("Private key (keep secret — never commit; store in 1Password, a local file, or a CI secret):");
    Console.WriteLine(privateKey);
    Console.WriteLine();
    Console.WriteLine("Public key (embed in AchievementSigner.DefaultPublicKeyBase64 OR set as achievementPublicKey in config.json):");
    Console.WriteLine(publicKey);
    return 0;
}

// ── Load private key ─────────────────────────────────────────────────────────

string? privateKeyBase64 = null;

int fileArgOffset = 0;
if (args.Length >= 2 && args[0] == "--key-file")
{
    string keyFile = args[1];
    if (!File.Exists(keyFile))
    {
        Console.Error.WriteLine($"Key file not found: {keyFile}");
        return 1;
    }
    privateKeyBase64 = File.ReadAllText(keyFile).Trim();
    fileArgOffset = 2;
}
else if (args.Length >= 2 && args[0] == "--key-env")
{
    string envVar = args[1];
    privateKeyBase64 = Environment.GetEnvironmentVariable(envVar);
    if (string.IsNullOrEmpty(privateKeyBase64))
    {
        Console.Error.WriteLine($"Environment variable '{envVar}' is not set or empty.");
        return 1;
    }
    fileArgOffset = 2;
}
else if (args.Length >= 2 && args[0] == "--key")
{
    privateKeyBase64 = args[1];
    if (string.IsNullOrEmpty(privateKeyBase64))
    {
        Console.Error.WriteLine($"Key parameter is missing or empty.");
        return 1;
    }
    fileArgOffset = 2;
}
else
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  seal-achievements --gen-keypair");
    Console.Error.WriteLine("  seal-achievements --key-file <private_key_file> [achievements.json]");
    Console.Error.WriteLine("  seal-achievements --key-env <ENV_VAR> [achievements.json]");
    Console.Error.WriteLine("  seal-achievements --key <private_key> [achievements.json]");
    return 1;
}

// Validate the private key is well-formed before proceeding
try { Convert.FromBase64String(privateKeyBase64); }
catch
{
    Console.Error.WriteLine("Private key is not valid base64.");
    return 1;
}

// ── Load achievements.json ───────────────────────────────────────────────────

string path = args.Length > fileArgOffset
    ? args[fileArgOffset]
    : Path.Combine(Directory.GetCurrentDirectory(), "achievements.json");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    Console.Error.WriteLine("Usage: seal-achievements --key-file <file> [path/to/achievements.json]");
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

var result = SealingService.Seal(configs, privateKeyBase64);

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
