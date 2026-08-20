---
layout: default
title: v3.0.0
nav_order: 69999
has_children: true
nav_exclude: false
description: "NEShim prerelease documentation — features not yet in a stable release."
---

# NEShim Documentation

NEShim is a full-featured NES emulator built on BizHawk's cycle-accurate core, with native Steam integration for commercial distribution. Publish any NES game on Steam as a native Windows or Linux application — with achievements, overlay support, Steam Input, save states, a rich multi-language UI, and a deep video and audio filter stack — without modifying the ROM.

---

## Documentation

| Page | What it covers |
|---|---|
| [Configuration reference](configuration.md) | Every field in `config.json`, with types, defaults, and examples |
| [Filters](filters.md) | Audio filters (8 processors), video filters (structural, overlay, color effects, motion effects), availability by renderer, combining examples, and shader architecture |
| [Achievement system](achievements.md) | How to define memory-watch triggers, encode them, and seal the config |
| [Publishing guide](publishing.md) | Step-by-step checklist for packaging a game for Steam release |
| [Multi-Game Mode](multi-game.md) | An alternate, additive publish path: one binary hosting N games, each bundled in a single deploy or sold as separate Steam DLC, selected through a front-end filmstrip carousel with per-game box art and descriptions |
| [Architecture](architecture.md) | Internals: thread model, subsystem design, patterns, how to extend |
| [Input system](input.md) | Keyboard remapping, XInput, Steam Input, hotkeys, the VDF file, and per-controller-brand button glyphs |
| [Localization](localization.md) | Language files, Steam language detection, CJK font fallback |
| [Steam Deck](steamdeck.md) | Automatic adjustments (menu scale, audio default), input latency fix, publishing requirements, known differences from Windows |

---

## Requirements

- **Windows x64**: Windows 10 or later; D3D11 rendering path (SDL_GPU/Vulkan fallback if D3D11 unavailable)
- **Linux x64**: SDL_GPU/Vulkan rendering path; runs natively on Ubuntu 22.04+, SteamOS, and other mainstream distros
- .NET 9 runtime (bundled in self-contained publish)
- Steam client — required for achievements and overlay; the emulator runs without it but Steam features are silently disabled
- **Steamworks native library** — must be placed alongside the executable; not included in the repository (Valve SDK license). Use the matching copy from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases): `steam_api64.dll` (Windows) or `libsteam_api.so` (Linux) **renamed to `libsteam_api64.so`** — the wrapper's native-library lookup resolves to that name; under the zip's default Linux filename, Steam init fails at startup (caught internally — the app still runs, but achievements/overlay/DLC checks are silently disabled). Games deployed through Steam receive it automatically via the Steam depot.
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

# Publish the game — Windows (self-contained, win-x64, with ReadyToRun)
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish/NEShim-win-x64

# Publish the game — Linux (cross-compiles from Windows, or run on Linux)
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r linux-x64 --self-contained true -p:PublishReadyToRun=true -o publish/NEShim-linux-x64

# Or publish all platforms at once using the publish script:
.\local-publish.ps1 1.0.0
```

After publishing, copy the Steamworks native library from the [Steamworks.NET GitHub release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe: `steam_api64.dll` for Windows, `libsteam_api.so` for Linux — **rename the Linux file to `libsteam_api64.so`** (the wrapper's native-library lookup resolves to that name; Steam init fails at startup under the zip's default name, caught internally and silently disabling achievements/overlay/DLC checks). See the [publishing guide](publishing-source.md#5-steam_api64dll) for details.

See the [architecture guide](architecture.md) for a detailed walkthrough of the codebase.
