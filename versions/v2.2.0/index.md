---
layout: default
title: v2.2.0
nav_order: 79799
has_children: true
nav_exclude: false
description: "NEShim prerelease documentation — features not yet in a stable release."
---

# NEShim Documentation

NEShim is a full-featured NES emulator built on BizHawk's cycle-accurate core, with native Steam integration for commercial distribution. Publish any NES game on Steam as a native Windows application — with achievements, overlay support, Steam Input, save states, a rich multi-language UI, and a deep video and audio filter stack — without modifying the ROM.

---

## Documentation

| Page | What it covers |
|---|---|
| [Configuration reference](configuration.md) | Every field in `config.json`, with types, defaults, and examples |
| [Filters](filters.md) | Audio filters (7 processors), video filters (structural, overlay, color effects, motion effects), availability by renderer, combining examples, and shader architecture |
| [Achievement system](achievements.md) | How to define memory-watch triggers, encode them, and seal the config |
| [Publishing guide](publishing.md) | Step-by-step checklist for packaging a game for Steam release |
| [Architecture](architecture.md) | Internals: thread model, subsystem design, patterns, how to extend |
| [Input system](input.md) | Keyboard remapping, XInput, Steam Input, hotkeys, and the VDF file |
| [Localization](localization.md) | Language files, Steam language detection, CJK font fallback |
| [Steam Deck](steamdeck.md) | Automatic adjustments (menu scale, audio default), input latency fix, publishing requirements, known differences from Windows |

---

## Requirements

- Windows 10 or later (x64)
- .NET 9 runtime (bundled in self-contained publish)
- Steam client — required for achievements and overlay; the emulator runs without it but Steam features are silently disabled
- **`steam_api64.dll`** — the native Steamworks SDK DLL. Use the copy bundled inside the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) — it is matched to the wrapper version. Must be placed alongside the executable. Not included in the repository (Valve SDK license). Games deployed through Steam receive it automatically via the Steam depot.
- A `.nes` ROM file

---

## Quick start (publishers)

Place `config.json` alongside the executable and set `romPath` at minimum:

```json
{
  "romPath": "mygame.nes",
  "windowTitle": "My Game"
}
```

Everything else has sensible defaults. See the [configuration reference](configuration.md) for the full list.

---

## Quick start (developers / contributors)

```bash
# Build the full solution
dotnet build NEShim/NEShim.sln

# Run tests
dotnet test NEShim/NEShim.Tests/NEShim.Tests.csproj

# Publish the game (self-contained, win-x64)
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r win-x64 --self-contained true -o publish/NEShim

# Publish the achievement sealer tool
dotnet publish NEShim/NEShim.SealAchievements/NEShim.SealAchievements.csproj -c Release -r win-x64 --self-contained true -o publish/SealAchievements
```

After publishing, copy `steam_api64.dll` from the [Steamworks.NET GitHub release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe — it is not included in the repository. See the [publishing guide](publishing-source.md#5-steam_api64dll) for details.

See the [architecture guide](architecture.md) for a detailed walkthrough of the codebase.
