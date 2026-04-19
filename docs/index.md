---
layout: default
title: Home
nav_order: 1
description: "NEShim — Windows shell for publishing NES games on Steam."
permalink: /
---

# NEShim Documentation

NEShim is a Windows shell that wraps the BizHawk NES emulation core and exposes Steam SDK integration, allowing NES games to be published on Steam as native Windows applications — with achievements, overlay support, Steam Input, save states, and a configurable front-end UI — without modifying the ROM.

---

## Documentation

| Page | What it covers |
|---|---|
| [Configuration reference](configuration.md) | Every field in `config.json`, with types, defaults, and examples |
| [Achievement system](achievements.md) | How to define memory-watch triggers, encode them, and seal the config |
| [Publishing guide](publishing.md) | Step-by-step checklist for packaging a game for Steam release |
| [Architecture](architecture.md) | Internals: thread model, subsystem design, patterns, how to extend |
| [Input system](input.md) | Keyboard remapping, XInput, Steam Input, hotkeys, and the VDF file |

---

## Requirements

- Windows 10 or later (x64)
- .NET 9 runtime (bundled in self-contained publish)
- Steam client — required for achievements and overlay; the emulator runs without it but Steam features are silently disabled
- **`steam_api64.dll`** — the native Steamworks SDK DLL. Use the copy bundled inside the [Steamworks.NET GitHub release zip](https://github.com/rlabrecque/Steamworks.NET/releases) — it is matched to the wrapper version. Must be placed alongside the executable. Not included in the repository (Valve SDK license). Games deployed through Steam receive it automatically via the Steam depot.
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

After publishing, copy `steam_api64.dll` (from `sdk/redistributable_bin/win64/` in the Steamworks SDK) into the output directory alongside the exe. See the [publishing guide](publishing.md#4-obtain-steam_api64dll) for details.

See the [architecture guide](architecture.md) for a detailed walkthrough of the codebase.
