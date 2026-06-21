---
layout: default
title: Steam Deck
parent: v2.1.0
nav_order: 8
---

# Steam Deck

NEShim runs on Steam Deck via Proton (DXVK). This page documents the changes that are applied automatically on Deck and the adjustments that improve the experience when packaging a game.

---

## Automatic adjustments

These apply whenever `SteamDeck=1` is detected in the environment (set by SteamOS automatically on Deck hardware, in both Game Mode and Desktop Mode).

### Menu scaling

All menu font sizes, row heights, and panel widths scale on Steam Deck (up from the desktop baseline of 1.0×):

- **Fonts and row heights**: 1.5× — 18pt item text, 63px row height
- **Panel widths**: 1.5× — same scale factor as fonts, so text and panel dimensions remain proportional to each other across windowed and fullscreen

The `SteamDeck` env var is the detection mechanism; it is also set in Steam Deck Desktop Mode, so menus scale there too.

### Audio default on first run

On first launch (no `config.json` present), the audio filter defaults to **`"Saturation"`** instead of `"Default"`. The Saturation filter applies tanh soft-clipping after the standard NES filter chain, adding a mild mid-level boost that compensates for the Steam Deck's small speaker frequency response. Users can change it in the Sound menu or by editing `config.json`.

This only applies on first run. If `config.json` already exists (e.g., the user has configured the game before), the stored value is used unchanged.

---

## Main menu rendering performance

On Wine/Proton, main menu navigation appeared laggy — pressing a button took noticeably longer to update the screen than in-game menu navigation. The root cause was rendering throughput, not input detection speed: the background image was rescaled with high-quality bicubic interpolation on every render frame. Under Wine's GDI+ implementation this takes tens of milliseconds, reducing the visible update rate from 60 Hz to roughly 5–10 Hz.

The fix: `MainMenuScreen` caches the pre-scaled background bitmap at the current viewport size. The cache is built once on first display and rebuilt only when the viewport changes (e.g., toggling windowed/fullscreen). Each frame does a fast 1:1 pixel-copy blit of the cached bitmap instead of a full bicubic resample. Menu navigation now updates at 60 Hz.

The menu present cycle is driven by the 16 ms Steam callback timer while the emulation loop is paused. Nav input is dispatched to the UI thread via `BeginInvoke` and marks the overlay dirty; the next timer tick presents the updated frame.

### Windowed mode and sidebars

The default windowed resolution is **1024×672** (wider than the previous 768×672). This ensures the NES frame's 8:7 pixel aspect ratio leaves horizontal letterbox space for sidebar images. At 1024×672, the NES frame is 819×672 px, leaving approximately 102 px on each side for sidebar art.

The previous 768×672 default had the same aspect ratio as the NES display (8:7), so the frame filled the full window width with no room for sidebars. Fullscreen mode is unaffected — the 1280×800 Deck screen is always wider than the NES display aspect.

---

## Publishing for Steam Deck

Use `local-publish.ps1` (or equivalent `dotnet publish` flags) rather than `dotnet build` for any Deck testing. Two flags make a significant difference:

| Flag | Effect |
|---|---|
| `--self-contained true` | Bundles the exact .NET 9 runtime built against. A framework-dependent build uses whatever Wine-mono or dotnet-wine provides, which may differ in GC and thread scheduler behavior. |
| `-p:PublishReadyToRun=true` | Pre-compiles IL to native x64 code at build time. Without this, the JIT runs on first entry to each method — each JIT step calls `VirtualAlloc`/`VirtualProtect`, which Wine intercepts. This causes frame spikes on ROM load, menu open, and achievement unlock. |

### Shader compilation

DXBC shaders (the `.cso` files embedded in the assembly) are transpiled to SPIR-V by DXVK on the first Proton launch. They are cached in Steam's shader cache immediately after, so only the very first launch sees the transpile cost. The shaders are simple enough that this takes under a second.

### Swap chain

NEShim uses `SwapEffect.FlipDiscard`, which is required for DXVK. The legacy `Discard` swap effect is emulated in DXVK via a slower blit path and should not be used.

### Row pitch

`UploadFrame` copies the NES framebuffer row-by-row using `MappedSubresource.RowPitch` rather than assuming `width × 4`. DXVK aligns texture rows for Vulkan compatibility, so the pitch may be wider than the texture width.

---

## Known differences from Windows

| Behavior | Notes |
|---|---|
| Steam overlay | Functions correctly. Steam's `GameOverlayRenderer64.dll` hooks `IDXGISwapChain::Present` and composites the overlay into the swap chain. |
| XInput | The Steam Deck controller exposes itself as both XInput and Steam Input. NEShim reads both; Steam Input takes priority for menu navigation when native actions are configured. |
| Timer precision | `WM_TIMER` is less precise under Wine/Proton. Main menu rendering uses a pre-scaled background cache so each frame completes in under 1 ms; the 16 ms Steam callback timer drives presents at 60 Hz while paused. |
| Audio latency | WASAPI shared mode is used first; WaveOut is the fallback. Both work under Wine. WASAPI typically has lower latency. |
| Performance testing | Always use the published build (`local-publish.ps1`) for framerate testing. Debug and framework-dependent builds show artificially poor framerates under Wine that are not representative of the release. |
