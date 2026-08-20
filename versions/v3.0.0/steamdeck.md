---
layout: default
title: Steam Deck
parent: v3.0.0
nav_order: 8
---

# Steam Deck

NEShim runs on Steam Deck natively on Linux (SDL_GPU/Vulkan path) or via Proton (DXVK, D3D11 path). This page documents the changes that are applied automatically on Deck and the adjustments that improve the experience when packaging a game.

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

## Suspend / resume and GPU device loss

A system sleep/wake cycle (or, less commonly, a GPU driver reset on desktop) can invalidate the underlying graphics device NEShim is rendering with. Both rendering paths recover from this automatically — the game pauses briefly, reinitialises its renderer, and resumes, rather than crashing or requiring a relaunch:

- **Native Linux (SDL_GPU/Vulkan)** — since SDL's renderer API doesn't expose a structured "device lost" error code the way DXGI does on Windows, NEShim treats several consecutive failed frame-present calls in a row as a lost device (a single failed present, e.g. from a transient resize race, is not enough to trigger a full reinitialisation).
- **Proton (D3D11 via DXVK)** — detected directly from the swap chain's `DXGI_ERROR_DEVICE_REMOVED`/`DXGI_ERROR_DEVICE_RESET` result codes, the same as the native Windows path.

This is more likely to come up on Deck than on desktop, since suspending the Deck to sleep is a routine, frequent action for most players.

---

## Main menu rendering performance

On Wine/Proton, main menu navigation appeared laggy — pressing a button took noticeably longer to update the screen than in-game menu navigation. The root cause was rendering throughput, not input detection speed: the background image was rescaled on every render frame. The cost of software-mode rescaling on each idle tick was enough to reduce the visible update rate from 60 Hz to roughly 5–10 Hz.

The fix: `MainMenuScreen` caches the pre-scaled background bitmap at the current viewport size. The cache is built once on first display and rebuilt only when the viewport changes (e.g., toggling windowed/fullscreen). Each frame does a fast 1:1 pixel-copy blit of the cached bitmap instead of a full bicubic resample. Menu navigation now updates at 60 Hz.

The menu present cycle is driven by `SteamManager.Tick()` called from `NEShimApp.OnIdle` while the emulation loop is paused. Nav input is enqueued via `MarshalToMainThread` and marks the overlay dirty; the next idle tick presents the updated frame.

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

## Known differences from Windows (Proton path)

The native Linux build has no differences from Windows by design. If running via Proton:

| Behavior | Notes |
|---|---|
| Steam overlay | Functions correctly. Steam's `GameOverlayRenderer64.dll` hooks `IDXGISwapChain::Present` and composites the overlay into the swap chain. |
| Gamepad | The Steam Deck controller is detected via SDL3 gamepad API and is fully rebindable in-game by default. Steam Input only takes over (with in-game rebinding locked) if the player has assigned a trackpad or gyro input to an action from the Steam overlay configurator — see [Native mode: trackpad and gyro only](input.md#native-mode-trackpad-and-gyro-only). Binding rows show Valve's own glyph icons for the Deck's controls via Steam Input's glyph lookup. |
| Timer precision | The SDL idle loop is less precise under Wine/Proton than on Linux. Main menu rendering uses a pre-scaled background cache so each frame completes in under 1 ms; `SteamManager.Tick()` from the idle loop drives presents at ~60 Hz while paused. |
| Audio | SDL3 audio output (`SDL.OpenAudioDeviceStream`) works correctly under Wine via PulseAudio or PipeWire. |
| Performance testing | Always use the published build (`local-publish.ps1`) for framerate testing. Debug and framework-dependent builds show artificially poor framerates under Wine that are not representative of the release. |
