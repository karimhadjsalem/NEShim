---
layout: default
title: Achievements
nav_order: 3
description: "Memory-watch triggers, achievements.json format, BCD encoding, ECDSA-P256 signature verification, and the seal-achievements tool."
---

# Achievement system

NEShim supports Steam achievements without requiring recompilation or ROM modification. Achievements are defined in `achievements.json` alongside the executable. On each emulated frame, NEShim reads one or more NES memory addresses and fires the Steam achievement when a configured condition is met.

---

## How it works

1. At startup, `EmulatorHost` computes the SHA1 hash of the raw ROM bytes.
2. `AchievementConfigLoader` reads `achievements.json` and looks up the entry for that ROM hash.
3. Each `AchievementDef` in the entry is signature-verified with ECDSA-P256. Any definition with a missing or invalid signature is silently dropped.
4. `AchievementManager` is constructed with the verified definitions and a reference to the NES memory domain.
5. Once per frame (after `RunFrame()` completes), `AchievementManager.Tick()` reads each watched address and evaluates the trigger condition.
6. When a condition matches and `StatsReady` is true (Steam's initial stats snapshot has been received), `SteamManager.UnlockAchievement()` is called. The achievement fires at most once per session — a `HashSet` tracks which ones have already fired.

**The signature check prevents casual text-file editing** from unlocking achievements. A player who edits `achievements.json` directly will invalidate the signature and the modified entry will never fire. See [Signing and sealing](#signing-and-sealing).

---

## `achievements.json` format

The file is a JSON object keyed by ROM SHA1 hash. Each value is a config block with a memory domain and a list of achievement definitions.

```json
{
  "A1B2C3D4E5F60718293A4B5C6D7E8F90A1B2C3D4": {
    "memoryDomain": "System Bus",
    "achievements": [
      {
        "steamId":    "ACH_FIRST_WIN",
        "address":    255,
        "bytes":      1,
        "encoding":   "binary",
        "comparison": "equals",
        "value":      1,
        "sig":        "base64-ecdsa-p256-written-by-seal-achievements"
      }
    ]
  }
}
```

---

## Field reference

### Config block

| Field | Type | Default | Description |
|---|---|---|---|
| `memoryDomain` | string | `"System Bus"` | Which NES memory domain to read from. `"System Bus"` = full 64 KB NES address space (recommended). `"RAM"` = the 2 KB internal RAM only (addresses 0x0000–0x07FF). |

### Achievement definition (`AchievementDef`)

| Field | Type | Default | Required | Description |
|---|---|---|---|---|
| `steamId` | string | — | Yes | The Steam achievement API name, exactly as defined in the Steamworks partner dashboard (e.g. `"ACH_WIN_ONE_GAME"`). |
| `address` | integer | — | Yes | NES memory address to watch. Use decimal (e.g. `255`) or hexadecimal in source — JSON is always decimal. |
| `bytes` | integer | `1` | No | Number of bytes to read starting at `address`. Supported values: `1`, `2`, `3`, `4`. Bytes are assembled into a single integer before comparison. |
| `bigEndian` | boolean | `false` | No | When `false` (default), bytes are assembled little-endian (NES native — LSB at lowest address). When `true`, the first byte is the most significant. Required for BCD-encoded scores where the most significant digit lives at the lowest address. |
| `encoding` | string | `"binary"` | No | `"binary"` — interpret the assembled bytes as a standard integer. `"bcd"` — decode as binary-coded decimal (see below). |
| `comparison` | string | `"equals"` | No | Trigger condition: `"equals"`, `"greaterOrEqual"`, `"greaterThan"`, `"lessOrEqual"`, or `"lessThan"`. |
| `value` | integer | — | Yes | Threshold for the comparison. |
| `sig` | string | — | Yes (to fire) | ECDSA-P256 signature (64 bytes, IEEE P1363, base64-encoded) written by `seal-achievements --key <keyfile>`. Definitions without a valid signature are silently ignored at runtime. |

---

## Memory domains

The NES has a 64 KB address space (`0x0000`–`0xFFFF`). The `"System Bus"` domain exposes the full space as seen by the CPU, including mirrors. The most useful regions are:

| Address range | Contents |
|---|---|
| `0x0000`–`0x07FF` | Internal RAM (2 KB, mirrored to `0x1FFF`) |
| `0x0100`–`0x01FF` | Stack (inside internal RAM) |
| `0x2000`–`0x3FFF` | PPU registers |
| `0x6000`–`0x7FFF` | Cartridge battery RAM (save RAM, if present) |
| `0x8000`–`0xFFFF` | Cartridge ROM (PRG) |

For custom-coded games, `0x00FF` (the last byte of zero page) is a convenient unused sentinel address in most homebrew games.

For published ROMs with known addresses, use a RAM map for the specific game to find where scores, lives, and progress flags are stored.

---

## Encoding modes

### `"binary"` (default)

Bytes at `address` are assembled into a single integer, then compared directly against `value`. This is the correct choice for flags, counters, and any value that isn't packed BCD.

Example: a custom game writes `0x01` to address `0x00FF` when the player wins the first level.

```json
{
  "steamId":    "ACH_LEVEL_1",
  "address":    255,
  "bytes":      1,
  "encoding":   "binary",
  "comparison": "equals",
  "value":      1
}
```

### `"bcd"` — Binary-Coded Decimal

Many NES games store scores as BCD: each nibble holds one decimal digit. The byte `0x42` represents the decimal value `42`, not `66`. Games like Donkey Kong, Pac-Man, and many arcade ports use this format.

To use BCD mode:

1. Set `"encoding": "bcd"`.
2. Set `"bigEndian": true` if the most-significant digit is at the lowest address (which is typical — the leading digit of the score is at the leftmost byte).
3. Set `"bytes"` to the number of bytes in the score field.
4. Set `"value"` to the decimal score threshold you want to trigger at.

**Example:** Trigger `ACH_SCORE_10000` when the score reaches 10,000. The game stores a 3-byte BCD score at addresses `0x0071`–`0x0073` with `0x0071` holding the most-significant digit.

```json
{
  "steamId":    "ACH_SCORE_10000",
  "address":    113,
  "bytes":      3,
  "bigEndian":  true,
  "encoding":   "bcd",
  "comparison": "greaterOrEqual",
  "value":      10000
}
```

**How the BCD decode works:** The engine reads 3 bytes starting at address 113 (0x71), assembles them big-endian into a 24-bit raw value, then decodes each nibble as a decimal digit. Raw bytes `[0x01, 0x00, 0x00]` → raw integer `0x010000` → decoded BCD value `10000`.

---

## Finding the ROM SHA1 hash

The hash is logged to the Visual Studio debug output window at startup:

```
[Achievements] No config found for ROM <SHA1_HASH_HERE>
```

Alternatively, compute it manually:

```bash
# PowerShell
(Get-FileHash mygame.nes -Algorithm SHA1).Hash
```

Use this hash as the key in `achievements.json`.

---

## Signing and sealing

Achievement definitions must be signed before they will fire in-game. NEShim uses **ECDSA-P256** asymmetric signing: the private key lives only on the publisher's build machine and is used by `seal-achievements` to sign definitions; the public key is used at runtime to verify them. Possession of the public key cannot forge signatures.

There is no default key — achievements will not fire until a key is configured. Two paths are available:

| Path | How | Security |
|---|---|---|
| **Binary-embedded** | Set `EmbeddedPublicKeyBase64` in `AchievementSigner.cs` at build time | Highest — key cannot be overridden by editing a file |
| **Config file** | Set `achievementPublicKey` in `config.json` | Good — no rebuild needed, suitable for pre-built releases |

The binary-embedded key takes precedence over the config key when both are present.

### Running the sealer

Sealing requires the private half of your signing keypair:

```bash
# Seal achievements.json in the current directory using a key file
seal-achievements --key private_key.txt

# Seal a specific file
seal-achievements --key private_key.txt path/to/achievements.json

# Seal using a private key from an environment variable (useful in CI)
seal-achievements --key-env NESHIM_SIGNING_KEY achievements.json
```

Output:

```
ROM A1B2C3D4E5F6...  (2 achievement(s))
  [sealed] ACH_FIRST_WIN
  [sealed] ACH_SCORE_10000

Done. 2 sealed, 0 skipped → achievements.json
```

**Run the sealer any time you edit `achievements.json`.** Editing a trigger field (address, value, comparison, etc.) without re-sealing will invalidate the signature and the achievement will never fire.

### What the signature covers

The signature is computed over a `|`-delimited canonical string of all trigger fields:

```
{steamId}|{address}|{bytes}|{bigEndian}|{encoding}|{comparison}|{value}
```

The `"sig"` field itself is excluded. Changing any trigger field without re-sealing produces a mismatch.

### Key management

#### 1. Generate a keypair

```bash
seal-achievements --gen-keypair
```

Output:

```
Private key (keep secret — never commit; store in 1Password, a local file, or a CI secret):
MHcCAQEEI...

Public key (embed in AchievementSigner.DefaultPublicKeyBase64 OR set as achievementPublicKey in config.json):
MFkwEwYHKo...
```

#### 2. Store the private key securely

Never commit the private key to source control. Options:

- Save it to a local file (e.g. `private_key.txt`) outside the repository.
- Store it in a password manager (1Password, Bitwarden).
- For CI builds, store it as an encrypted secret and pass via `--key-env`.

#### 3. Provide the public key to the runtime

Two options — use whichever fits your release path:

**Binary embedding (source build):** Set `EmbeddedPublicKeyBase64` in `NEShim/NEShim.AchievementSigning/AchievementSigner.cs`, then rebuild:

```csharp
public const string? EmbeddedPublicKeyBase64 = "MFkwEwYHKo..."; // your public key
```

The key is compiled into the binary and cannot be overridden by editing any file. This takes precedence over `achievementPublicKey` in config.json.

**Config file (pre-built release, no rebuild required):** Set `achievementPublicKey` in `config.json`:

```json
{
  "achievementPublicKey": "MFkwEwYHKo..."
}
```

If neither is set, the loader logs a warning and no achievements fire.

#### 4. Re-seal after a key change

Whenever you rotate to a new keypair, re-run `seal-achievements --key <newkeyfile> achievements.json`. Signatures from the old private key will fail verification with the new public key and be silently rejected.

**A key must be configured before any achievements will fire.** There is no default key — achievements are silently disabled until either `EmbeddedPublicKeyBase64` is set at build time (source build) or `achievementPublicKey` is set in `config.json` (pre-built release).

---

## Authoring guide: custom-coded games

If you're building a game from scratch (homebrew), the simplest achievement pattern is a reserved sentinel address:

1. Choose unused addresses in zero page or overflow RAM, e.g. `0x00E0`–`0x06FF`.
2. In your game code, write `0x01` to `0x00E0` when the player reaches the first achievement, `0x02` to `0x00E0` for the second, etc.
3. In `achievements.json`, define each achievement with `"address": 224` (0x00E0), `"value": 1`, `"comparison": "equals"`, `"bytes": 1`.

This is reliable because you control exactly when the write happens. There's no ambiguity about data format or timing — the write is atomic and permanent for the session.

---

## Authoring guide: published ROMs with scores

For games you didn't write:

1. Use a NES RAM map (NESdev wiki or dedicated resources for the game) to locate the score variable.
2. Identify the address, byte width, and encoding (binary or BCD).
3. Use `"comparison": "greaterOrEqual"` with the score threshold as `"value"`.

**Important:** Using `greaterOrEqual` or `greaterThan` is recommended over `equals` for score-based triggers. If you use `equals` and the score advances past the target value in a single frame (e.g. a large bonus), the trigger will never fire because the exact value was skipped.

The `"lessOrEqual"` and `"lessThan"` comparisons are available for triggers based on values decreasing — for example, unlocking an achievement when a lives counter drops to zero, or when a timer falls below a threshold.

---

## Runtime behaviour

- **Thread safety:** `AchievementManager.Tick()` runs on the emulation thread immediately after `RunFrame()`. All Steam API calls (`GetAchievement`, `SetAchievement`, `StoreStats`) are made on the same thread. No cross-thread synchronisation is needed.
- **Steam not available:** If Steam is unavailable (`SteamManager.IsAvailable == false`), `AchievementManager` still evaluates triggers but the `_unlock` delegate is a no-op, so nothing is sent to Steam.
- **`StatsReady` guard:** Achievements are suppressed until `SteamManager.StatsReady` becomes true. This is set by the `UserStatsReceived_t` callback, which SDK 1.61+ is supposed to fire automatically on init (`RequestCurrentStats()` no longer exists). In practice the callback does not always arrive via Steamworks.NET 2025.x, so a 5-second timeout fallback forces `StatsReady` true regardless. This prevents calling `SetAchievement` before Steam is ready, which would silently fail.
- **Session dedup:** Once an achievement fires, it is added to `_firedThisSession`. Subsequent frames that still satisfy the trigger condition are ignored without any Steam API call.
- **Already unlocked:** `UnlockAchievement` calls `SteamUserStats.GetAchievement()` before setting. If the achievement is already unlocked (from a previous play session), it skips the `SetAchievement` call.
