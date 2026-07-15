using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NEShim.Achievements;

// ── Usage ────────────────────────────────────────────────────────────────────
//   pub-utils --gen-keypair
//   pub-utils --key-file <private_key_file> [path/to/achievements.json]
//   pub-utils --key <base64_private_key>    [path/to/achievements.json]
//   pub-utils --key-env <ENV_VAR>           [path/to/achievements.json]
//   pub-utils --validate [--pub-key <base64>] [path/to/package/dir]
//   pub-utils --seal-dlc-map --key-file <private_key_file> [games/multigame.json]
//   pub-utils --seal-dlc-map --key-env <ENV_VAR>           [games/multigame.json]
//   pub-utils --seal-dlc-map --key <base64_private_key>    [games/multigame.json]
//
// --gen-keypair  generates a new ECDSA-P256 keypair and exits. Run this TWICE if you use both
//                achievement signing and DLC-map signing (--seal-dlc-map) — they MUST use two
//                separate, independent keypairs. Never reuse one keypair for both: they protect
//                different things, and a leaked/rotated key for one must not force touching the
//                other. Generating a second keypair costs nothing.
//                Achievements: embed the public key in AchievementSigner.EmbeddedPublicKeyBase64
//                (source build) or set achievementPublicKey in config.json (pre-built release).
//                DLC map: embed the public key in DlcMapSigner.EmbeddedPublicKeyBase64 — this
//                one has NO config.json equivalent, by design (see --seal-dlc-map below).
//                Store each private key outside source control either way.
//
// --key-file     path to a file containing the base64-encoded private key.
// --key-env      name of an environment variable holding the base64-encoded private key.
//
// --validate     verifies all achievement signatures in a package directory (defaults to cwd).
//                Public key resolution (same precedence as the game at runtime):
//                  1. --pub-key <base64>  explicit override
//                  2. AchievementSigner.EmbeddedPublicKeyBase64  baked into the compiled tool
//                  3. achievementPublicKey in config.json in the package directory
//
// --seal-dlc-map signs the gameDlcAppIds map in a multi-game shell manifest (games/multigame.json)
//                and writes the result to gameDlcAppIdsSignature in the same file, in place.
//                Re-run any time gameDlcAppIds changes. This signature has NO effect until the
//                matching public key is compiled into DlcMapSigner.EmbeddedPublicKeyBase64 and
//                the game is rebuilt from source — unlike achievements, there is deliberately no
//                config.json-driven public key for this: a tampered install could otherwise just
//                supply its own matching keypair alongside a forged map.
//                IMPORTANT: use a keypair generated separately from your achievement-signing
//                keypair (--gen-keypair) — never the same one for both. See CLAUDE.md
//                ("DLC ownership anti-tamper") or the docs site's multi-game guide.
// ─────────────────────────────────────────────────────────────────────────────

const int RomHashDisplayLength = 12; // truncation length for the ROM hash shown in console output

// Resolves a private key from --key-file/--key-env/--key starting at args[startIndex] (shared by
// the main sealing path and --seal-dlc-map, which differ only in where their own leading flag(s)
// push this sub-parse's start index to). Returns null and writes an error/usage message to
// stderr on any failure — missing/unrecognized flag, missing file, unset env var, empty key
// value, or malformed base64 — so callers can just check for null and return 1.
static (string? privateKeyBase64, int nextArgIndex) TryResolvePrivateKeyFromArgs(
    string[] args, int startIndex, string usage)
{
    string? privateKeyBase64;
    int nextArgIndex;

    if (args.Length >= startIndex + 2 && args[startIndex] == "--key-file")
    {
        string keyFile = args[startIndex + 1];
        if (!File.Exists(keyFile))
        {
            Console.Error.WriteLine($"Key file not found: {keyFile}");
            return (null, 0);
        }
        privateKeyBase64 = File.ReadAllText(keyFile).Trim();
        nextArgIndex = startIndex + 2;
    }
    else if (args.Length >= startIndex + 2 && args[startIndex] == "--key-env")
    {
        string envVar = args[startIndex + 1];
        privateKeyBase64 = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrEmpty(privateKeyBase64))
        {
            Console.Error.WriteLine($"Environment variable '{envVar}' is not set or empty.");
            return (null, 0);
        }
        nextArgIndex = startIndex + 2;
    }
    else if (args.Length >= startIndex + 2 && args[startIndex] == "--key")
    {
        privateKeyBase64 = args[startIndex + 1];
        if (string.IsNullOrEmpty(privateKeyBase64))
        {
            Console.Error.WriteLine("Key parameter is missing or empty.");
            return (null, 0);
        }
        nextArgIndex = startIndex + 2;
    }
    else
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine(usage);
        return (null, 0);
    }

    try { Convert.FromBase64String(privateKeyBase64); }
    catch
    {
        Console.Error.WriteLine("Private key is not valid base64.");
        return (null, 0);
    }

    return (privateKeyBase64, nextArgIndex);
}

if (args.Length >= 1 && args[0] == "--validate")
{
    // ── Resolve optional --pub-key override ──────────────────────────────────
    string? explicitKey = null;
    int validateArgOffset = 1;
    if (args.Length >= 3 && args[1] == "--pub-key")
    {
        explicitKey = args[2];
        validateArgOffset = 3;
    }

    // ── Resolve package directory ────────────────────────────────────────────
    string dir = args.Length > validateArgOffset
        ? args[validateArgOffset]
        : Directory.GetCurrentDirectory();

    if (!Directory.Exists(dir))
    {
        Console.Error.WriteLine($"Directory not found: {dir}");
        return 1;
    }

    // ── Resolve public key (three-step precedence) ───────────────────────────
    string? publicKey = null;

    if (!string.IsNullOrEmpty(explicitKey))
    {
        publicKey = explicitKey;
    }
    else if (!string.IsNullOrEmpty(AchievementSigner.EmbeddedPublicKeyBase64))
    {
        publicKey = AchievementSigner.EmbeddedPublicKeyBase64;
        Console.WriteLine("Using public key embedded in tool binary.");
    }
    else
    {
        string configPath = Path.Combine(dir, "config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"config.json not found in {dir}");
            Console.Error.WriteLine("Provide a public key with --pub-key or ensure config.json contains achievementPublicKey.");
            return 1;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            publicKey = doc.RootElement.TryGetProperty("achievementPublicKey", out var el)
                ? el.GetString()
                : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read config.json: {ex.Message}");
            return 1;
        }

        if (string.IsNullOrEmpty(publicKey))
        {
            Console.Error.WriteLine("achievementPublicKey is not set in config.json.");
            Console.Error.WriteLine("Provide a public key with --pub-key or set achievementPublicKey in config.json.");
            return 1;
        }
    }

    // ── Load achievements.json ───────────────────────────────────────────────
    string achievementsPath = Path.Combine(dir, "achievements.json");
    if (!File.Exists(achievementsPath))
    {
        Console.Error.WriteLine($"achievements.json not found in {dir}");
        return 1;
    }

    var validateOptions = new JsonSerializerOptions
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
    };

    Dictionary<string, GameAchievementConfig>? validateConfigs;
    try
    {
        validateConfigs = JsonSerializer.Deserialize<Dictionary<string, GameAchievementConfig>>(
            File.ReadAllText(achievementsPath), validateOptions);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to parse achievements.json: {ex.Message}");
        return 1;
    }

    if (validateConfigs is null || validateConfigs.Count == 0)
    {
        Console.Error.WriteLine("No entries found in achievements.json.");
        return 1;
    }

    // ── Validate ─────────────────────────────────────────────────────────────
    int totalValid = 0, totalFailed = 0;
    foreach (var (romHash, config) in validateConfigs)
    {
        Console.WriteLine($"\nROM {romHash[..Math.Min(RomHashDisplayLength, romHash.Length)]}…  ({config.Achievements.Count} achievement(s))");

        var romResult = ValidationService.Validate(
            new Dictionary<string, GameAchievementConfig> { [romHash] = config },
            publicKey!,
            (steamId, ok) => Console.WriteLine(ok
                ? $"  [OK]   {steamId}"
                : $"  [FAIL] {steamId} — missing or invalid signature"));

        totalValid  += romResult.Valid;
        totalFailed += romResult.Failed;
    }

    Console.WriteLine(totalFailed == 0
        ? $"\nResult: {totalValid}/{totalValid + totalFailed} valid — All OK."
        : $"\nResult: {totalValid}/{totalValid + totalFailed} valid — {totalFailed} FAILED.");

    return totalFailed > 0 ? 1 : 0;
}

if (args.Length == 1 && args[0] == "--gen-keypair")
{
    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    string privateKey = Convert.ToBase64String(ecdsa.ExportECPrivateKey());
    string publicKey  = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
    Console.WriteLine("Private key (keep secret — never commit; store in 1Password, a local file, or a CI secret):");
    Console.WriteLine(privateKey);
    Console.WriteLine();
    Console.WriteLine("Public key (embed in AchievementSigner.EmbeddedPublicKeyBase64 OR set as achievementPublicKey in config.json,");
    Console.WriteLine("or embed in DlcMapSigner.EmbeddedPublicKeyBase64 for DLC-map signing — see --seal-dlc-map):");
    Console.WriteLine(publicKey);
    Console.WriteLine();
    Console.WriteLine("IMPORTANT: if you use BOTH achievement signing and DLC-map signing, these must be TWO");
    Console.WriteLine("SEPARATE keypairs. Do not reuse this one for both purposes — run --gen-keypair again");
    Console.WriteLine("for the second one.");
    return 0;
}

if (args.Length >= 1 && args[0] == "--seal-dlc-map")
{
    const string dlcMapUsage =
        "  pub-utils --seal-dlc-map --key-file <private_key_file> [games/multigame.json]\n" +
        "  pub-utils --seal-dlc-map --key-env <ENV_VAR>           [games/multigame.json]\n" +
        "  pub-utils --seal-dlc-map --key <private_key>            [games/multigame.json]";

    var (dlcPrivateKeyBase64, dlcFileArgOffset) = TryResolvePrivateKeyFromArgs(args, startIndex: 1, dlcMapUsage);
    if (dlcPrivateKeyBase64 is null) return 1;

    string manifestPath = args.Length > dlcFileArgOffset
        ? args[dlcFileArgOffset]
        : Path.Combine(Directory.GetCurrentDirectory(), "games", "multigame.json");

    if (!File.Exists(manifestPath))
    {
        Console.Error.WriteLine($"File not found: {manifestPath}");
        Console.Error.WriteLine("Usage: pub-utils --seal-dlc-map --key-file <file> [path/to/multigame.json]");
        return 1;
    }

    JsonObject manifestRoot;
    try
    {
        manifestRoot = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("document is empty or not a JSON object");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to parse {manifestPath}: {ex.Message}");
        return 1;
    }

    // gameDlcAppIds is read with an exact camelCase key match (unlike the full AppConfig-based
    // runtime load path, which is case-insensitive) — matches this tool's existing --validate
    // lookup of achievementPublicKey. Author games/multigame.json with camelCase keys.
    var gameDlcAppIds = new Dictionary<string, uint>();
    if (manifestRoot.TryGetPropertyValue("gameDlcAppIds", out var mapNode) && mapNode is JsonObject mapObject)
    {
        foreach (var (gameId, valueNode) in mapObject)
        {
            if (valueNode is null) continue;
            gameDlcAppIds[gameId] = valueNode.GetValue<uint>();
        }
    }

    if (gameDlcAppIds.Count == 0)
    {
        Console.Error.WriteLine($"No gameDlcAppIds entries found in {manifestPath} — nothing to sign.");
        Console.Error.WriteLine("Add an entry for every game once signing is opted into, including bundled");
        Console.Error.WriteLine("games (list them with value 0) — a signed map must be complete.");
        return 1;
    }

    Console.WriteLine($"Signing {gameDlcAppIds.Count} gameDlcAppIds entr{(gameDlcAppIds.Count == 1 ? "y" : "ies")}:");
    foreach (var (gameId, appId) in gameDlcAppIds.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {gameId} = {appId}");

    string dlcSignature = DlcMapSigner.ComputeSig(gameDlcAppIds, dlcPrivateKeyBase64);
    manifestRoot["gameDlcAppIdsSignature"] = dlcSignature;

    File.WriteAllText(manifestPath, manifestRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine();
    Console.WriteLine($"Done. gameDlcAppIdsSignature written to {manifestPath}.");
    Console.WriteLine();
    Console.WriteLine("IMPORTANT: this signature has no effect until the matching public key is compiled");
    Console.WriteLine("into DlcMapSigner.EmbeddedPublicKeyBase64 (NEShim.Signing/DlcMapSigner.cs)");
    Console.WriteLine("and the game is rebuilt from source — there is no config.json equivalent, by design.");
    Console.WriteLine("Re-run this command any time gameDlcAppIds changes.");
    return 0;
}

// ── Load private key ─────────────────────────────────────────────────────────

const string sealUsage =
    "  pub-utils --gen-keypair\n" +
    "  pub-utils --key-file <private_key_file> [achievements.json]\n" +
    "  pub-utils --key-env <ENV_VAR> [achievements.json]\n" +
    "  pub-utils --key <private_key> [achievements.json]";

var (privateKeyBase64, fileArgOffset) = TryResolvePrivateKeyFromArgs(args, startIndex: 0, sealUsage);
if (privateKeyBase64 is null) return 1;

// ── Load achievements.json ───────────────────────────────────────────────────

string path = args.Length > fileArgOffset
    ? args[fileArgOffset]
    : Path.Combine(Directory.GetCurrentDirectory(), "achievements.json");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    Console.Error.WriteLine("Usage: pub-utils --key-file <file> [path/to/achievements.json]");
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
    Console.WriteLine($"\nROM {romHash[..Math.Min(RomHashDisplayLength, romHash.Length)]}...  ({config.Achievements.Count} achievement(s))");

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
