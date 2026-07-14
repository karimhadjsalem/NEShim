# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

NEShim is a full-featured NES emulator built on BizHawk's cycle-accurate core, with native Steam integration for commercial distribution on Steam. It started as a thin integration layer and has grown into a complete emulator host: a platform-adaptive rendering pipeline (D3D11/DXBC on Windows; SDL_GPU/SPIR-V/Vulkan on Linux) with a multi-axis video filter stack (structural filters, two-pass overlay, color effects, motion effects, picture adjustments, video presets), a 7-processor SDL3 audio chain, full in-game and main-menu UI with keyboard/gamepad navigation, 10-language localization, ECDSA-signed achievements, and save states — all delivered as cross-platform executables (Windows x64 and Linux x64) that publish any NES ROM to Steam without modifying the ROM. The BizHawk emulation code lives in `BizHawk/` and was adapted from the BizHawk multi-system emulator — it is the authoritative NES emulation layer and generally should not be modified unless fixing compatibility issues. The BizHawk code is treated as a frozen vendored dependency with no proactive upstream sync cadence — the NES core is decades-stable. Sync only when a specific emulation accuracy bug or confirmed security issue warrants it; cherry-pick the specific fix, do not bulk-merge.

## Build & Run Commands

```bash
# Build the full solution
dotnet build NEShim.sln

# Build release
dotnet build NEShim.sln -c Release

# Run tests
dotnet test NEShim.Tests/NEShim.Tests.csproj

# Run a single test
dotnet test NEShim.Tests/NEShim.Tests.csproj --filter "TestName"
```

The main application targets `net9.0` and runs natively on Windows x64 and Linux x64 (SDL3 windowing; D3D11 renderer on Windows, SDL_GPU/Vulkan on Linux). The BizHawk library targets `net8.0`.

### Testing on Proton / Steam Deck — use the publish script, not `dotnet build`

**Do not use `dotnet build` output for performance testing on Proton, the native Linux binary, or Steam Deck.** The difference in emulation performance between a local build and a published build is significant and has caused real confusion (local builds appeared to have framerate problems that the release did not).

Two build flags in `local-publish.ps1` are responsible:

- **`--self-contained true`** — bundles the exact .NET 9 runtime the app was built against. A framework-dependent `dotnet build` output relies on whatever wine-mono or dotnet-wine provides, which may be a different version with different GC and thread scheduler behavior.
- **`-p:PublishReadyToRun=true`** — pre-compiles managed IL to native x64 code at build time, eliminating JIT work at runtime. On Proton/Wine, JIT is expensive because every JIT code-generation step calls `VirtualAlloc`/`VirtualProtect`, which Wine must intercept and translate. Without ReadyToRun, these calls happen on first entry to each method, causing frame spikes whenever a new code path is hit (ROM load, menu open, achievement unlock, etc.).

When testing on Proton or Steam Deck, always run `local-publish.ps1` and copy that output to the device. A raw `dotnet build` output is only valid for iterating on logic and running tests on Windows.

## Project Structure

```
NEShim.sln
NEShim/                    — SDL3 application (entry point: Program.cs → SDL3WindowHost + NEShimApp)
NEShim.Tests/              — NUnit test project
NEShim.AchievementSigning/ — ECDSA-P256 signing library (AchievementSigner, AchievementDef)
NEShim.SealAchievements/   — CLI tool for stamping achievement signatures (seal-achievements)
NEShim.SealAchievementsUI/ — Windows Forms GUI for the sealer (Windows only; net9.0-windows)
BizHawk/                   — NES emulation core (adapted from BizHawk emulator)
```

## Architecture

### NEShim Application Layer (`NEShim/`)
An SDL3-based application. `NEShimApp.cs` orchestrates startup, wires all components together, and manages the application lifecycle. `SDL3WindowHost` creates and manages the SDL3 window, runs the SDL event loop, and provides a `MarshalToMainThread` delegate for cross-thread work (equivalent to WinForms `BeginInvoke`). `Program.cs` is the entry point: it calls `PlatformDetector.BeginHighResolutionTiming()`, creates `SDL3WindowHost`, and runs `new NEShimApp(sdlHost).Run()`. The intent is for this layer to own SDK integrations (Steamworks, etc.) while delegating all emulation to BizHawk.

Key subsystems and their responsibilities:

| Namespace | Responsibility |
|---|---|
| `NEShim.Config` | Two-file config system: `AppConfig` (runtime POCO — all fields), `UserConfig` (nullable user-settable fields; `ApplyTo(AppConfig)` overlays non-null values; `FromConfig(AppConfig)` extracts user fields for writing; deprecated fields readable but never written), `ConfigLoader` — `Load()` merges publisher `config.json` (install dir) with user `user.json` (`%APPDATA%\<WindowTitle>\`); `Save(AppConfig)` writes user-settable fields only to `user.json`; on first run, `user.json` is bootstrapped from the current `config.json` values before returning (protects player preferences from future depot overwrites); `config.json` is never modified at runtime; `InputBinding` — keyboard + gamepad mapping POCO |
| `NEShim.Emulation` | BizHawk bridge (`EmulatorHost`), controller adapter, stubs |
| `NEShim.GameLoop` | `EmulationThread` — timing, hotkeys, pause logic |
| `NEShim.Rendering` | `IFrameRenderer` strategy (Windows: `D3D11Renderer` primary / `SDL3HwRenderer` fallback; Linux: `SDL3HwRenderer`), `IMenuSceneProvider` pull interface, `SDL3PaintContext` (cross-platform paint surface — wraps an SDL_Surface for menu/HUD rendering), `SdlSurfaceLoader` (cross-platform image loader using SDL3_image), `FrameBuffer` (double-buffer), `IOverlayRenderer` (platform overlay strategy — `SteamOverlayRenderer` on Windows, `NullOverlayRenderer` on Linux), `D3DOverlayHook` (Windows: D3D11 device + swap chain bound to the SDL HWND); D3D11 subsystems: `Filters/` (structural DXBC pixel shaders + two-pass overlay via intermediate RT), `MotionEffects/` (`IMotionEffect` strategy — CPU quad-offset effects implement `GetFrameOffset`; shader-backed effects additionally provide `PixelShaderResourceName` and `WriteShaderParams`), `VideoColorFilterMode`, `OverscanMode`, `VideoPreset`, `VideoPresetRegistry` |
| `NEShim.Audio` | SDL3 audio stream bridge (`AudioPlayer` — `SDL.OpenAudioDeviceStream` + callback); `IAudioProcessor` strategy + 8 concrete processors; `AudioEqProcessor` — 3-band biquad peaking EQ (bass@100 Hz, mid@1 kHz, treble@8 kHz, Q=0.9, ±12 dB) applied after the processor when any gain is non-zero |
| `NEShim.Input` | `InputManager` (keyboard + SDL3 gamepad + Steam Input), `InputSnapshot`; `SDL3GamepadDevice`, `SDL3GamepadSource`, `SDL3GamepadMapper`; `SteamInputSource`, `SteamInputMapper`; `KeyboardInputSource`, `KeyboardMapper` |
| `NEShim.Platform` | `PlatformDetector` — Wine/Proton detection (`IsWine`), Steam Deck detection (`IsSteamDeck`), D3D11 active flag (`IsD3D11Active`), `ConfigureVideoDriverForSteamOverlay()` (Linux: sets `SDL_VIDEODRIVER=x11` before `SDL_Init` to force XWayland so the Steam overlay hook can intercept `vkQueuePresentKHR`), `BeginHighResolutionTiming`/`EndHighResolutionTiming`; `SDL3WindowHost` — SDL window lifecycle, event loop, marshal queue; `MenuScale` — font/layout scale factor (1.0× normal, 1.5× Steam Deck) |
| `NEShim.Saves` | `SaveStateManager` (8 slots + auto), `SaveRamManager` |
| `NEShim.Achievements` | `AchievementConfigLoader` — parses and signature-verifies `achievements.json`; `AchievementManager` — per-frame memory-watch evaluation and Steam unlock |
| `NEShim.Localization` | `LocalizationData` — POCO with all UI strings and font family; `LocalizationLoader` — loads `lang/<language>.json` with English fallback; `LanguageRegistry` — static list of 10 `LanguageInfo` records with `FindByCode`/`FindBySteamCode`/`FindByCulture`; `ILanguageResolver` strategy interface + `SteamLanguageResolver`, `CultureInfoLanguageResolver`, `ChainedLanguageResolver` implementations; `FlagImageLoader` — embedded PNG flag resources |
| `NEShim.UI` | `InGameMenu`, `MainMenuScreen` state machines + stateless renderers; `IMenuInputTarget` (gamepad dispatch interface implemented by `NEShimApp`) |
| `NEShim.Steam` | `SteamManager` — init, overlay callbacks, UI-thread tick; `SteamInputManager` — action sets |

### BizHawk Emulation Core (`BizHawk/`)

The core is a faithful port of BizHawk's NES subsystem. Key layers:

**Emulation framework** (`BizHawk/Emulation/Common/`): Interfaces that all emulator cores implement — `IEmulator`, `IStatable`, `ISaveRam`, `IDebuggable`, `IInputPollable`, `IRegionable`, `ISettable<,>`, plus `IEmulatorServiceProvider` for runtime service lookup.

**NES core** (`BizHawk/Emulation/Cores/Consoles/NIntendo/NES/`):
- `NES.cs` — Main orchestrator, implements all the emulation interfaces above
- `NES.Core.cs` — Frame execution loop
- `PPU.cs` — Picture Processing Unit (scanline/sprite rendering)
- `APU.cs` — Audio Processing Unit (Pulse ×2, Triangle, Noise, DMC channels)
- `Boards/` — 150+ cartridge mapper implementations, all extending `NesBoardBase`

**CPU** (`BizHawk/Emulation/Cores/CPUs/MOS 6502/`): Generic `MOS6502X<TLink>` — cycle-accurate 6502 emulator with a disassembler.

**Common utilities** (`BizHawk/Common/`): Checksums (CRC32/MD5/SHA1), bit-manipulation helpers, platform abstractions (Win32/POSIX), and collection/buffer extensions.

### Mapper System
Each NES cartridge type maps to a `NesBoardBase` subclass in `Boards/`. The board handles PRG/CHR bank switching and any on-cartridge hardware. When adding game support, the relevant board is usually the first place to look for emulation bugs.

### Design patterns in use

**State machine** — `InGameMenu` and `MainMenuScreen` each own a `Screen` enum and dispatch to a per-screen `ScreenHandler` (nested private class). Each handler encapsulates one screen's title, items, enabled-state logic, and activation logic. Adding a new screen requires: add an enum value, add a handler class, add one entry to `BuildHandlers()`. Rendering is always delegated to a paired stateless `*Renderer` class that takes the state object as a read-only parameter. Never put rendering logic inside a state machine, and never put state mutation inside a renderer. `ScreenHandler` has a virtual `GetItemIcon(int index) => null`; only `LanguageHandler` overrides it to return flag `Bitmap`s from `FlagImageLoader`.

**Video preset state** — `AppConfig.VideoPreset` (string: `"None"` | `"LivingRoom"` | `"Arcade"` | `"Sharp"` | `"Phosphor"`) tracks the last-applied preset. `ApplyPreset(VideoPreset)` on each menu object sets all 8 filter fields atomically (`VideoFilter`, `VideoFilterOverlay`, `VideoColorFilter`, `VideoMotionEffect`, `OverscanMode`, `VideoBrightness`, `VideoContrast`, `VideoSaturation`, `VideoHue`), writes the preset name into config, fires all callbacks, and calls `_onConfigSaved()`. `ClearPreset()` resets the name to `"None"` and is called from every handler that changes an individual setting: `VideoFilterHandler.Activate`, `VideoMotionEffectHandler.Activate`, `VideoPictureHandler.Activate` (color), `VideoHandler.CycleOverlay`, and `VideoHandler.Activate` (overscan), plus `AdjustPicture` and `ResetPicture`. The active preset name is displayed inline on the Video screen via `ActivePresetDisplayName()`. `VideoPresetsHandler` is the `ScreenHandler` for `Screen.VideoPresets`; it lists items from `VideoPresetRegistry.All` (indices 1–4) plus "None" at index 0, each with a checkmark when active. Adding a new preset requires only adding to `VideoPresetRegistry.All` and the `PresetName`/`ActivePresetDisplayName` switch blocks — no new handler class needed.

**Strategy + Chain of Responsibility (language resolution)** — `ILanguageResolver` is the strategy. `ChainedLanguageResolver` (Chain of Responsibility) tries each in order: `SteamLanguageResolver` → `CultureInfoLanguageResolver`. `NEShimApp.ResolveLanguage()` checks `config.Language` first; if it's "Auto", it delegates to the chain. An explicit language in config overrides Steam. Both resolvers log every decision via `Logger.Log` for debugging. `SteamLanguageResolver` normalizes the raw Steam API code through `LanguageRegistry.FindBySteamCode` before returning — this handles Steam's quirks (`koreana` → `korean`, `brazilian` → `portuguese`) and returns null for unsupported Steam languages (e.g. `tchinese`) so the chain falls through to `CultureInfoLanguageResolver`. `CultureInfoLanguageResolver` uses `FindByCulture`, which does a two-pass lookup: exact `culture.Name` first, then `TwoLetterISOLanguageName`. Simplified Chinese uses explicit culture-name prefixes (`zh-CN`, `zh-SG`, `zh-Hans`, etc.) rather than the bare `zh` two-letter code, because `zh-Hant`/`zh-TW` (Traditional Chinese) share the same two-letter code and must not match.

**Live language switching** — `InGameMenu.UpdateLocalization(LocalizationData)` and `MainMenuScreen.UpdateLocalization(LocalizationData)` rebuild binding arrays and handlers in-place when the user changes language in the Language screen. `NEShimApp.OnLanguageChanged` saves config, reloads localization, calls `UpdateLocalization` on both menus, and marks the overlay dirty.

**Observer (events)** — Components communicate upward via C# events (`NewGameChosen`, `ResumeChosen`, `Opened`, `Closed`). Wiring is done in `NEShimApp.InitializeEmulator()`, keeping components decoupled.

**Double-buffer** — `FrameBuffer` keeps a back buffer (emulation thread writes) and a front buffer (paint thread reads). `Swap()` atomically exchanges them under a `SpinLock`. Never read from the back buffer on the paint thread or write to the front buffer on the emulation thread.

**Pause flags** — `EmulationThread.PauseReasons` is a `[Flags]` enum. Any non-zero value blocks the loop on a `ManualResetEventSlim`. Always use `SetPauseReason(reason, active)` rather than setting bits directly — it handles the CAS loop and the audio mute side-effect.

**Stateless renderer** — `MainMenuRenderer` and `MenuRenderer` are `internal static` classes with a single `Draw(SDL3PaintContext, SDL.Rect, <StateType>)` entry point. They are pure drawing functions with no side-effects. Do not cache fonts or surfaces across calls in these classes; use `SDL3FontCache` for font lifecycle management.

**Pull scene interface (`IMenuSceneProvider`)** — The active frame renderer does not know which UI scene (logo, main menu, in-game menu) is active. Instead it calls `IMenuSceneProvider.GetActiveScenePainter()` each frame. `NEShimApp` implements this: it returns a paint delegate (`Action<SDL3PaintContext, SDL.Rect>`) for whichever scene is current, or `null` during gameplay (zero overhead on the hot path). The delegate is invoked inside the overlay render pass before the FPS/toast/achievement overlays, so the scene is always painted under the HUD. Never add scene-detection logic to the renderer itself.

**Menu input interface (`IMenuInputTarget`)** — `EmulationThread` dispatches gamepad navigation without knowing about `NEShimApp` directly. It calls `IMenuInputTarget.IsWaitingForGamepadButton`, `HandleGamepadNav`, and `HandleGamepadButtonPress`. `NEShimApp` implements this interface and routes calls to whichever state machine is active. All implementations are explicit (e.g., `void UI.IMenuInputTarget.HandleGamepadNav(...)`) to avoid CS0051 accessibility issues caused by the internal `MenuNavInput` type appearing in a method signature.

### Rendering architecture: Windows vs Linux

**Windows (D3D11):** `RendererFactory.Windows.cs` creates a `D3D11Renderer` using a `D3DOverlayHook` device and swap chain bound to the HWND obtained from `SDL3WindowHost.Handle`. If D3D11 initialisation fails, it falls back to `SDL3HwRenderer`. `PlatformDetector.IsD3D11Active` is set once at startup to reflect which path was taken.

**Linux (SDL_GPU):** `RendererFactory.cs` creates an SDL_GPU renderer backed by Vulkan. `NullOverlayRenderer` is used (Steam's overlay is injected via `LD_PRELOAD` on Linux; no swap chain hook is needed). `PlatformDetector.IsD3D11Active` is `false` on Linux; D3D11-specific features are not offered in menus.

The rest of this section describes the D3D11 Windows path in detail.

### D3D11 rendering and Steam overlay architecture (Windows)

`D3DOverlayHook` creates a D3D11 device and swap chain (using `SwapEffect.FlipDiscard` — required for DXVK on Proton) bound to the HWND from `SDL3WindowHost.Handle`. `RendererFactory.Windows.cs` tries to construct a `D3D11Renderer`; if the hook device or swap chain is null, or the constructor throws, it falls back to `SDL3HwRenderer`. `PlatformDetector.IsD3D11Active` is set once at startup to reflect which path was taken.

**D3D11 is the primary path for all rendering** — not just NES frames. The logo splash, main menu, in-game menu, and all HUD overlays (FPS, toasts, achievements) are all composed by `D3D11Renderer`. When D3D11 is unavailable, `SDL3HwRenderer` takes over all rendering duties using SDL_GPU.

**Frame delivery:** The emulation thread batches `UploadFrame` and `Tick` together in a single marshal call to the main thread (`MarshalToMainThread`), so Present fires immediately after the texture is ready — tightly coupled to emulation timing with no clock drift. The `OnIdle` callback in the SDL event loop calls `SteamManager.Tick()` and `Renderer.Tick` when the emulation loop is paused, keeping the Steam overlay hook alive.

**Overlay texture pipeline:** `D3D11Renderer` maintains a separate BGRA overlay texture the same size as the swap chain. `DrawOverlay()` checks whether any scene (via `IMenuSceneProvider.GetActiveScenePainter()`) or transient HUD element (FPS, toast, achievement) is active. If so, it calls `RenderOverlayBitmap()` — which paints content into a CPU-side `SDL3PaintContext` (scene first, HUD on top), uploads it, and alpha-blends the overlay quad over the NES frame. `MarkOverlayDirty()` forces an overlay repaint on the next `DrawAndPresent` tick (called from `NEShimApp` whenever menu state changes). The scene painter is always re-invoked each frame while a scene is active, since cursor movement does not call `MarkOverlayDirty`.

**Pixel format:** BizHawk's `IVideoProvider` returns `int[]` where each int is `0xAARRGGBB`. In little-endian memory the bytes are `[B, G, R, A]` — BGRA — which maps directly to `B8G8R8A8_UNorm` with no byte-swapping. Row copy in `UploadFrame` always uses `MappedSubresource.RowPitch` (DXVK may align rows wider than `width × 4`).

**`SDL3HwRenderer` fallback:** When D3D11 initialisation fails, `SDL3HwRenderer` takes over all rendering using SDL_GPU. Structural filters (SPIR-V), color effects, picture adjustments, all four motion effects (CRT Jitter, Scanline Bob, Magnetic Distortion, PhosphorPersistence), and Video Overlay (second-pass filter slot) are all supported. `DrawNesFrame` runs a cascading-target pipeline of up to four optional stages (structural filter → overlay → motion-effect shader/phosphor → picture adjust), mirroring `D3D11Renderer`'s render pass model — see the doc comment on `SDL3HwRenderer` for the exact stage-ordering and colour-grade-deferral rules. PhosphorPersistence (Screen Glow) has no SPIR-V shader on this path: `SDL_GPURenderState` only binds one texture sampler per draw (no public API exists to bind a second, unlike D3D11's `PSSetShaderResource` slot 1), so the D3D11 shader's `max(current, previous * decay)` formula is instead reproduced with two single-sampler draws into a ping-pong accumulation texture using `SDL_ComposeCustomBlendMode`'s `Maximum` operation plus `SetTextureColorModFloat` for the decay scale — GPU fixed-function blending, no CPU pixel work.

**No mouse input:** Mouse navigation has been removed from all menus. `SDL.HideCursor()` is called once at startup and the cursor is never shown again. All menu navigation is keyboard or gamepad only.

**`EmulationThread` decoupling:** `EmulationThread` takes an `Action<Action> _marshalToMainThread` (the SDL marshal queue delegate from `SDL3WindowHost`) and an `IMenuInputTarget _menuInput` (for gamepad dispatch — `NEShimApp`). `NEShimApp` implements `IMenuInputTarget` explicitly to avoid CS0051.

**Device loss recovery:** `D3D11Renderer.DrawAndPresent()` fires `DeviceLost` on `DXGI_ERROR_DEVICE_REMOVED/RESET`. `NEShimApp.OnD3DDeviceLost` handles it by setting `PauseReasons.DeviceLost`, disposing both renderer and overlay renderer, recreating them (calling `_renderer.SetMenuSceneProvider(this)` again on the new renderer), then clearing the pause reason.

`D3DOverlayHook` must be initialised **after** `SetWindowMode` so the swap chain dimensions match the final window size. `D3D11Renderer` is constructed immediately after. An `SDL3WindowHost.Resized` event calls `D3D11Renderer.Resize()` which calls `ResizeBuffers` and recreates the RTV.

**`PlatformDetector.IsD3D11Active`** is set once at startup after `D3D11Renderer` is (or isn't) constructed; `PlatformDetector.IsSdlGpuRendererActive` is set analogously by `RendererFactory` whenever `SDL3HwRenderer` initialises with `SDL_CreateGPURenderer` (SPIR-V support) rather than the plain `SDL_CreateRenderer` fallback. The in-game Video menu gates its extended 9-item set (Presets, Overlay, Motion Effect, Picture, in addition to the base 5) on `PlatformDetector.SupportsAdvancedVideoFeatures` (`IsD3D11Active || IsSdlGpuRendererActive`) rather than `IsD3D11Active` alone, since `SDL3HwRenderer` now supports the full feature set on the SDL_GPU path too. Only the plain `SDL_CreateRenderer` fallback (no GPU device — extremely rare, would require both D3D11 init failure and `SDL_CreateGPURenderer` failure) shows the reduced 5-item menu (Window, Filter, Overscan, FPS, Back).

**Render pass model:** `DrawAndPresent` selects among one to four passes depending on which effects are active.

- **Single pass** (no overlay, no shader motion effect): structural filter renders directly to the backbuffer via `DrawNesQuad`.
- **Two passes** (overlay active, no shader motion effect): `DrawStructuralFilterToTarget(_overlayRt)` → structural filter to `_overlayRt` (letterbox-sized intermediate, `colorMode=0`); then overlay filter reads from `_overlayRt` and renders to the backbuffer applying the real `colorMode`.
- **Two passes** (no overlay, shader motion effect active): `DrawStructuralFilterToTarget(_motionEffectRt)` → structural filter to `_motionEffectRt` (letterbox-sized intermediate, `colorMode=0`); then `DrawMotionEffectToBackbuffer()` → ME pixel shader reads from `_motionEffectRt` and renders to the backbuffer.
- **Three passes** (both overlay and shader motion effect active): structural → `_overlayRt`; overlay → `_motionEffectRt` (`colorMode=0`); ME shader → backbuffer.
- **+1 picture adjust pass** (any of `VideoBrightness`, `VideoContrast`, `VideoSaturation`, or `VideoHue` is non-zero): the preceding passes write to `_pictureAdjustRt` (viewport-sized intermediate) instead of the swap chain backbuffer; `DrawPictureAdjust()` then reads from `_pictureAdjustRt` and writes to the backbuffer. This pass uses its own independent `b0` (`{brightness, contrast, saturation, hue}`) written directly by the renderer — it does not go through `WriteBaseParams`. When all four values are at their config defaults (0) the intermediate RT is not allocated and `DrawPictureAdjust` is skipped entirely.

`IntermediateRenderTarget` (private nested class in `D3D11Renderer`) groups the `ID3D11Texture2D`, `ID3D11RenderTargetView`, `ID3D11ShaderResourceView`, and pixel dimensions into one object. `Sync(device, width, height)` creates or disposes the RT to match the requested dimensions, with a size-unchanged early exit. `SyncOverlayRt()`, `SyncMotionEffectRt()`, and `SyncPictureAdjustRt()` are thin wrappers that pass the correct condition-guarded dimensions (zero when the feature is inactive) and log on creation. `SyncOverlayRt()` and `SyncMotionEffectRt()` are called from `Resize`; `SyncMotionEffectRt` is also called from `UpdateLetterboxRect` so the ME intermediate tracks letterbox changes triggered by filter PAR differences; `SyncPictureAdjustRt()` is called from `Resize` and whenever picture settings change. The overlay slot accepts only `CrtScanlines`, `CrtPhosphor`, and `CrtScreen` — filters composable on an already-scaled frame. The menu prevents selecting the same filter for both primary and overlay via `IsItemEnabled`. Selecting a new primary filter that matches the current overlay also immediately clears the overlay to `None`.

**Cbuffer layout is fixed.** `b0` is always exactly 4 floats: `{param0, param1, param2, colorMode}`. Structural filters may use slots [0..2] via `WriteBaseParams()`; the renderer owns slot [3] (color mode) and always writes it. No filter may use a second constant buffer — if a future filter genuinely needs more than 3 configuration floats, revisit this rule explicitly rather than working around it silently. Exception: `PictureAdjust.ps` runs as a completely separate pass with its own dedicated `b0` (`{brightness, contrast, saturation, hue}`); the renderer writes those floats directly and the structural filter cbuffer rule does not apply to it.

**Adding a new video filter (structural):** touch seven places across both renderer paths — (1) add an enum value to `VideoFilterMode` and update `D3D11Supported`; (2) write a `.ps.hlsl` in `Shaders/Dx11/` (must `#include "ColorGrade.hlsli"` and call `ApplyColorGrade`; 4-float `cbuffer FilterParams`); (3) implement `ID3D11Filter` in `Filters/Dx11/`; (4) add a case to `D3D11FilterFactory.Create`; (5) compile the same HLSL to SPIR-V (`.ps.spv` in `Shaders/Vulkan/`); (6) implement `ISdlFilter` in `Filters/SDL/` (provide `PixelShaderResourceName`, `WriteUniformData`, `NumFragmentSamplers`, `NumFragmentUniformBuffers`); (7) add a case to `SdlFilterFactory.Create`. Register both `.cso` and `.spv` in `NEShim.csproj` as `<EmbeddedResource>`. Add `OverlaySupported` entry only if the filter can run as D3D11 second-pass overlay. Return `null` from `PixelShaderResourceName` (both interfaces) to reuse the passthrough shader.

**Adding a new motion effect:** implement `IMotionEffect` in a new class under `MotionEffects/`. For a **CPU quad-offset effect**: implement `GetFrameOffset` and `NotifyLayout` only — the renderer uses the returned `(dx, dy)` as a clip-space offset on the NES quad with no extra pass. Register in both `MotionEffectFactory.Create` (D3D11) and `SdlMotionEffectFactory.Create` (SDL) — same implementation works on both paths. For a **shader-backed effect**: additionally return a non-null `PixelShaderResourceName` (D3D11) and implement `ISdlMotionEffect` (for SDL, adds `SpvResourceName`, `NumFragmentSamplers`, `NumFragmentUniformBuffers`); the renderer allocates an intermediate RT and a dedicated shader pass when this is set. Add both `.hlsl` + `.cso` (D3D11) and `.spv` (SDL) and register in `NEShim.csproj` following the same pattern as `MagneticDistortion`. Add `Parse()`/`DisplayName()` cases in `VideoMotionEffectModeParser`. Note: temporal effects requiring two simultaneous texture samplers (ping-pong buffer) cannot be expressed via `SDL_GPURenderState`; return `NoneMotionEffect` (with a log entry) from `SdlMotionEffectFactory.Create` for such effects.

**Slow-frame timing log:** When `enableLogging` is true, `EmulationThread` logs any frame whose total work time exceeds 14 ms (2.67 ms below the 16.67 ms budget). Each log entry breaks down time into `input`, `runFrame`, `video`, and `audio` segments. This is the first place to look when investigating FPS regressions. Note: in a Debug build, `runFrame` typically takes 17–30 ms due to unoptimised BizHawk JIT output — this is expected and is not a bug. Always use a Release build for performance testing.

**Proton/DXVK notes:**
- `SwapEffect.FlipDiscard` is required — legacy `Discard` is emulated via a slower blit path in DXVK.
- `UploadFrame` copies row-by-row using `RowPitch` — DXVK aligns texture rows for Vulkan compatibility.
- DXBC passthrough shaders compile to SPIR-V on first Proton launch (cached in Steam shader cache); near-instant due to trivial shader complexity.
- Use `local-publish.ps1`, not raw `dotnet build`, for Proton performance testing.

`SteamAPI.RestartAppIfNecessary(appId)` is called in `Program.Main` before `new SDL3WindowHost(...)`. It reads the App ID from `steam_appid.txt`. If the game was not launched via Steam, the call returns `true` and the process exits so Steam can relaunch it with the overlay DLL already injected (Windows: `GameOverlayRenderer64.dll`; Linux: `steamoverlayvulkanlayer.so` via `LD_PRELOAD`).

#### Steamworks.NET version pinning

Do **not** upgrade Steamworks.NET via NuGet — the NuGet package tops at 2024.8.0 (SDK 1.60), which is incompatible with SDK 1.63+. Use the GitHub releases zip instead:

- **Steamworks.NET**: 2025.163.0 — local DLL at `NEShim/lib/Steamworks.NET.dll`
- **steam_api64.dll**: **not stored in the repository** (Valve SDK license). At packaging time, copy it from the Steamworks.NET GitHub release zip into the output directory alongside the exe — it is matched to the wrapper version. Do not source it separately from the Steamworks SDK partner dashboard, and do not commit it to source control.
- Reference in csproj: `<Reference Include="Steamworks.NET"><HintPath>lib\Steamworks.NET.dll</HintPath></Reference>`

SDK 1.61+ has the Steam client sync stats before the game process launches, so stats are already in the local cache when `SteamAPI_Init` returns. `SteamUserStats.RequestCurrentStats()` still exists in Steamworks.NET 2025.x but is marked obsolete and always returns `true` without doing anything — do not call it.

### Coding conventions (NEShim layer only)

- **Naming**: `PascalCase` for types, methods, properties, and events; `_camelCase` for private fields; `camelCase` for locals and parameters.
- **Nullability**: Enable nullable reference types (`<Nullable>enable</Nullable>`). Use `?` annotations throughout. Avoid `!` (null-forgiving) except at genuine interop boundaries.
- **Method length**: Keep methods under ~30 lines. Extract named helpers rather than adding comments that describe blocks.
- **One responsibility per class**: State machines hold state and drive transitions; renderers draw; managers own lifecycle and I/O. Do not mix these.
- **No magic numbers**: Give all frame dimensions, timing constants, and UI sizes a named `const` or `static readonly` in the class that owns them.
- **Dispose discipline**: Every `IDisposable` created inside a method must be in a `using` declaration or `using` block. Classes that own SDL surfaces, audio, or host resources must implement `IDisposable` and be disposed in `NEShimApp.Shutdown()`.
- **Thread safety**: Emulation thread and main thread share `_pauseReasonBits` (volatile `int` + CAS) and `FrameBuffer` (SpinLock). All other mutable state is owned by one thread. Use `MarshalToMainThread` (the SDL3 marshal queue delegate from `SDL3WindowHost`) to marshal work to the main thread from the emulation thread — never call SDL functions directly from the emulation thread.
- **One class per file**: each top-level class, interface, enum, or record gets its own `.cs` file named after the type. Acceptable exceptions are small private helper types that are tightly coupled to a single containing class (e.g., a `ScreenHandler` subclass nested inside a state machine) — these may stay in the same file as their owner. Do not add a second public or internal top-level type to an existing file.
- **No `#if` preprocessor directives**: treat `#if` / `#elif` / `#else` as a last resort. Prefer MSBuild `<Compile Remove>` with `Condition` attributes on `<ItemGroup>` to exclude platform-specific files entirely, and `.Platform.cs` / `.Windows.cs` sibling files that define the same class for each target. `#if` is acceptable only where file-level exclusion is genuinely impractical (e.g., a single expression inside a file that is otherwise identical across platforms). Never use `#if` for feature flags or debug-only paths — use runtime checks (`OperatingSystem.IsWindows()`, config values) instead.
- **Don't modify BizHawk source** unless fixing a direct compatibility issue (emulation accuracy bug or confirmed security fix). Prefer adapter/wrapper classes in `NEShim/Emulation/` to bridge BizHawk interfaces. See the upstream sync policy in "What This Project Is" above.

## Testing

### Libraries and tooling
- **NUnit** — test framework. Use `[TestFixture]`, `[Test]`, `[SetUp]`, `[TearDown]`. Prefer `Assert.That(actual, Is.EqualTo(expected))` constraint syntax over classic assertions.
- **NSubstitute** — mocking library. Add via NuGet (`NSubstitute`). Do not introduce a second mocking library.
- Keep attributes lightweight: avoid `[Category]`, `[Description]`, `[Author]`, and other decorative metadata unless a specific CI filtering need requires them.

### Structure — mirror the SUT
Each class under test gets exactly one test class, in a file that mirrors the source path:

```
NEShim/UI/InGameMenu.cs                          →  NEShim.Tests/UI/InGameMenuTests.cs
NEShim/Achievements/AchievementManager.cs        →  NEShim.Tests/Achievements/AchievementManagerTests.cs
NEShim/Saves/SaveStateManager.cs                 →  NEShim.Tests/Integration/SaveStateManagerTests.cs
```

The last example illustrates that classes with I/O dependencies land in `Integration/` rather than mirroring the source folder directly.

If a source class is not worth testing in isolation (e.g., a pure data record, a stateless renderer), no test class is required.

### Unit test rules
- **No boundary crossing.** A unit test must not touch the file system, audio devices, the Windows registry, network, or any external process. Anything that does is an integration test, not a unit test.
- **Mock dependencies at the boundary.** Use `Substitute.For<T>()` for interfaces and abstract classes that would otherwise pull in I/O or heavy subsystems. Pass substitutes through the constructor (prefer constructor injection over property injection). Verify only interactions that matter to the behaviour under test — do not assert on every call.
- **Do not over-mock.** Concrete collaborators with no I/O side-effects (plain data objects, pure value computations) should be used directly, not mocked. A test that mocks everything except the SUT is testing nothing.
- **Avoid test globals.** Shared `static` state and class-level fields shared across tests make failures hard to diagnose. Initialise the SUT and its substitutes in `[SetUp]` so each test gets a fresh instance. The only acceptable class-level fields are `readonly` constants or substitute / SUT fields initialised in `[SetUp]`.
- **One behaviour per test.** Each `[Test]` method asserts one logical outcome. Name tests in the form `MethodName_Condition_ExpectedOutcome`.

### Boundary-crossing (integration) tests
Tests that must cross a boundary — file system, real audio device, BizHawk core execution — are allowed only when the behaviour cannot be verified any other way. They must live in a separate location:

- **Same project, separate folder** if the test count is small: `NEShim.Tests/Integration/`
- **Separate project** (`NEShim.IntegrationTests/`) if the suite grows or requires different setup (e.g., a real ROM file, elevated permissions).

Never place a boundary-crossing test alongside unit tests. CI should be able to run unit tests alone (`--filter "TestCategory!=Integration"`) without external dependencies.

### Key BizHawk Dependencies
- `CommunityToolkit.HighPerformance` — SIMD/span performance helpers
- `Newtonsoft.Json` — settings serialization

## Multi-Game Mode

NEShim's default publish path is single-game: one `config.json` and one ROM next to the exe, matching everything described above. **Multi-game mode is a second, additive publish path on the same binary** — one exe hosting N games, selected at runtime through a carousel screen. It never changes single-game behavior; it only activates under an explicit, opt-in signal. Steam DLC is entirely optional per game, not a requirement of multi-game mode itself — see `SteamDlcManager.FilterOwned`/`IsOwned` below: a game with `SteamDlcAppId = 0` (the default) always shows, so an entire library can ship bundled in a single deploy with zero DLC depots, or any mix of bundled and paid-DLC games in the same install.

**Detection**: `MultiGameMode.IsActive` (`NEShim/Config/MultiGameMode.cs`) is `true` when `games/multigame.json` exists next to the exe — a dedicated manifest file shipped once with the base multi-game publish, not by scanning for per-game content. This matters concretely: on a fresh install where Steam hasn't finished downloading any game DLC, `games/` may not even exist yet, so detection must not depend on DLC content being present, only on whether this is fundamentally a multi-game build. `games/multigame.json` is also reused as the *shell config* — an ordinary `AppConfig` (loaded via `ConfigLoader.LoadFrom`) that drives window mode, language, and `NoLogo` for the carousel period before any game is chosen.

**Per-game content layout**: every game lives in its own `games/<gameId>/` subfolder (its own ROM, `config.json`, `achievements.json`, artwork), directly under the same install directory as the base exe. `steam_appid.txt` and `game_actions_<appid>.vdf` stay singular/app-wide; the whole multi-game install is still one Steam base App ID and one running process. If a game's own `config.json` sets `steamDlcAppId`, that folder is additionally distributed as a **Steam DLC depot** — Steam mounts it into the install directory only once the depot is owned/downloaded (the standard/simplest Steam content-DLC pattern) — and ownership is checked at scan time with `SteamApps.BIsDlcInstalled` (`SteamDlcManager.IsOwned`/`FilterOwned`, `NEShim/Steam/SteamDlcManager.cs`). A game with `SteamDlcAppId = 0` (the default when unset), or when there's no live Steam session (`SteamManager.IsAvailable == false`), is shown unconditionally — this is both how local dev/test game folders work without configuring real Steamworks entitlements, and the supported way to ship an entire library bundled in one deploy with no DLC depots at all (every game's folder just ships directly in the base install/depot). A single multi-game build can freely mix bundled and paid-DLC games.

**DLC ownership anti-tamper** (`AppConfig.GameDlcAppIds`/`GameDlcAppIdsSignature`, `NEShim.Achievements.DlcMapSigner`, `GameScanner.Scan`): each game's own `config.json` — including its `steamDlcAppId` — lives inside that game's own DLC depot/folder, which is as player-editable as any other file on disk; trusting it blindly would let a copied/leaked DLC folder simply have `steamDlcAppId` edited down to `0` to bypass the ownership check entirely (`0` always means "show unconditionally"). Note that `games/multigame.json` itself is **not** meaningfully more protected — Steam's file-integrity verification is a manual, player-triggered check, not a continuous runtime guarantee, so it's realistically just as locally-editable as any per-game `config.json`. `GameScanner.Scan` supports three tiers, escalating in strength:

- **No trust data** — each game's own claimed `steamDlcAppId` is trusted outright. Correct for a single-deploy build with no DLC-gated games.
- **Unsigned `gameDlcAppIds` map** (`games/multigame.json`'s `gameDlcAppIds` field, a `gameId → expected steamDlcAppId` map authored by the publisher) — a *soft* cross-check: a `gameId` present in the map must match, but a `gameId` absent from it is still trusted as declared by its own `config.json`. Closes the "edit one field" bypass but not one that also edits/removes the corresponding map entry — not tamper-proof.
- **Signed map** (`DlcMapSigner.EmbeddedPublicKeyBase64`, a compile-time constant in `NEShim.AchievementSigning/DlcMapSigner.cs`, mirroring `AchievementSigner.EmbeddedPublicKeyBase64`) — real, cryptographic tamper-evidence via ECDSA-P256. **Deliberately never config-driven**: a config-file-hosted public key would let a tampered install just supply its own matching keypair alongside a forged map, defeating the signature entirely. Setting the embedded key requires editing source and rebuilding — there is no config.json equivalent, by design. Signing is opted into purely by that constant being non-null; `AppConfig.GameDlcAppIdsSignature` (the ECDSA-P256 signature itself, base64, sealed via `seal-achievements --seal-dlc-map`) is meaningless without it. Once opted in, verification **fails closed**: a missing or invalid signature marks *every* scanned game invalid, never silently falling back to a weaker tier — a corrupted signature must not be indistinguishable from "protection intentionally disabled". Once the signature verifies, the map becomes the sole, *complete* source of truth: every game must appear in it (bundled games too, with value `0`) with a matching `steamDlcAppId`, or it's rejected — this closes the loophole where an attacker downgrades both a game's own claim and the map entry in lockstep.

Any rejected entry is invalid the same way a missing ROM or corrupt config is — logged via `Logger.LogAlways`, never shown on screen (see the "Game Error" note above). None of this is a substitute for Steam's own depot-delivery boundary (a fully repacked/cracked install is a different threat entirely, outside what any client-side check can prevent) — nor is it about confidentiality: `steamDlcAppId` values are public Steam data (visible on the store page itself), so there is no reason to encrypt them, only to authenticate them.

**`GameContext`** (`NEShim/Config/GameContext.cs`) identifies one game's content root and is the mechanism that keeps single-game mode byte-identical. Every per-game path resolution method (`ConfigLoader.Load`/`Save`, `AchievementConfigLoader.Load`, `MainMenuScreen.ResolveAssetPath`) takes a trailing optional `GameContext? ctx = null` — omitting it (as every single-game call site does) reproduces exactly the exe-relative resolution these methods have always done. `GameContext.ResolvePath(path, ctx)` is the one formula: absolute paths pass through verbatim; relative paths resolve against `ctx.RootDirectory` when set, or `AppContext.BaseDirectory` when `ctx` is null. Per-game `user.json` isolation uses a separate scheme keyed by the stable `GameId` (`%APPDATA%\NEShim\Games\<gameId>\user.json`), not the mutable/dual-purpose `WindowTitle` the single-game scheme uses — the two never collide.

**Bootstrapping** (`NEShimApp.cs`): `InitializeEmulator()` is a two-way dispatcher (`InitializeEmulatorSingleGame` vs `InitializeEmulatorMultiGame`) on `MultiGameMode.IsActive`. Both the once-per-process single-game boot and every multi-game carousel selection funnel through one shared Template Method, `LoadGameContent(GameContext? ctx)` — it loads config, ROM, saves, and sidebar art, and internally calls `UnloadCurrentGame()` first (a safe no-op the first time). `LoadGame(GameContext)` is the second half: presentation — re-applies render options, restarts audio, rebuilds the main/in-game menus, and starts the emulation session. Only truly engine-level singletons (`_input`, `_gamepadDevice`, `_frameBuffer`, `_renderer`, `_overlayRenderer`) survive a game switch; `EmulationThread` is always freshly reconstructed since it captures its `AppConfig` reference once at construction.

**Carousel**: `GameCarouselScreen`/`GameCarouselRenderer` (`NEShim/UI/`) follow the same lightweight state-object + stateless-renderer split as `LogoScreen`/`LogoRenderer`, wired through the existing `IMenuSceneProvider` pull-scene chain — not the fuller `Screen` enum + `ScreenHandler` machinery `MainMenuScreen`/`InGameMenu` use, since the carousel is a single flat list with no sub-screens. It renders as a filmstrip: `GameCarouselRenderer.ComputeSlotGameIndices(selectedIndex, gameCount, visibleSlotCount)` maps carousel state to which game occupies each visible slot via modulo arithmetic, which naturally duplicates games at both edges when the library is smaller than the visible slot count (e.g. a 2-game library shown across 5 slots) — no special-casing needed. `GameCarouselScreen` is `IDisposable` — it owns per-game thumbnail surfaces (loaded once at construction, resolved per game via `MainMenuScreen.ResolveAssetPath(game.ThumbnailPath, GameContext.ForGame(...))`; a missing file is not an error, the renderer falls back to a procedurally-drawn placeholder card) and an optional `AnimatedImagePlayer` background (`NEShim/Rendering/AnimatedImagePlayer.cs` — plays back a decoded GIF/WEBP/APNG frame sequence, or a single static image, based on wall-clock elapsed time; backed by `SdlSurfaceLoader.LoadAnimationFromFile`, a thin marshalling wrapper around SDL3_image's `Image.LoadAnimation`/`FreeAnimation`, already available via the existing `SDL3-CS.*.Image` packages with no new dependency). Left/Right selection changes and the Up-to-flip-description card both animate via progress properties (`SlideProgress`, `FlipProgress`) computed from wall-clock elapsed time on every access — the same convention as `LogoScreen.CurrentAlpha` — backed by pure, unit-tested `ComputeSlideProgress`/`ComputeFlipProgress`/`ComputeFlipVisual`/`ComputeBoxArtRect` functions mirroring `LogoRenderer.ComputeDisplayRect`. `GameScanner.Scan(gamesRoot)` enumerates `games/*/config.json` to build the list shown (independent of `MultiGameMode.IsActive`'s manifest-based detection — a `games/` folder can exist with zero valid subfolders while multi-game mode is still correctly active, e.g. DLC still downloading) and never skips a folder: a missing/unparseable `config.json`, or a config whose `RomPath` doesn't resolve to a real file, produces a `GameManifest` with `IsValid: false` instead of being dropped, so the carousel can render a generic "Game Error" note (`LocalizationData.CarouselUnavailable`) on that entry rather than silently omitting it; `Confirm()` no-ops on an invalid entry. The specific reason is deliberately not carried on `GameManifest` or shown on screen at all — a broken entry is almost always either a Steam download problem Steam itself flags, or a publisher packaging mistake caught in testing, neither of which a player needs the detail for — but `GameScanner` always writes it to `neshim.log` via `Logger.LogAlways` (unlike `Logger.Log`, not gated behind `EnableLogging`) so it's recoverable for support purposes even from a default-config run. A missing thumbnail image is deliberately not a validity failure.

**Carousel localization**: unlike the v1 carousel, `GameCarouselScreen`/`GameCarouselRenderer` are fully localized — `NEShimApp.InitializeCarousel()` calls the existing `LoadLocalization()` (which only depends on `_config.Language`, already populated from the shell manifest by that point) before scanning games or constructing the carousel, and passes the result to both `GameScanner.Scan` and the `GameCarouselScreen` constructor (exposed as `GameCarouselScreen.Localization`, read by the renderer — same shape as `MainMenuScreen`/`InGameMenu`, minus `UpdateLocalization`, since the carousel is fully reconstructed on every entry rather than persisting through a language change). All ten `lang/*.json` files carry the full key set (`NEShim.Tests/Integration/LangFileValidationTests.cs` enforces this — every language file must have exactly the same keys as `english.json`, no more, no fewer). The renderer also scales title/placeholder-glyph text size by each tile's slot scale (`GameCarouselRenderer.ScaledTitlePtSize`, floored so far-offset tiles stay legible) and omits the old game-count/arrow-glyph hints — the centered, highlighted tile already conveys selection state, so a numeric counter and "<"/">" glyphs added nothing a player needed. Both `MainMenuScreen` and `InGameMenu` gain a "Change Game" item (only shown when `MultiGameMode.IsActive` — `MainHandler`/`RootHandler` each gate it with their own `HasChangeGame` check, mirroring each other) that eagerly tears down the current game via `UnloadCurrentGame()` and returns to a freshly-scanned, freshly-filtered carousel — distinct from "Return to Main Menu" (in-game menu only), which stays within the same game. `InitializeCarousel()` reloads `_config` from `MultiGameMode.ManifestPath` every time it runs (not just on first boot) so shell-level fields like `CarouselBackgroundPath` are correctly re-applied after "Change Game," rather than leaking the just-exited game's own config.

## Publishing Checklist

Before building a release for a specific game:

- **Window title**: set `WindowTitle` in `config.json` to the game's name.
- **Language**: `lang/*.json` files ship alongside the exe. The active language is read from Steam at startup; set `language` in `config.json` as a fallback for non-Steam launches. Ten languages are built in (english, french, german, spanish, latam, japanese, korean, russian, schinese, portuguese). Add translated achievement names in the Steamworks dashboard — the unlock notification pulls the display name from Steam automatically.
- **Exe icon**: set `<ApplicationIcon>path/to/icon.ico</ApplicationIcon>` in `NEShim/NEShim.csproj` and place a valid `.ico` file at that path. This controls the icon shown in Windows Explorer, the taskbar, alt-tab, and Steam. Do not attempt to configure the icon at runtime — only the compile-time embedded icon affects the exe's file icon and Steam library entry.
- **Signing keypair**: run `seal-achievements --gen-keypair` once per game. Set the printed public key as `achievementPublicKey` in `config.json` (pre-built release) or in `AchievementSigner.EmbeddedPublicKeyBase64` and rebuild (source build). Store the private key outside source control. Achievements are disabled until a key is configured — there is no shipped default.
- **Achievements**: edit `achievements.json`, then run `seal-achievements --key-file private_key.txt achievements.json` to stamp ECDSA-P256 signatures. Re-seal any time a definition changes.
- **steam_appid.txt**: the file in the output directory must contain the real Steam App ID (not `0`). `SteamAPI.RestartAppIfNecessary` and `SteamAPI.Init` both read this file. During development the source-tree copy contains `0` (skips restart, still inits if Steam is running); the publish pipeline must replace it with the real ID.
- **Steamworks native library**: not included in the repository. After `dotnet publish`, copy the matching library from the [Steamworks.NET GitHub release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe: `steam_api64.dll` for Windows (`win-x64`), `libsteam_api.so` for Linux (`linux-x64`). Use the copy bundled with the wrapper (matched version); do not pull it from the Steamworks SDK partner dashboard separately.

### Publishing Checklist — multi-game

Additive to the checklist above — a multi-game publish still needs every item above done once per game, plus:

- **`games/multigame.json`**: ship this shell-config manifest with the base engine publish (`local-publish.ps1`'s output) — its mere presence is what activates multi-game/carousel mode (see "Multi-Game Mode" above). It is *not* created automatically by any publish script; author it by hand (same `AppConfig` schema as `config.json`, but only window/language/`NoLogo`/`carouselBackgroundPath` fields are meaningful here). `carouselBackgroundPath` is relative to the `games/` folder (where the manifest lives) and accepts a static image or an animated GIF.
- **Per-game content**: build each `games/<gameId>/` folder with `local-publish-game.ps1` (`-GameId`, `-SourceDir`, and either `-OutDir` or `-EngineOutDir`) — independent of, and run in addition to, the engine binary publish. Set `thumbnailPath` (box art in NES-box aspect ratio, ~1.42:1) and `gameDescription` (shown on the carousel's flip-card back) in the game's own `config.json` — both are optional; a missing thumbnail shows a placeholder card rather than blocking the entry.
- **DLC vs. bundled, per game**: choose independently for each game. Leave `steamDlcAppId` at its default 0 to bundle that game directly in the base install/depot (shows unconditionally, no Steamworks setup needed) — appropriate for a fixed anthology sold as one purchase, and this can apply to *every* game in the build for a single-deploy release with zero DLC depots at all. Or create a Steam Partner DLC depot for that `games/<gameId>/` folder and set the depot's App ID as `steamDlcAppId`, to sell/gate it separately. A build can mix both freely.
- **DLC ownership anti-tamper — unsigned tier (optional, recommended minimum for any DLC-gated game)**: add an entry to `games/multigame.json`'s `gameDlcAppIds` for every game whose `steamDlcAppId` is nonzero, mapping its `gameId` to that same App ID — e.g. `"gameDlcAppIds": { "kaaz": 3010000 }`. Without an entry here, a per-game `steamDlcAppId` is trusted as-is, which a copied/leaked DLC folder's config.json could edit down to 0 to bypass the ownership check. Bundled games (steamDlcAppId 0) need no entry at this tier.
- **DLC ownership anti-tamper — signed tier (optional, real tamper-proofing, recommended for a real commercial DLC release)**: run `seal-achievements --gen-keypair` to generate a **fresh keypair dedicated to this purpose — do NOT reuse the achievement-signing keypair.** The two protect different things (gameplay-trigger integrity vs. DLC ownership integrity); a leaked or rotated key for one must never force touching the other, and a second keypair costs nothing to generate. Compile the printed public key into `NEShim.AchievementSigning/DlcMapSigner.cs`'s `EmbeddedPublicKeyBase64` constant, and rebuild from source — **this key is deliberately never config-driven**, so it cannot be set via `games/multigame.json` or any `config.json`. Then seal the map: `seal-achievements --seal-dlc-map --key-file private_key.txt games/multigame.json` (writes `gameDlcAppIdsSignature` into the manifest in place). Once the embedded key is set, `gameDlcAppIds` must be *complete* — list every game, including bundled ones with value `0` — or that game is rejected; and any signature problem (missing, or the map changed since sealing) rejects *every* game, fail-closed, rather than silently trusting the unsigned data. Re-run `--seal-dlc-map` any time `gameDlcAppIds` changes.
- **Achievement `steamId` namespacing**: all N games share one Steam base App ID and therefore one Steamworks achievement schema — prefix `steamId`s per game (e.g. `KAAZ_ACH_WIN` vs `GAME2_ACH_WIN`) to avoid collisions across games.
- **Compile-time achievement key**: `AchievementSigner.EmbeddedPublicKeyBase64` must stay unset (`null`) for multi-game builds — there is no single correct key for N different games' `achievements.json` files. Each game's own `AchievementPublicKey` in its `config.json` is what resolves at runtime instead, via the existing precedence logic in `AchievementConfigLoader`.
- **`steam_appid.txt` / `game_actions_<appid>.vdf`**: unchanged from the single-game checklist — both stay singular/app-wide, since the whole multi-game install is still one Steam base App ID and one running process.

## License Policy

This project is licensed **Apache 2.0** and is intended for commercial distribution via Steam. Every dependency compiled into the shipped binary must be compatible with commercial closed-source distribution.

### Attribution
All MIT-licensed compiled dependencies require their copyright notices to be preserved in distributions. These are collected in `THIRD-PARTY-NOTICES.md` at the repository root. Update that file whenever a compiled dependency is added, removed, or upgraded to a new major version.

### Permitted licenses for new dependencies
MIT, Apache 2.0, BSD 2-Clause, BSD 3-Clause, ISC, Unlicense/Public Domain. All current compiled dependencies already fall in this set:

| Package | License | Copyright |
|---|---|---|
| BizHawk source (adapted) | MIT | Copyright (c) 2012-present BizHawk contributors |
| blip_buf.dll (TASEmulators fork) | MIT | Copyright (c) 2003-2009 Shay Green; fork © BizHawk contributors |
| libbizhash.dll | MIT | Copyright (c) 2012-present BizHawk contributors |
| NAudio | MIT | Copyright 2020 Mark Heath |
| Newtonsoft.Json | MIT | Copyright © James Newton-King 2008 |
| CommunityToolkit.HighPerformance | MIT | Copyright © .NET Foundation and Contributors |
| Steamworks.NET | MIT | Copyright (c) Riley Labrecque |
| Vortice.Windows (Vortice.Direct3D11) | MIT | Copyright © Amer Koleci and contributors |

### Prohibited licenses — do not add
- **GPL v1/v2/v3** — copyleft infects the entire binary; incompatible with commercial distribution
- **LGPL** — dynamic linking exception is ambiguous under .NET's AOT/bundling; avoid unless you have confirmed it can be safely isolated as a separate DLL that ships unmodified
- **AGPL** — network-service copyleft; prohibited
- **SSPL, BUSL, Commons Clause** — source-available but not commercially distributable
- **CC BY-NC / CC BY-SA** — non-commercial or share-alike restrictions

When evaluating a new NuGet package, check its repository license **and** the licenses of its transitive dependencies (`dotnet list package --include-transitive`).

### Steamworks SDK
The underlying Steamworks C++ SDK (wrapped by `Steamworks.NET`) is governed by the [Valve Steamworks SDK license](https://partner.steamgames.com/documentation/sdk_access_agreement). Key constraint: the SDK may only be used to distribute software through the Steam platform. This is separate from, and in addition to, the code license requirements above.
