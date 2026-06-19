---
layout: default
title: Steam Deck
parent: Pre-release
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
- **Panel widths**: viewport-proportional — panels occupy the same fraction of the screen in both windowed and fullscreen modes, maintaining the correct visual proportion on the 1280×800 fullscreen display

The viewport-proportional width scaling means a 360px panel on a 768px windowed viewport and a 600px panel on a 1280px fullscreen viewport each fill approximately 47% of the screen. Without this, the same 360px panel on a 1280px screen would appear disproportionately small.

The `SteamDeck` env var is the detection mechanism; it is also set in Steam Deck Desktop Mode, so menus scale there too.

### Audio default on first run

On first launch (no `config.json` present), the audio filter defaults to **`"Saturation"`** instead of `"Default"`. The Saturation filter applies tanh soft-clipping after the standard NES filter chain, adding a mild mid-level boost that compensates for the Steam Deck's small speaker frequency response. Users can change it in the Sound menu or by editing `config.json`.

This only applies on first run. If `config.json` already exists (e.g., the user has configured the game before), the stored value is used unchanged.

---

## Input latency

On Wine/Proton, `WM_TIMER` fires 20–30 ms late due to Gamescope compositor scheduling. NEShim uses two mitigations:

1. **Immediate present after navigation**: After each gamepad nav event is dispatched to the UI thread, the renderer presents a new frame immediately rather than waiting for the next timer tick. This eliminates the "wait for timer" portion of the latency.
2. **4 ms pause-loop poll interval**: The emulation thread's menu poll cycle runs at 4 ms intervals (250 Hz) while paused, cutting the average input-to-detection delay from ~8 ms to ~2 ms.

Combined, these reduce the typical button-press-to-screen-update latency to 3–5 ms. No configuration change is needed — both fixes are applied unconditionally.

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
| Timer precision | `WM_TIMER` is less precise under Wine/Proton. Menu rendering uses an immediate-present path after input events and a 4 ms pause-loop poll interval to keep latency low. |
| Audio latency | WASAPI shared mode is used first; WaveOut is the fallback. Both work under Wine. WASAPI typically has lower latency. |
| Performance testing | Always use the published build (`local-publish.ps1`) for framerate testing. Debug and framework-dependent builds show artificially poor framerates under Wine that are not representative of the release. |
