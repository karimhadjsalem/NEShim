# NEShim

A full-featured NES emulator built on BizHawk's cycle-accurate core, with native Steam integration for commercial distribution. Publish any NES game on Steam as a native Windows or Linux application — with achievements, overlay support, Steam Input, save states, a rich multi-language UI, and a deep video and audio filter stack — without modifying the ROM.

NEShim is built to make classic NES games feel native on modern platforms. Instead of acting as a general-purpose emulator, NEShim provides a complete application host around the original ROM — menus, localization, Steam integration, audio/video customization, save systems, and a polished player experience — all without altering the game itself. It’s a turnkey way to ship NES titles on Steam with modern expectations and professional presentation.



## Why NEShim?

NEShim is built for one purpose: to make it easy to ship NES games on modern platforms with the features players expect and the workflow publishers need. It is not just an emulator — it is a complete, commercial‑ready application host built around BizHawk’s cycle‑accurate NES core.

For players, NEShim delivers a polished, modern experience:
- Native Steam support — achievements, overlay, Steam Input, Steam Deck compatibility  
- A rich video pipeline — CRT effects, NTSC composite, sharp pixel modes, motion effects, color grading, and presets  
- A full audio chain — eight audio filters plus a 3‑band EQ  
- In‑game menus, save states, battery RAM, and ten built‑in languages  
- Native Windows and Linux builds with no ROM patching or external frontend required

For publishers, NEShim provides a turnkey distribution platform:
- Self‑contained executables for Windows x64 and Linux x64  
- Steamworks integration (achievements, overlay, input, language detection)  
- A complete UI layer — main menu, in‑game menu, configuration screens, localization system  
- A stable configuration model (`config.json` + `user.json`) that preserves player preferences across updates  
- ECDSA‑signed achievements with dedicated sealing tools (CLI + GUI)  
- A modern rendering architecture (D3D11 on Windows, SDL_GPU/Vulkan on Linux) with a deep, extensible filter stack  
- Zero ROM modification — all features layer cleanly on top of the original game

NEShim’s mission is simple:  
**Enable classic NES games to ship on Steam as polished, modern, native applications — with professional features, accurate emulation, and no changes to the original ROM.**

---

### Full Documentation
https://karimhadjsalem.github.io/NEShim/

---

## Features

- **Steam achievements** — memory-watch triggers configurable per ROM hash; no recompilation required to add or change achievements for different games
- **Steam overlay & input** — overlay pause, Steam Controller support via Steam Input action sets
- **Save states** — 8 named slots plus automatic on-exit save; slot selection via hotkeys or in-game menu
- **Battery RAM persistence** — save RAM written to disk on exit and restored on load
- **Configurable front end** — main menu with custom background image, sidebar art, and looping MP3 music
- **Audio** — volume control; eight audio filters (Default NES chain, Warm, Pseudo Stereo, Warm Stereo, Compression, Bass Boost, Saturation, Pop Filter); and a **3-band EQ** (Bass/Mid/Treble, ±12 dB per band) that stacks after the active filter
- **Graphics** — platform-adaptive rendering: D3D11 on Windows (with SDL_GPU/Vulkan fallback), SDL_GPU/Vulkan on Linux. D3D11 and SDL_GPU both support seven structural filters (Pixel Perfect, Smooth, CRT Scanlines, CRT Phosphor, CRT Screen, NTSC Composite, Sharp Pixel), six color effects, four motion effects (CRT Jitter, Scanline Bob, Magnetic Distortion, Screen Glow), a **Video Overlay** second-filter slot, **picture adjustments** (brightness, contrast, saturation, hue), and four built-in **Video Presets** — all independently stackable; see [Filters](#filters) below
- **Input** — keyboard remapping and SDL3 gamepad support (XInput, DualShock, Switch Pro, Steam Deck controller) with configurable dead zone; auto-pause on controller disconnect
- **Localization** — in-game Language screen lets users pick a language at any time; each language is listed in its own native script with a flag icon. Auto mode resolves language from Steam first, then falls back to the OS UI culture (`CultureInfo.CurrentUICulture`), then English. An explicit selection overrides Steam for subsequent launches. Ten built-in languages (English, Français, Deutsch, Español, Español (Latinoamérica), 日本語, 한국어, Русский, 中文（简体）, Português); add custom languages by dropping a `lang/<code>.json` file alongside the exe
- **Steam Deck / Linux** — runs natively on Linux x64 (SDL_GPU/Vulkan path) and on Steam Deck natively or via Proton with no configuration changes required
- **Window title** — set per-game via `config.json`; no rebuild needed

---

## Requirements

**End users / players:** none — published builds are self-contained and prepackaged. Runs natively on Windows 10+ (x64) and Linux x64.

**Publishers:** see the [publishing guide](https://karimhadjsalem.github.io/NEShim/) on the project site. For the Windows build you will also need `steam_api64.dll` from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) — place it alongside the executable. For the Linux build, `libsteam_api.so` from the same release zip is required. Neither file is included in the repository (Valve SDK license); do not commit them to source control.

**Developers (building from source):** .NET 9 SDK. Building for Windows additionally requires Windows 10+ (x64). Filter development (adding or modifying HLSL shaders) requires `fxc.exe` from the Windows 10 SDK for DXBC shaders (D3D11/Windows) or `dxc.exe` (from the `Microsoft.Direct3D.DXC` NuGet package) for SPIR-V shaders (SDL_GPU/Linux). Standard builds use pre-compiled `.cso`/`.spv` files checked into source control and do not need either tool. See [Building from source](#building-from-source) below.

---

## Getting started (publishers)

NEShim uses two configuration files. **`config.json`**, placed alongside the executable, is the publisher configuration layer — set your game-specific settings here. At minimum, point it at your ROM:

```json
{
  "romPath": "mygame.nes",
  "windowTitle": "My Game"
}
```

Everything else — save paths, audio settings, input mappings, menu artwork — has sensible defaults and can be left as-is or tuned as needed. Audio/video/input defaults you set in `config.json` become the player's starting preferences; on first launch they are copied to **`user.json`** in `%APPDATA%\<WindowTitle>\`, where all subsequent in-game menu changes are stored. Steam updates that overwrite `config.json` never affect `user.json`, so player preferences are preserved across your releases automatically.

**Before shipping a release**, work through the [publishing checklist](CLAUDE.md#publishing-checklist):
- Set `WindowTitle` in `config.json`
- Set the exe icon via `<ApplicationIcon>` in the csproj
- Generate a signing keypair with `seal-achievements --gen-keypair` and configure the public key
- Seal your `achievements.json` with `seal-achievements --key-file private_key.txt achievements.json`

Full configuration reference and a step-by-step publishing guide are on the project site.

---

## Filters

### Audio filters

Eight audio processors are available via **Settings → Sound → Audio Filter**: Default (standard NES hardware chain), Warm, Pseudo Stereo, Warm Stereo, Compression, Bass Boost, Saturation, and Pop Filter (DMC click reduction). Switching takes effect immediately with no audio pop.

A **3-band EQ** is available via **Settings → Sound → EQ**: Bass (100 Hz), Mid (1 kHz), and Treble (8 kHz), each adjustable from −12 dB to +12 dB. The EQ runs after the active audio filter and is bypassed when all bands are at 0.

### Video filters

NEShim uses two rendering paths. The D3D11 renderer is used by default on all modern Windows systems; the SDL_GPU renderer (Vulkan or D3D11 via SDL) is the Linux-native path and the Windows fallback when D3D11 is unavailable. The active path is detected at startup and logged.

Both paths support all structural filters — DXBC shaders on D3D11, SPIR-V shaders on SDL_GPU:

| Filter | Available |
|---|:---:|
| Pixel Perfect (8:7 PAR, point-sampled) | D3D11 and SDL_GPU |
| Smooth (Jinc2 windowed-sinc reconstruction) | D3D11 and SDL_GPU |
| CRT Scanlines | D3D11 and SDL_GPU |
| CRT Phosphor (scanlines + aperture-grille mask) | D3D11 and SDL_GPU |
| CRT Screen (barrel distortion + chromatic aberration + vignette) | D3D11 and SDL_GPU |
| NTSC Composite | D3D11 and SDL_GPU |
| Sharp Pixel (xBRZ edge-preserving upscaler) | D3D11 and SDL_GPU |

Both paths support **Color Effects** that stack on top of any structural filter:

| Color Effect | Description |
|---|---|
| None | No transform (default) |
| Warm | Slight amber tint with reduced blues |
| Cool | Blue-green tint approximating the D93 9300K CRT white point |
| Greyscale | Full desaturation using BT.601 luma coefficients |
| NES Colors | Color-correction matrix for more accurate 2C02 → sRGB output |
| Phosphor Amber | Greyscale converted to the warm orange-yellow of a monochrome amber phosphor display |
| Phosphor Green | Greyscale converted to the bright green of P1 phosphor used in arcade and early CRT monitors |

If `config.json` specifies a filter not supported by the active renderer, NEShim logs a warning, falls back to Pixel Perfect, and saves the fallback to `user.json`.

### Video Overlay

A second structural filter pass applied on top of the primary structural filter, available on both D3D11 and SDL_GPU. When active, the primary filter renders to an intermediate render target at letterbox pixel dimensions, then the overlay filter reads from that intermediate and renders the final composited frame. Color grading is deferred to the second pass so it is applied only once to the combined image.

Overlay-eligible filters: **CRT Scanlines**, **CRT Phosphor**, **CRT Screen**. Any primary filter can be paired with any eligible overlay filter — for example, Smooth (Jinc2 reconstruction) as the base with CRT Scanlines as the overlay, or Pixel Perfect with CRT Screen for barrel distortion around sharp pixels. The menu prevents selecting the same filter in both slots; switching the primary filter to one that matches the current overlay automatically resets the overlay to None. With no overlay selected (default `"None"`), rendering is identical to the single-pass path with no overhead.

**Overscan mode** is available in both renderers and controls how the 256×240 NES frame is cropped and scaled:

| Mode | Behaviour |
|---|---|
| Overscan | Crops 8 rows from the top and bottom (224 visible rows), matching the NTSC TV overscan region the NES was designed for |
| Normal | Shows all 240 rows |
| Underscan | Shows all 240 rows but renders at 88% of the window size, centred, with a uniform black border |

Filter and overscan changes take effect immediately while the game is running — no restart needed.

### Video Presets

Four built-in presets apply a coordinated combination of filter settings in one step via **Settings → Video → Presets**. Presets, along with Video Overlay, Motion Effect, and Picture, are available on both rendering paths; the in-game Video menu hides these entries only in the rare case where neither D3D11 nor the SDL_GPU shader path is active (SDL's plain-renderer fallback, no GPU device found), but the settings are always accessible via the main menu and `config.json`.

| Preset | Video Filter | Video Overlay | Color Effect | Motion Effect |
|---|---|---|---|---|
| Living Room | CRT Screen | CRT Scanlines | NES Colors | CRT Jitter |
| Arcade Monitor | CRT Phosphor | — | Cool | CRT Jitter |
| Sharp | Sharp Pixel | — | NES Colors | — |
| Phosphor | CRT Screen | CRT Phosphor | Phosphor Amber | Screen Glow |

Selecting any individual filter after applying a preset clears the preset name back to None. The active preset name appears inline on the Video settings screen.

**Motion Effects** animate the NES viewport each frame. CPU quad-offset effects (CRT Jitter, Scanline Bob) apply a per-frame clip-space displacement with no extra render pass. Shader-backed effects (Magnetic Distortion, Screen Glow) add a dedicated pixel shader pass:

| Motion Effect | Platforms | Description |
|---|---|---|
| None | D3D11 and SDL_GPU | No animation |
| CRT Jitter | D3D11 and SDL_GPU | Micro-pixel translation simulating hold instability on an aging CRT |
| Scanline Bob | D3D11 and SDL_GPU | 30 Hz vertical oscillation mimicking interlaced scanline wobble |
| Magnetic Distortion | D3D11 and SDL_GPU | Per-pixel sine-wave UV warp simulating a magnetic field deflecting the CRT electron beam unevenly |
| Screen Glow | D3D11 and SDL_GPU | Temporal frame accumulation (65% per-frame retention) simulating CRT phosphor persistence. D3D11 uses a 2-sampler pixel shader; SDL_GPU (which can only bind one sampler per draw) reproduces the same `max(current, previous × decay)` result via GPU blend compositing instead |

Motion effects compose with all structural filters, the Video Overlay slot, and color effects.

### Picture Adjustments (D3D11 and SDL_GPU/Vulkan)

Four independent sliders available under **Settings → Video → Picture**: **Brightness** (−100 to +100), **Contrast** (−100 to +100), **Saturation** (−100 to +100), and **Hue** (−100 to +100, mapping to −π..+π radians rotation around the grey axis). Applied as a post-process pass after all structural, overlay, and motion effect passes. All four default to 0 (neutral); when all are neutral the pass is skipped entirely with no rendering overhead.

### Developer note — injectable filter architecture

Every structural filter ships as two compiled shader variants: **DXBC** (`.cso`) for D3D11 and **SPIR-V** (`.spv`) for SDL_GPU, both embedded as assembly resources and compiled from the same HLSL source. The D3D11 side uses `ID3D11Filter` + `D3D11FilterFactory`; the SDL side uses `ISdlFilter` + `SdlFilterFactory` + `SdlGpuRenderState` (one SDL_GPUShader + SDL_GPURenderState pair per active filter, created on filter change). Both interfaces share the same 4-float uniform layout: structural params at `[0..2]`, color mode at `[3]`. A shared `ColorGrade.hlsli` include applies the active Color Effect in every shader, so any filter + color effect combination works without shader permutations.

Motion effects implement `IMotionEffect`. CPU quad-offset effects (CRT Jitter, Scanline Bob) need only `GetFrameOffset` — no extra pass, no shader. Shader-backed effects (Magnetic Distortion) additionally implement `ISdlMotionEffect` on the SDL side and return a non-null `PixelShaderResourceName` on the D3D11 side, causing the renderer to allocate an intermediate render target and run a dedicated warp pass. Screen Glow (PhosphorPersistence) is a special case: D3D11 renders it as a genuine 2-sampler shader pass, but `SDL_GPURenderState` can only bind one texture per draw, so on SDL_GPU the renderer instead detects `IMotionEffect.NeedsTemporalBuffer` and reproduces the same accumulation formula with two single-sampler draws composited via a custom `SDL_ComposeCustomBlendMode` (Maximum op) — no SPIR-V shader involved. See the [Architecture guide](https://karimhadjsalem.github.io/NEShim/) for step-by-step instructions on adding new filters and motion effects to both paths.

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

`seal-achievements` is published alongside each release as standalone binaries for Windows and Linux.

---

## Building from source

```bash
# Restore, build, test
dotnet restore NEShim/NEShim.sln
dotnet build   NEShim/NEShim.sln
dotnet test    NEShim/NEShim.Tests/NEShim.Tests.csproj

# Publish the game — Windows
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish/NEShim-win-x64

# Publish the game — Linux (cross-compiles from Windows or runs on Linux)
dotnet publish NEShim/NEShim/NEShim.csproj -c Release -r linux-x64 --self-contained true -p:PublishReadyToRun=true -o publish/NEShim-linux-x64

# Or publish all platforms at once (game + sealer CLI + sealer UI) using the publish script:
.\local-publish.ps1 1.0.0
```

**After publishing**, copy the matching Steamworks SDK native library from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe: `steam_api64.dll` for Windows, `libsteam_api.so` for Linux. Use the copy bundled with the wrapper — it is matched to the wrapper version. Do not commit these files to source control.

Releases are built and published automatically on version tags (`v*.*.*`) via GitHub Actions, producing platform-named archives for both `win-x64` and `linux-x64`.

---

## Project structure

| Project | Purpose |
|---|---|
| `NEShim` | Main application — SDL3 windowing + rendering, Steam wiring, game loop (Windows x64 + Linux x64) |
| `NEShim.AchievementSigning` | Shared library — achievement types and ECDSA-P256 signing logic |
| `NEShim.SealAchievements` | Developer CLI tool — stamps ECDSA-P256 signatures onto `achievements.json` (Windows + Linux) |
| `NEShim.SealAchievementsUI` | Developer GUI tool — Windows Forms UI for the sealer (Windows only) |
| `NEShim.Tests` | NUnit test suite |
| `BizHawk` | NES emulation core, adapted from the BizHawk multi-system emulator |

---

## License

Licensed under the **Apache License 2.0**. See [LICENSE](LICENSE).

This project incorporates components from several open-source projects. Attribution and license notices for all compiled dependencies are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Use of the Steam features requires acceptance of the [Valve Steamworks SDK License Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement).
