# NEShim

A Windows shell that wraps the BizHawk NES emulation core and exposes Steam SDK integration, allowing NES games to be published on Steam as native Windows applications — with achievements, overlay support, Steam Input, save states, and a configurable front-end UI — without modifying the ROM.

---

## Features

- **Steam achievements** — memory-watch triggers configurable per ROM hash; no recompilation required to add or change achievements for different games
- **Steam overlay & input** — overlay pause, Steam Controller support via Steam Input action sets
- **Save states** — 8 named slots plus automatic on-exit save; slot selection via hotkeys or in-game menu
- **Battery RAM persistence** — save RAM written to disk on exit and restored on load
- **Configurable front end** — main menu with custom background image, sidebar art, and looping MP3 music
- **Audio** — volume control and optional sound scrubber for warmer playback on modern hardware
- **Graphics** — nearest-neighbour (pixel-perfect) and bilinear (smoothed) scaling modes
- **Input** — keyboard remapping and XInput gamepad support with configurable dead zone
- **Window title** — set per-game via `config.json`; no rebuild needed

---

## Requirements

- Windows 10 or later (x64)
- .NET 9 runtime
- Steam client (required for achievement and overlay features; the emulator runs without it but Steam features are silently disabled)
- **`steam_api64.dll`** — the native Steamworks SDK DLL, found in `sdk/redistributable_bin/win64/` of the Steamworks SDK download. Must be placed alongside the executable. This file is **not** included in the repository (Valve SDK license); obtain it from the [Steamworks partner dashboard](https://partner.steamgames.com/). Games deployed through Steam receive it automatically via the Steam depot.
- A `.nes` ROM file

---

## Getting started (publishers)

NEShim is configured entirely through `config.json` placed alongside the executable. At minimum, point it at your ROM:

```json
{
  "romPath": "mygame.nes",
  "windowTitle": "My Game"
}
```

Everything else — save paths, audio settings, input mappings, menu artwork — has sensible defaults and can be left as-is or tuned as needed.

**Before shipping a release**, work through the [publishing checklist](CLAUDE.md#publishing-checklist):
- Set `WindowTitle` in `config.json`
- Set the exe icon via `<ApplicationIcon>` in the csproj
- Seal your `achievements.json` with the `SealAchievements` tool

Full configuration reference and a step-by-step publishing guide are on the project site.

---

## Achievement system

Achievements are defined in `achievements.json`, keyed by the SHA1 hash of the ROM. Each definition specifies a memory address to watch, the number of bytes to read, how to interpret them, and a comparison condition. When the condition is met post-frame, the Steam achievement is unlocked.

```json
{
  "ROM_SHA1_HASH": {
    "memoryDomain": "System Bus",
    "achievements": [
      {
        "steamId": "ACH_FIRST_WIN",
        "address": 255,
        "bytes": 1,
        "encoding": "binary",
        "comparison": "equals",
        "value": 1,
        "sig": "..."
      }
    ]
  }
}
```

Each definition must be signed with the `SealAchievements` tool before shipping. Unsigned or tampered entries are silently ignored at runtime.

`SealAchievements` is published alongside each release as a standalone Windows binary.

---

## Building from source

```bash
# Restore, build, test
dotnet restore NEShim/NEShim.sln
dotnet build   NEShim/NEShim.sln
dotnet test    NEShim/NEShim.Tests/NEShim.Tests.csproj

# Publish the game (self-contained, win-x64)
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r win-x64 --self-contained true -o publish/NEShim

# Publish the achievement sealer tool
dotnet publish NEShim/NEShim.SealAchievements/NEShim.SealAchievements.csproj -c Release -r win-x64 --self-contained true -o publish/SealAchievements
```

**After publishing**, copy `steam_api64.dll` (from `sdk/redistributable_bin/win64/` in the Steamworks SDK) into the output directory alongside the exe. This file is not included in the repository and must be obtained from the [Steamworks partner dashboard](https://partner.steamgames.com/). Do not commit it to source control.

Releases are built and published automatically on version tags (`v*.*.*`) via GitHub Actions.

---

## Project structure

| Project | Purpose |
|---|---|
| `NEShim` | Main application — Windows Forms shell, Steam wiring, game loop |
| `NEShim.AchievementSigning` | Shared library — achievement types and HMAC signing logic |
| `NEShim.SealAchievements` | Developer tool — stamps HMAC signatures onto `achievements.json` |
| `NEShim.Tests` | NUnit test suite |
| `BizHawk` | NES emulation core, adapted from the BizHawk multi-system emulator |

---

## License

Licensed under the **Apache License 2.0**. See [LICENSE](LICENSE).

This project incorporates components from several open-source projects. Attribution and license notices for all compiled dependencies are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Use of the Steam features requires acceptance of the [Valve Steamworks SDK License Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement).
