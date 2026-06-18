# NEShim

A Windows shell that wraps the BizHawk NES emulation core and exposes Steam SDK integration, allowing NES games to be published on Steam as native Windows applications — with achievements, overlay support, Steam Input, save states, and a configurable front-end UI — without modifying the ROM.


### Full Documentation
https://karimhadjsalem.github.io/NEShim/

---

## Features

- **Steam achievements** — memory-watch triggers configurable per ROM hash; no recompilation required to add or change achievements for different games
- **Steam overlay & input** — overlay pause, Steam Controller support via Steam Input action sets
- **Save states** — 8 named slots plus automatic on-exit save; slot selection via hotkeys or in-game menu
- **Battery RAM persistence** — save RAM written to disk on exit and restored on load
- **Configurable front end** — main menu with custom background image, sidebar art, and looping MP3 music
- **Audio** — volume control and seven audio filters (Default NES chain, Warm, Pseudo Stereo, Warm Stereo, Compression, Bass Boost, Saturation)
- **Graphics** — dual rendering paths: D3D11 (primary) and GDI+ (fallback). D3D11 adds CRT Scanlines and NTSC Composite structural filters plus four color effects (None, Warm, Greyscale, NES Colors) that stack independently on top of any structural filter; see [Filters](#filters) below
- **Input** — keyboard remapping and XInput gamepad support with configurable dead zone; auto-pause on controller disconnect
- **Localization** — UI language loaded from Steam at startup; nine built-in languages (English, French, German, Spanish, Japanese, Korean, Russian, Simplified Chinese, Brazilian Portuguese); add custom languages by dropping a `lang/<code>.json` file alongside the exe
- **Steam Deck** — runs on Steam Deck via Proton with no configuration changes required
- **Window title** — set per-game via `config.json`; no rebuild needed

---

## Requirements

- Windows 10 or later (x64)
- .NET 9 runtime
- Steam client (required for achievement and overlay features; the emulator runs without it but Steam features are silently disabled)
- **`steam_api64.dll`** — the native Steamworks SDK DLL. Use the copy bundled inside the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) — it is matched to the wrapper version. Must be placed alongside the executable. This file is **not** included in the repository (Valve SDK license); do not commit it to source control. Games deployed through Steam receive it automatically via the Steam depot.
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
- Generate a signing keypair with `seal-achievements --gen-keypair` and configure the public key
- Seal your `achievements.json` with `seal-achievements --key-file private_key.txt achievements.json`

Full configuration reference and a step-by-step publishing guide are on the project site.

---

## Filters

### Audio filters

Seven audio processors are available via **Settings → Sound → Audio Filter**: Default (standard NES hardware chain), Warm, Pseudo Stereo, Warm Stereo, Compression, Bass Boost, and Saturation. Switching takes effect immediately with no audio pop.

### Video filters

NEShim uses two rendering paths. The D3D11 renderer is used by default on all modern Windows systems; the GDI+ renderer is a complete fallback for hardware or driver configurations where D3D11 is unavailable. The active path is detected at startup and logged; it can be forced to GDI+ for debugging via `"forceRenderer": "gdi"` in `config.json`.

Each rendering path exposes its own set of structural filters:

| Filter | GDI+ | D3D11 |
|---|:---:|:---:|
| Pixel Perfect (8:7 PAR, point-sampled) | Yes | Yes |
| Smooth (bilinear interpolation) | Yes | — |
| CRT Scanlines | — | D3D11 only |
| NTSC Composite | — | D3D11 only |

D3D11 mode also supports **Color Effects** that stack on top of any structural filter:

| Color Effect | Description |
|---|---|
| None | No transform (default) |
| Warm | Slight amber tint with reduced blues |
| Greyscale | Full desaturation using BT.601 luma coefficients |
| NES Colors | Color-correction matrix for more accurate 2C02 → sRGB output |

If `config.json` specifies a filter not supported by the active renderer, NEShim logs a warning, falls back to Pixel Perfect, and saves the fallback to `config.json`.

**Overscan mode** is available in both renderers and controls how the 256×240 NES frame is cropped and scaled:

| Mode | Behaviour |
|---|---|
| Overscan | Crops 8 rows from the top and bottom (224 visible rows), matching the NTSC TV overscan region the NES was designed for |
| Normal | Shows all 240 rows |
| Underscan | Shows all 240 rows but renders at 88% of the window size, centred, with a uniform black border |

Filter and overscan changes take effect immediately while the game is running — no restart needed.

### Developer note — injectable filter architecture

Structural filters implement `ID3D11Filter` (in `NEShim.Rendering.Filters`) and are compiled as DXBC pixel shaders. All shaders share a uniform 4-float constant buffer: structural params at `[0..2]` (filled by the filter), color mode at `[3]` (filled by the renderer). A shared `ColorGrade.hlsli` include applies the active Color Effect as the final step in every shader, so any structural filter + color effect combination works without shader permutations. Adding a new structural filter requires implementing `ID3D11Filter`, writing the `.ps.hlsl`, registering in `D3D11FilterFactory`, and adding to `VideoFilterModeParser.D3D11Supported` — no renderer or menu changes needed. Adding a new color effect only requires extending the enum and adding a branch in `ColorGrade.hlsli`.

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

Each definition must be signed with `seal-achievements` before shipping. A private key is required to sign; the matching public key is embedded in the binary or set in `config.json`. Unsigned or tampered entries are silently ignored at runtime.

`seal-achievements` is published alongside each release as a standalone Windows binary.

---

## Building from source

```bash
# Restore, build, test
dotnet restore NEShim/NEShim.sln
dotnet build   NEShim/NEShim.sln
dotnet test    NEShim/NEShim.Tests/NEShim.Tests.csproj

# Publish the game (self-contained, win-x64)
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish/NEShim

# Publish the achievement sealer tool
dotnet publish NEShim/NEShim.SealAchievements/NEShim.SealAchievements.csproj -c Release -r win-x64 --self-contained true -o publish/SealAchievements
```

**After publishing**, copy `steam_api64.dll` from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe. Use the copy bundled with the wrapper — it is matched to the wrapper version. Do not commit it to source control.

Releases are built and published automatically on version tags (`v*.*.*`) via GitHub Actions.

---

## Project structure

| Project | Purpose |
|---|---|
| `NEShim` | Main application — Windows Forms shell, Steam wiring, game loop |
| `NEShim.AchievementSigning` | Shared library — achievement types and ECDSA-P256 signing logic |
| `NEShim.SealAchievements` | Developer tool — stamps ECDSA-P256 signatures onto `achievements.json` |
| `NEShim.Tests` | NUnit test suite |
| `BizHawk` | NES emulation core, adapted from the BizHawk multi-system emulator |

---

## License

Licensed under the **Apache License 2.0**. See [LICENSE](LICENSE).

This project incorporates components from several open-source projects. Attribution and license notices for all compiled dependencies are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Use of the Steam features requires acceptance of the [Valve Steamworks SDK License Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement).
