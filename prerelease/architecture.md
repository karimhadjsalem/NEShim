---
layout: default
title: Architecture
nav_order: 5
parent: Pre-release
description: "Internals: thread model, subsystem design, patterns, and how to extend NEShim."
---

# Architecture

This page describes the internal design of NEShim for contributors and anyone extending the project. It covers the project structure, thread model, key patterns, and how all the pieces fit together.

---

## Projects

| Project | Target | Purpose |
|---|---|---|
| `NEShim` | `net9.0` | Main application — SDL3 windowing + rendering, Steam wiring, game loop (Windows x64 + Linux x64) |
| `NEShim.AchievementSigning` | `net9.0` | Shared library — `AchievementDef` type, ECDSA-P256 signing/verification logic |
| `NEShim.SealAchievements` | `net9.0` | Developer CLI tool — stamps ECDSA-P256 signatures onto `achievements.json` (Windows + Linux) |
| `NEShim.SealAchievementsUI` | `net9.0-windows` | Developer GUI tool — Windows Forms UI for the sealer (Windows only) |
| `NEShim.Tests` | `net9.0` | NUnit test suite |
| `BizHawk` | `net8.0` | NES emulation core, adapted from the BizHawk multi-system emulator |

`NEShim` targets `net9.0` (cross-platform) with platform-specific files selected at build time via MSBuild `<Compile Remove>` conditions: D3D11 renderer, Steam overlay renderer, and their factory implementations are excluded on non-Windows; the SDL_GPU renderer factory and null overlay renderer are excluded on Windows.

---

## Namespace map

| Namespace | Responsibility |
|---|---|
| `NEShim.Config` | `AppConfig` POCO + `ConfigLoader` (JSON load/save) |
| `NEShim.Emulation` | `EmulatorHost` — owns the `NES` instance, exposes its services; adapters and stubs |
| `NEShim.GameLoop` | `EmulationThread` — timing, hotkeys, pause logic, per-frame orchestration |
| `NEShim.Rendering` | `IFrameRenderer` strategy (Windows: `D3D11Renderer` primary / `SDL3HwRenderer` fallback; Linux: `SDL3HwRenderer` via SDL_GPU/Vulkan), `IMenuSceneProvider` pull interface, `SDL3PaintContext` (cross-platform paint surface — wraps an SDL_Surface for menu/HUD rendering), `SDL3FontCache` (SDL3_ttf font lifecycle; keyed by family+size; dispose-tracked), `SdlSurfaceLoader` (cross-platform image loader using SDL3_image), `IOverlayRenderer` (Windows: `SteamOverlayRenderer`; Linux: `NullOverlayRenderer`), `OverlayRenderer` (stateless static helpers — `DrawFps(SDL3PaintContext, rect, fps)` and `DrawToast(SDL3PaintContext, rect, text)` — called by both renderers), `FrameBuffer` (double-buffer), `D3DOverlayHook` (Windows: D3D11 device + swap chain bound to SDL HWND); **D3D11 subsystems** (`Rendering/Filters/Dx11/`): `ID3D11Filter` + 7 implementations, `D3D11FilterFactory`; **SDL subsystems** (`Rendering/Filters/SDL/`, `Rendering/MotionEffects/SDL/`, `Rendering/SDL/`): `ISdlFilter` (mirrors `ID3D11Filter` for SPIR-V; exposes `PixelShaderResourceName`, `NumFragmentSamplers`, `NumFragmentUniformBuffers`, `WriteUniformData`), `SdlGpuRenderState` (wraps one SDL_GPUShader + SDL_GPURenderState pair; `Apply(Span<float>)` uploads uniforms; `Clear()` restores default pipeline; created per active filter, disposed on filter change), `SdlFilterFactory` (maps `VideoFilterMode` → `ISdlFilter`), `ISdlMotionEffect` (extends `IMotionEffect`; adds `SpvResourceName`, `NumFragmentSamplers`, `NumFragmentUniformBuffers`), `SdlMotionEffectFactory` (maps `VideoMotionEffectMode` → `IMotionEffect`; `PhosphorPersistence` → `NoneMotionEffect` with log entry); **shared** (`Filters/`, `MotionEffects/`): `IMotionEffect`, `MotionEffectFactory` (D3D11 path), `VideoFilterMode`, `VideoColorFilterMode`, `VideoMotionEffectMode` |
| `NEShim.Audio` | `AudioPlayer` (SDL3 audio stream bridge — `SDL.OpenAudioDeviceStream` + callback), 8 `IAudioProcessor` implementations, `AudioEqProcessor`, `MainMenuMusic` |
| `NEShim.Input` | `InputManager`, `InputSnapshot`; `SDL3GamepadDevice`, `SDL3GamepadSource`, `SDL3GamepadMapper`; `SteamInputSource`, `SteamInputMapper`; `KeyboardInputSource`, `KeyboardMapper` |
| `NEShim.Saves` | `SaveStateManager` (8 slots + auto), `SaveRamManager` |
| `NEShim.Platform` | `PlatformDetector` — Wine/Proton detection (`IsWine`), SteamDeck detection (`IsSteamDeck`), `IsD3D11Active` (set once at startup), `ConfigureVideoDriverForSteamOverlay()` (Linux: sets `SDL_VIDEODRIVER=x11` before `SDL_Init`), `BeginHighResolutionTiming`/`EndHighResolutionTiming`; `SDL3WindowHost` — SDL3 window lifecycle, event loop, marshal queue; `MenuScale` — font/layout scale factor |
| `NEShim.UI` | `InGameMenu` + `MainMenuScreen` state machines; `MenuRenderer` + `MainMenuRenderer` (stateless `Draw(SDL3PaintContext, SDL.Rect, state)` entry points); `IMenuInputTarget` (gamepad dispatch interface implemented by `NEShimApp`) |
| `NEShim.Steam` | `SteamManager` — init, overlay callbacks, main-thread tick; `SteamInputManager` — action sets |
| `NEShim.Achievements` | `AchievementManager` — per-frame memory watcher; `AchievementConfigLoader` |

---

## Startup sequence

```
Program.cs
  ├─ PlatformDetector.BeginHighResolutionTiming()
  ├─ SteamAPI.RestartAppIfNecessary(appId)   — exits if not launched via Steam
  └─ new SDL3WindowHost("NEShim", 1024, 672)
       └─ PlatformDetector.ConfigureVideoDriverForSteamOverlay()  [Linux only: SDL_VIDEODRIVER=x11]
       └─ SDL.Init(Video | Events | Audio | Gamepad)
       └─ SDL.CreateWindow(...)
  └─ new NEShimApp(sdlHost).Run()
       └─ NEShimApp.InitializeEmulator()
            1. InitializeConfig()           — ConfigLoader.Load() → AppConfig
            2. InitializeEmulatorCore()    — BizHawkEmulationCore.LoadRom(); AchievementManager?
            3. InitializeSaveSystems()     — SaveManager (states + SRAM)
            4. InitializeRendering()       — FrameBuffer; load sidebar SDL_Surfaces
            5. InitializeInput()           — SDL3GamepadDevice + InputManager (keyboard, SDL3 gamepad, Steam Input)
            6. InitializeAudio()           — AudioPlayer (SDL3 audio stream)
            7. InitializeSteam()           — SteamManager.Initialize() → overlay callback wired
            8. InitializeWindowAndD3DHook()
               ├─ SetWindowMode()
               ├─ OverlayRendererFactory.Create()
               │    Windows: SteamOverlayRenderer (D3DOverlayHook wraps SDL HWND)
               │    Linux:   NullOverlayRenderer
               └─ RendererFactory.Create()
                    Windows: D3D11Renderer (primary) / SDL3HwRenderer (fallback via SDL_GPU)
                    Linux:   SDL3HwRenderer (SDL_GPU/Vulkan)
                    → PlatformDetector.IsD3D11Active set accordingly
            9. ShowLogo() / FinishInitialization()
               └─ InitializeMainMenu(), InitializeInGameMenu()
               └─ EmulationThread.Start() [starts paused at MainMenu]
               └─ AudioPlayer.Start()
  └─ sdlHost.RunLoop(NEShimApp.OnIdle)       — SDL event loop (main thread)
  └─ NEShimApp.Shutdown()
```

All components are wired together in `NEShimApp.InitializeEmulator()` which owns construction, event subscription, and lifetime management. There is no dependency injection container — wiring is explicit and centralised.

---

## Thread model

NEShim uses two threads:

### Main thread (SDL event loop)

- Runs `SDL3WindowHost.RunLoop(NEShimApp.OnIdle)` — polls `SDL.PollEvent` until the event queue is drained, drains the `MarshalToMainThread` action queue, then calls `OnIdle`.
- Receives SDL keyboard and window events; forwards keyboard events to `InputManager`.
- Receives SDL gamepad events (`SDL_EVENT_GAMEPAD_ADDED/REMOVED`) for hot-plug detection.
- `SDL_EVENT_WINDOW_FOCUS_LOST` → `SetPauseReason(FocusLost, true)`; `SDL_EVENT_WINDOW_FOCUS_GAINED` → clears it.
- `NEShimApp.Shutdown()` stops the emulation thread and writes persistence files.
- `SteamManager.Tick()` is called from `OnIdle` when the emulation loop is paused, keeping the Steam overlay hook alive.

### Emulation thread (`EmulationThread.Loop`)

High-priority background thread running at ~60 Hz (timed to the NES's VSync rate).

Per-frame sequence:
1. `InputManager.PollSnapshot()` — read keyboard + gamepad
2. `NesController.Update()` — push snapshot to BizHawk
3. `HandleHotkeys()` — edge-triggered system actions (save/load slot, menu open)
4. `InputManager.AdvanceHotkeyState()` — advance edge-detection state
5. **Pause check** — if `_pauseReasonBits != 0`, block on `ManualResetEventSlim`, polling for gamepad menu nav
6. `EmulatorHost.RunFrame()` — advance NES by one frame
7. `AchievementManager.Tick()` — evaluate memory triggers
8. `FrameBuffer.WriteBack()` + `FrameBuffer.Swap()` — copy video to front buffer
9. **Frame dispatch (non-blocking, via `MarshalToMainThread`):**
   - D3D11 active: `D3D11Renderer.UploadFrame(FrontBuffer)` then `D3D11Renderer.DrawAndPresent(vsync: true)` — upload and present are enqueued together in the same `MarshalToMainThread` action so Present fires immediately after the texture is ready, with no clock drift. After `Present()` returns, `SteamManager.RunCallbacksAfterPresent()` is invoked immediately within the same action (see [Steam overlay: Gamescope callback timing](#gamescope-callback-timing) below).
   - SDL3HwRenderer fallback (Windows) or primary (Linux): `SDL3HwRenderer.UploadFrame()` + `SDL3HwRenderer.Tick()` via SDL_GPU/Vulkan.
10. `AudioPlayer.Enqueue()` — push audio samples to ring buffer
11. FPS tracking
12. Frame timing — sleep + spin to hit the target timestamp

Steam requires callbacks to be dispatched on the same thread that called `SteamAPI.Init()`.

**Cross-thread rules:**
- The emulation thread never calls SDL or main-thread APIs directly — always via `MarshalToMainThread` (a `ConcurrentQueue<Action>` drained by the SDL event loop).
- `InputManager._pressedKeys` is protected by a lock (keyboard events arrive on the main thread via SDL events; reads happen on the emulation thread).
- `FrameBuffer` is protected by a `SpinLock` at swap time.
- `_pauseReasonBits` is a `volatile int` updated with CAS (`Interlocked.CompareExchange`) from either thread.

---

## Pause reasons

`EmulationThread.PauseReasons` is a `[Flags]` enum. The loop blocks whenever any bit is set:

| Bit | Name | Set when | Cleared when |
|---|---|---|---|
| 1 | `Menu` | In-game pause menu opened, or controller disconnected mid-game | Menu closed / disconnect overlay dismissed |
| 2 | `Overlay` | Steam overlay opened | Overlay closed |
| 4 | `FocusLost` | Window loses focus (`SDL_EVENT_WINDOW_FOCUS_LOST`) | Window gains focus (`SDL_EVENT_WINDOW_FOCUS_GAINED`) |
| 8 | `MainMenu` | App starts / user returns to main menu | User picks New Game or Resume |
| 16 | `DeviceLost` | D3D11 device removed (GPU driver reset, suspend/resume) | D3D11 reinitialised successfully |

`SetPauseReason(reason, active)` uses a CAS loop to atomically set or clear the bit. When the result is non-zero the audio is muted and the `ManualResetEventSlim` is reset; when it reaches zero the audio is unmuted and the event is set to unblock the loop.

---

## Frame buffer (double-buffer)

```
Emulation thread          Main thread (MarshalToMainThread action)
─────────────────         ──────────────────────────────────────────
WriteBack(pixels)  →  [back buffer]
Swap()             ←──── SpinLock ────→  FrontBuffer (read only)
                          │
                    Renderer.UploadFrame reads FrontBuffer
```

`WriteBack` copies the NES pixel array into the back buffer. `Swap` atomically flips `_frontIndex` under a `SpinLock`. The renderer always reads from `FrontBuffer` — it never touches the back buffer.

When the pause menu is open, the emulation loop does not run `RunFrame`, so the front buffer holds the last frame before the pause. In D3D11 and SDL_GPU modes the NES texture persists in the GPU between frames; the last rendered frame is visible beneath the semi-transparent menu overlay without any extra copy.

---

## State machines and renderers

Both menus follow the same two-class pattern:

| Class | Responsibility |
|---|---|
| `InGameMenu` | Owns state (`Current`, `SelectedItem`, `IsOpen`). Handles all input (keyboard, gamepad). Drives transitions. Fires events. |
| `MenuRenderer` | Stateless, `internal static`. Single entry point `Draw(SDL3PaintContext, SDL.Rect, InGameMenu)`. Creates and disposes all SDL3 resources within the call. |
| `MainMenuScreen` | Same as `InGameMenu` but for the pre-game menu. |
| `MainMenuRenderer` | Same as `MenuRenderer` for the pre-game menu. |

**Rule:** Never put rendering logic inside a state machine. Never put state mutation inside a renderer. This separation makes both independently testable — the state machines are tested without a graphics context; the renderers are not tested (they are pure SDL3 drawing).

### Per-screen handler pattern

Each menu uses a **per-screen handler** internally (nested private classes implementing an abstract `ScreenHandler` base). Each handler owns exactly one screen's title, item list, enabled-state logic, and activation logic. The state machine dispatches to the current screen's handler via a `Dictionary<Screen, ScreenHandler>` built at construction time.

This means adding a new screen requires only: add an enum value, add a handler class, add one entry to `BuildHandlers()`. There are no parallel switch statements to keep in sync.

Handlers are nested private classes and therefore have full access to all private fields and methods of their enclosing menu class.

---

## Audio

### SDL3 audio stream bridge

`AudioPlayer` bridges the emulation thread (producer) and the SDL3 audio callback (consumer) via a `short[]` ring buffer.

- **Producer:** `EmulationThread` calls `Enqueue(samples, count)` each frame.
- **Consumer:** SDL3's audio callback (`OnAudioGetCallback`) calls `Read()` then `SDL.PutAudioStreamData(stream, buffer, length)` to push samples to the device.
- **Device:** Opened with `SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, in spec, callback, userData)`.
- **Pause:** `SetPaused(true)` calls `SDL.PauseAudioStreamDevice`; `SetPaused(false)` calls `SDL.ResumeAudioStreamDevice`. The ring buffer is drained on pause to prevent stale audio playing on resume. The processor state is also reset to avoid a pop from DC offset in the filter memory.

### Audio processors

`IAudioProcessor` is a single-method interface:

```csharp
(short L, short R) Process(short monoSample);
void ResetState();
```

The active processor can be swapped at runtime via `AudioPlayer.SetProcessor()`. The new processor's state is reset before it takes effect to avoid pops. Eight implementations ship, selected via the `audioFilter` config field and the Audio Filter sub-menu in both the in-game pause menu and the main menu:

| Class | `audioFilter` value | Description |
|---|---|---|
| `NesFilterProcessor` | `"Default"` | Emulates the NES hardware output filter: HP@37Hz → HP@39Hz → LP@14kHz. Accurate to the real hardware. |
| `SoundScrubberProcessor` | `"Warm"` | Modified filter for warmer sound: HP@80Hz → HP@80Hz → LP@14kHz → LP@8kHz. The raised HP cutoffs tighten bass transients; the extra LP stage removes harsh square-wave harmonics. |
| `PseudoStereoProcessor` | `"PseudoStereo"` | Standard NES filter chain + Haas-effect stereo widening. L = direct × 0.6, R = 20 ms delayed × 0.4. Constructor accepts `delayMs` so tests can warm up quickly. |
| `WarmStereoProcessor` | `"WarmStereo"` | `PseudoStereo` + independent LP@8kHz per channel applied after the Haas split. |
| `CompressionProcessor` | `"Compression"` | Standard NES chain + look-ahead RMS compressor (220-sample window, −6 dBFS threshold, 3:1 ratio, +2 dB makeup). Evens out DPCM channel level spikes. Running sum-of-squares for O(1) RMS; no per-sample buffer traversal. |
| `BassBoostProcessor` | `"BassBoost"` | Standard NES chain + additive low-shelf LP@150Hz (`β ≈ 0.979`). Adds ~+4 dB at DC, ~+2 dB at 150 Hz; no effect above 1 kHz. For fuller sound on bass-light speakers or headphones. |
| `TapeSaturationProcessor` | `"Saturation"` | Standard NES chain + tanh soft-clip (`Drive = 1.5`, normalized so 0 dBFS → 1.0). Super-linear below full scale (mid-level signals get a small boost); smooth saturation at peaks. Never clips to digital full-scale. |
| `DmcStabilizerProcessor` | `"DmcStabilizer"` | Slew-rate limiter applied before the standard NES filter chain. When consecutive sample delta exceeds ~18% of full scale, the jump is blended 50% toward the previous value, significantly reducing audible pops and clicks from DPCM samples. |

### Main menu music

`MainMenuMusic` plays a looping audio file via an SDL3 audio stream with smooth 1-second fade-in and 0.5-second fade-out transitions. Volume is split into `_fadeLevel` (0–1, driven by a timer) and `_masterVolume` (user-controlled). The audible output is `_fadeLevel × _masterVolume`, so master volume changes during a fade behave correctly.

Looping is handled by seeking the decoded audio source back to position 0 when it is exhausted. Volume changes are applied by calling `SDL.SetAudioStreamGain` on the stream.

---

## Steam overlay

### Windows (D3D11 path)

Steam's overlay DLL (`GameOverlayRenderer64.dll`) hooks `IDXGISwapChain::Present` at the vtable level — without that hook, `SteamUtils.IsOverlayEnabled()` stays `false` and Shift+Tab does nothing.

**`D3DOverlayHook`** creates a D3D11 device and swap chain bound to the HWND obtained from `SDL3WindowHost.Handle` (`SDL.GetPointerProperty` on the SDL window). `D3D11Renderer.DrawAndPresent()` calls `SwapChain.Present()` every ~16 ms, which Steam intercepts. When D3D11 is unavailable and `SDL3HwRenderer` is the active renderer instead, no `D3DOverlayHook` device is created — the Steam overlay is not available on the Windows fallback path in that case.

The swap chain uses `SwapEffect.FlipDiscard` (required for DXVK on Proton — see [Proton / Steam Deck notes](#proton--steam-deck-notes) below).

### Linux (SDL_GPU / LD_PRELOAD path)

On Linux, Steam injects its overlay via `LD_PRELOAD` (`steamoverlayvulkanlayer.so`), which hooks `vkQueuePresentKHR`. No D3D device or swap chain is needed. `NullOverlayRenderer` is the `IOverlayRenderer` implementation on Linux — it is a no-op that satisfies the interface without doing anything.

**`SDL_VIDEODRIVER=x11` requirement:** Steam's Vulkan overlay layer requires an XCB or Xlib window surface — it does not support the Wayland EGL surface SDL3 would otherwise create. `PlatformDetector.ConfigureVideoDriverForSteamOverlay()` sets `SDL_VIDEODRIVER=x11` before `SDL.Init()` on Linux, forcing SDL3 to use the X11 backend (which creates an Xlib surface compatible with the overlay layer). This is a Linux-only code path guarded by a runtime `OperatingSystem.IsLinux()` check. If Steam is not running, the environment variable has no effect.

### Gamescope callback timing

On Steam Deck, Gamescope (the Wayland compositor Proton uses) can block `vkQueuePresentKHR` for the duration of its own overlay presentation. While it is blocked, the Windows message pump is starved — `WM_TIMER` messages do not fire, so `_steamTimer` cannot call `SteamAPI.RunCallbacks()`. As a result, `GameOverlayActivated_t` is never dispatched, `SetPauseReason(Overlay, true)` is never called, and the emulation loop runs unpaused underneath the overlay.

The fix: `SteamManager.RunCallbacksAfterPresent()` is called immediately after `Present()` unblocks, within the same `BeginInvoke` lambda that dispatches `DrawAndPresent`. This guarantees callbacks fire on the first frame after Gamescope releases the call, before the next emulation frame begins.

`RunCallbacksAfterPresent()` does not run the store-retry logic (only `Tick()` on the timer does), so there is no double-counting of the retry countdown when both fire in the same tick.

### Audio during overlay

`SetPauseReason(Overlay, true)` mutes `AudioPlayer` (the NES APU SDL3 audio stream) and blocks the emulation loop. However, `MainMenuMusic` has its own independent SDL3 audio stream that `AudioPlayer` does not control. When the overlay opens on the main menu screen, `MainMenuMusic.Pause()` must be called separately; `MainMenuMusic.Resume()` is called when the overlay closes. This is wired in `NEShimApp`'s overlay callback alongside the `SetPauseReason` call.

### Initialisation order

`D3DOverlayHook.Initialize(Handle, Width, Height)` must be called **after** `SetWindowMode()` so the swap chain is created at the window's final dimensions — `Handle` is retrieved from `SDL3WindowHost.Handle`. `D3D11Renderer` is constructed immediately after `D3DOverlayHook.Initialize()`. An SDL `SDL_EVENT_WINDOW_RESIZED` handler calls `D3D11Renderer.Resize()` (which calls `ResizeBuffers` internally) to keep the swap chain and viewport in sync with the window.

### Proton / Steam Deck notes

DXVK is the Vulkan translation layer Proton uses for D3D11. Key behaviors:

- **`SwapEffect.FlipDiscard` is required.** The legacy `Discard` effect is emulated in DXVK via a slower blit path. `FlipDiscard` maps cleanly to Vulkan's `VK_PRESENT_MODE_FIFO_KHR`.
- **`RowPitch` alignment.** DXVK aligns texture row pitches for Vulkan buffer compatibility. `D3D11Renderer.UploadFrame` always copies row-by-row using `MappedSubresource.RowPitch`, never assuming `width × 4`.
- **Shader cache.** DXVK compiles the passthrough DXBC shaders to SPIR-V on first launch and caches them in `~/.local/share/Steam/steamapps/shadercache/<appid>/`. The passthrough shaders are trivially simple; compilation is near-instant. Subsequent launches use the cached SPIR-V with no stutter.
- **Testing on Proton requires the publish script.** Run `local-publish.ps1` before copying to a Steam Deck. `dotnet build` output omits `--self-contained` and `PublishReadyToRun`, both of which matter significantly for frame-rate on Proton. See `CLAUDE.md` for details.

### `SteamAPI.RestartAppIfNecessary`

`Program.Main` calls `SteamAPI.RestartAppIfNecessary(appId)` before `new SDL3WindowHost(...)`. It reads the App ID from `steam_appid.txt`. If the game was launched directly (not via Steam), the call returns `true` and the process exits so Steam can relaunch it with the overlay library already injected — on Windows this is `GameOverlayRenderer64.dll` (hooks `IDXGISwapChain::Present`); on Linux it is `steamoverlayvulkanlayer.so` (injected via `LD_PRELOAD`, hooks `vkQueuePresentKHR`) — in both cases the library must be loaded before the graphics device is created.

---

## D3D11 renderer and device loss (Windows only)

### Ownership model

`D3DOverlayHook` owns the D3D11 device and DXGI swap chain — it creates them and disposes them. `D3D11Renderer` is constructed with a reference to both and owns all other rendering objects:

| Resource | Owner |
|---|---|
| `ID3D11Device`, `IDXGISwapChain` | `D3DOverlayHook` |
| `ID3D11DeviceContext` (immediate) | retrieved from device; not disposed |
| `ID3D11Texture2D` (NES texture), SRV | `D3D11Renderer` |
| `ID3D11RenderTargetView` | `D3D11Renderer` (recreated on resize) |
| Vertex buffer, VS, PS, input layout, sampler | `D3D11Renderer` |

`D3D11Renderer.Dispose()` releases only the objects it owns. `D3DOverlayHook.Dispose()` is called after `D3D11Renderer.Dispose()` in `NEShimApp.Shutdown()`.

### Device loss recovery

`D3D11Renderer.DrawAndPresent()` checks the `Present()` HRESULT for `DXGI_ERROR_DEVICE_REMOVED` (0x887A0005) and `DXGI_ERROR_DEVICE_RESET` (0x887A0007). If either occurs:

1. `DeviceLost` event fires.
2. `NEShimApp.OnD3DDeviceLost` handles it: sets `PauseReasons.DeviceLost`, disposes `D3D11Renderer` then `D3DOverlayHook`.
3. Recreates `D3DOverlayHook` (new device + swap chain) and `D3D11Renderer`.
4. If recreation succeeds, clears `PauseReasons.DeviceLost` to resume emulation.

Device loss is rare on desktop (typically caused by a GPU driver reset or suspend/resume cycle). On Steam Deck it is more likely during system sleep.

---

## Rendering pipeline

### D3D11 path (Windows primary)

```
NES pixel buffer (int[256×240], 0xAARRGGBB / BGRA in little-endian memory)
  └─ FrameBuffer.WriteBack + Swap (emulation thread)
       └─ MarshalToMainThread — upload and present enqueued together:
            ├─ D3D11Renderer.UploadFrame
            │    └─ Map(WriteDiscard) → row-by-row copy respecting RowPitch
            └─ D3D11Renderer.DrawAndPresent(vsync: true)
                      ├─ SetupPipelineState — bind _activePixelShader (structural filter)
                      ├─ UpdateFilterCbuffer — write structural params + colorMode to b0
                      ├─ DrawSidebars — sidebar quads drawn via passthrough shader
                      │
                      ├─ [Pass 1] Draw letterboxed NES quad via structural filter shader
                      │    • no overlay, no shader ME → write directly to backbuffer (or
                      │      _pictureAdjustRt if any picture adjust is non-zero)
                      │    • overlay active → write to _overlayRt (intermediate)
                      │    • shader ME active → write to _motionEffectRt (intermediate)
                      │    • both active → write to _overlayRt
                      │
                      ├─ [Pass 2, if overlay] Overlay filter reads from _overlayRt,
                      │    writes to _motionEffectRt (if ME active) or backbuffer.
                      │    colorMode applied here (deferred from Pass 1 which uses colorMode=0)
                      │
                      ├─ [Pass 2/3, if shader ME] ME pixel shader reads from _motionEffectRt,
                      │    warps UV coordinates, writes to backbuffer (or _pictureAdjustRt)
                      │
                      ├─ [Final optional pass] DrawPictureAdjust — reads from _pictureAdjustRt,
                      │    applies brightness/contrast/saturation/hue, writes to backbuffer.
                      │    Skipped entirely when all four values are 0.
                      │
                      ├─ DrawOverlay — SDL3PaintContext surface (menus / frozen frame / HUD)
                      │    drawn via passthrough shader, alpha-blended over NES frame
                      └─ SwapChain.Present(syncInterval=1) — vsync on
```

`D3DOverlayHook` creates and owns the D3D11 device and swap chain. `D3D11Renderer` reuses them (passed via constructor) and owns all other rendering resources: NES texture, overlay texture, SRV, RTV, vertex buffer, shaders, input layout, sampler, and filter constant buffer. NES pixels are `B8G8R8A8_UNorm` — no byte-swapping needed.

**D3D11 renders everything** — not just the NES frame. The logo splash, main menu, in-game menu, toasts, achievement banners, and FPS overlay are all composited by `D3D11Renderer` via an overlay texture pipeline. `NEShimApp` implements `IMenuSceneProvider`, returning a paint delegate (`Action<SDL3PaintContext, SDL.Rect>`) for whichever scene is active (or `null` during pure gameplay — zero overhead on the hot path).

### Video filter architecture (D3D11)

Two independent filter axes can be combined freely:

- **Video Filter** (`videoFilter` in config): a structural filter — controls how the NES frame is sampled and stylised. Options: `PixelPerfect`, `Bilinear`, `CrtScanlines`, `CrtPhosphor`, `NtscComposite`, `CrtScreen`, `Xbr` (Sharp Pixel). Implemented as DXBC pixel shaders (or sampler-only for `Bilinear`) compiled to `.cso` files and embedded as assembly resources.
- **Color Effect** (`videoColorFilter` in config): a color-grade transform applied on top of any structural filter. Options: `None`, `Warm`, `Greyscale`, `NesColorCorrection`, `Cool`, `PhosphorAmber`, `PhosphorGreen`. Not a separate shader — the grade is a cbuffer value consumed by every structural shader via a shared `ColorGrade.hlsli` include.

All pixel shaders use a uniform 4-float constant buffer (`b0`):

```hlsl
cbuffer FilterParams : register(b0)
{
    float param0;     // structural param 0  (nesWidth for CRT, invWidth for NTSC, 0 for PP)
    float param1;     // structural param 1  (nesHeight / frameParity / 0)
    float param2;     // structural param 2  (scanlineIntensity / chromaStrength / 0)
    float colorMode;  // 0=none  1=warm  2=greyscale  3=nes_colors  4=cool  5=phosphor_amber  6=phosphor_green — written by renderer
}
```

The renderer always fills `param[3]` with the active `VideoColorFilterMode` cast to `float`. Each structural filter fills `param[0..2]` via its `WriteBaseParams()` method. For Pixel Perfect all three structural params are zero (no-op), so the shader applies only the color grade.

**Passthrough shader** (`Passthrough.ps.cso`) applies only the color grade — no structural effect. It is used for the overlay quad (menus, frozen frame, HUD elements) and the sidebar quads (letterbox bar artwork). This prevents scanline or NTSC effects from being applied to 2D overlay content or sidebar images. The passthrough shader is temporarily bound before those draws, then restored to `_activePixelShader` after.

**Shader interface:** `ID3D11Filter` (in `NEShim.Rendering.Filters`) exposes `FilterMode`, `PixelAspectRatio`, `PixelShaderResourceName` (null → passthrough), `UseLinearSampler` (default false — override to true for bilinear-sampled filters), `WriteBaseParams(Span<float>, nesWidth, nesHeight)` (default no-op — writes structural params to cbuffer slots [0..2]), and `NotifyFrame(int frameCount)` (default no-op — called once per draw call for filters that animate per-frame, e.g. NTSC noise). `D3D11FilterFactory` maps a `VideoFilterMode` value to the correct implementation. See [Filters](filters.md) for the full filter reference.

### Overlay texture pipeline (D3D11)

```
IMenuSceneProvider.GetActiveScenePainter() — null during gameplay, delegate during menus/logo
  │
  ▼
RenderOverlayBitmap()                       — only runs when _overlayDirty == true
  ├─ g.Clear(Transparent)
  ├─ scenePainter?.Invoke(g, clientRect)    — paints menu/logo onto CPU Bitmap
  ├─ OverlayRenderer.DrawFps(...)           — transient HUD elements on top
  └─ OverlayRenderer.DrawToast(...)
  │
  ▼
UploadOverlayBitmap()
  └─ LockBits(Format32bppArgb) → Map(WriteDiscard) → row-by-row copy (respects RowPitch)
  │
  ▼
DrawAndPresent                              — overlay quad drawn after NES quad
  └─ OMSetBlendState(SrcAlpha / InvSrcAlpha, straight alpha)
       └─ Draw fullscreen overlay quad alpha-blended over the NES frame
```

**Texture format:** The overlay texture is `B8G8R8A8_UNorm` and viewport-sized (matches the swap chain, e.g. 1920×1080 at 1080p). `SDL3PaintContext` renders into an SDL_Surface with `ARGB8888` format — bytes `[B,G,R,A]` in little-endian memory — same layout as `B8G8R8A8_UNorm`, no byte-swapping needed.

**Alpha blending:** Straight alpha (`SourceBlend = SrcAlpha`, `DestBlend = InvSrcAlpha`). `SDL3PaintContext.Clear(transparent)` initialises alpha=0 across the surface; only pixels actively painted by the scene delegate or HUD renderers carry non-zero alpha. The blend equation is `out = src.rgb × src.a + dst.rgb × (1 − src.a)`.

**Conditional upload:** The overlay bitmap is re-rendered and re-uploaded only when `_overlayDirty == true`. All state changes that visually affect the overlay call `MarkOverlayDirty()`:
- Scene transitions: menu open/close, logo start/tick, navigation key/gamepad input
- Transient changes: `UpdateFpsOverlay`, `ShowToast`

Static menu frames between inputs produce no SDL3PaintContext render and no GPU upload — the previously uploaded texture is simply re-composited by the quad draw. At 1080p (1920×1080×4 = ~8 MB), this matters: a static main menu screen costs one 8 MB upload on first display and nothing thereafter until the user presses a key.

### SDL3HwRenderer path (Linux primary / Windows fallback)

Used on Linux (always) and on Windows when D3D11 initialisation fails. Backed by SDL_GPU (SPIR-V/Vulkan on Linux; SPIR-V/D3D11 on Windows via SDL's GPU abstraction). On systems without Vulkan or GPU support, falls back to plain SDL rendering (`_isGpuRenderer = false`) with only Pixel Perfect and Bilinear available.

```
NES pixel buffer (int[256×240], ARGB)
  └─ FrameBuffer.WriteBack + Swap
       └─ MarshalToMainThread → SDL3HwRenderer.UploadFrame + SDL3HwRenderer.Tick
            └─ SDL.LockTexture → row-by-row pixel copy respecting pitch
                 └─ SDL3HwRenderer.DrawAndPresent():
                      ├─ SDL.RenderClear (black)
                      ├─ DrawSidebars — cover-scaled sidebar textures
                      ├─ DrawNesFrame — NES texture via active SPIR-V filter shader
                      │    ├─ optional: shader motion effect pass
                      │    └─ optional: picture adjust pass (render-to-texture)
                      └─ DrawOverlay — SDL3PaintContext surface blit to overlay texture,
                            alpha-blended over NES frame
```

**Filter abstraction layer (`ISdlFilter` / `SdlFilterFactory` / `SdlGpuRenderState`):**

`SDL3HwRenderer.SetFilter(ID3D11Filter filter)` does not use the D3D11 filter object directly — it translates through `SdlFilterFactory.Create(filter.FilterMode)` to obtain an `ISdlFilter` implementation. `ISdlFilter` mirrors the `ID3D11Filter` contract but targets SPIR-V: it exposes `PixelShaderResourceName` (the embedded `.spv` resource name, or null for a passthrough draw), `WriteUniformData(Span<float> dst, int nesWidth, int nesHeight)` (writes the 4-float uniform block), `NumFragmentSamplers`, and `NumFragmentUniformBuffers`. The renderer wraps each active filter in a `SdlGpuRenderState` — a disposable object that creates the `SDL_GPUShader` and `SDL_GPURenderState` once and exposes `Apply(Span<float> uniformData)` (uploads uniforms to slot 0 and activates the render state) and `Clear()` (restores the default SDL pipeline). When the active filter changes, the old `SdlGpuRenderState` is disposed and a new one is created for the replacement filter.

**Motion effects (`ISdlMotionEffect` / `SdlMotionEffectFactory`):**

Three motion effects are fully supported: **CRT Jitter**, **Scanline Bob**, and **Magnetic Distortion**. `SdlMotionEffectFactory.Create(mode)` returns the correct `IMotionEffect` implementation, or `NoneMotionEffect` for `PhosphorPersistence` (logged: "requires two texture samplers and cannot be expressed via SDL_GPURenderState"). Shader-backed effects (`MagneticDistortion`) additionally implement `ISdlMotionEffect`, which extends `IMotionEffect` with `SpvResourceName` (the embedded `.spv` resource name), `NumFragmentSamplers`, and `NumFragmentUniformBuffers`. When `SetMotionEffect` selects a shader-backed effect, the renderer allocates a `SdlGpuRenderState` for the motion effect shader and runs an additional render-to-texture pass. CPU quad-offset effects (CRT Jitter, Scanline Bob) use only `GetFrameOffset` — no extra pass or `SdlGpuRenderState`.

**Aspect ratio:** `SDL3HwRenderer` computes a letterboxed destination rectangle preserving the 8:7 NES pixel aspect ratio (`256 × (8/7) : 240 ≈ 1.212`), producing black (or artwork) bars on the sides for widescreen displays.

**Overlay:** Uses an SDL software renderer targeting an `SDL_Surface` (the `SDL3PaintContext`), uploaded to a streaming texture each dirty frame and composited with `SDL_SetTextureBlendMode(Blend)`.

**Video Overlay not supported:** `SDL3HwRenderer.SetOverlayFilter()` is a no-op — two-pass overlay requires temporal intermediate textures and is D3D11-only. The `PhosphorPersistence` motion effect is similarly unavailable — see [Motion effects](filters.md#motion-effect).

### Renderer mode flag

`PlatformDetector.IsD3D11Active` is set once at startup:
- `true` — D3D11 device successfully initialised via `D3DOverlayHook`; `D3D11Renderer` is the active frame renderer. All video features are available: structural filters (DXBC), Video Overlay (two-pass D3D11 intermediate RT), color effects, all four motion effects including PhosphorPersistence (Screen Glow), picture adjustments, and presets.
- `false` — `SDL3HwRenderer` is the active renderer (Linux always; Windows when D3D11 fails). Structural filters (SPIR-V via SDL_GPU), color effects, CRT Jitter / Scanline Bob / Magnetic Distortion motion effects, and picture adjustments are all fully supported. Video Overlay is not available (`SetOverlayFilter` is a no-op); PhosphorPersistence demotes to None (`SdlMotionEffectFactory` returns `NoneMotionEffect` with a log entry).

The `VideoFilterModeParser.D3D11Supported` array lists all seven filters: `PixelPerfect`, `Bilinear`, `CrtScanlines`, `CrtPhosphor`, `NtscComposite`, `CrtScreen`, and `Xbr`. All seven have matching SPIR-V shaders and work on `SDL3HwRenderer` as well. If a shader filter is loaded from `config.json` but the active renderer cannot find the shader (e.g. GPU renderer unavailable), NEShim logs a warning, falls back to `PixelPerfect`, and saves the change to `config.json`.

---

## Save system

### Save states

`SaveStateManager` wraps BizHawk's `IStatable` interface:

- **8 named slots** stored as `slot{n}.state` (binary) + `slot{n}.meta` (JSON timestamp).
- **Auto-save** stored as `autosave.state`. Written when the in-game menu opens, every ~5 minutes during active play (18,000-frame counter in `EmulationThread.Loop`), and on graceful exit — never before the player has started a game session (i.e., if the player exits from the pre-game main menu without ever starting play, no auto-save is written).
- `ActiveSlot` is persisted to `config.json` on exit.

BizHawk's `IStatable` serialises the full emulator state (CPU registers, RAM, PPU, APU, mapper) to a `BinaryWriter`. Restoring from a state is immediate and cycle-accurate.

### Battery RAM

`SaveRamManager` wraps `ISaveRam`:

- `LoadFromDisk()` is called at startup, before the first frame. If no `.srm` file exists, the emulator starts with uninitialised save RAM (same as a fresh cartridge).
- `SaveToDisk()` is called on exit, but only after the player has started a game session. Note: `ISaveRam.SaveRamModified` on the BizHawk NES core returns `true` for any cartridge that has a save RAM array, regardless of whether the game has written to it — it is not a write-tracking flag. The `_gameHasStarted` guard in `NEShimApp` is what prevents the SRM file from being created on first launch before any play.

---

## JSON loading

Both `config.json` and `achievements.json` are loaded with **`System.Text.Json`** (the BCL library, `System.Text.Json.JsonSerializer`) — not Newtonsoft.Json. Each file is deserialized into a strongly-typed POCO (`AppConfig` or `GameAchievementConfig`) with no `object`, `dynamic`, or loosely-typed fields.

`System.Text.Json` has no equivalent to Newtonsoft.Json's `TypeNameHandling`. Polymorphic type loading in STJ requires explicit opt-in via `[JsonPolymorphic]` / `[JsonDerivedType]` attributes on the target type; neither `AppConfig` nor `GameAchievementConfig` carry those attributes. A crafted `$type` field in a config file is ignored — it is treated as an unknown property and silently skipped.

Newtonsoft.Json **is** present as a transitive dependency of BizHawk, and BizHawk uses it internally to serialise emulator core settings. Those settings are written and read by the emulator itself; they are not user-editable files and are never loaded from disk paths the publisher or player controls.

---

## Network activity and telemetry

NEShim makes no outbound network connections of its own. There is no telemetry, analytics, or automatic crash reporting built into the application.

**Crash log:** When an unhandled exception occurs, NEShim writes a `crash.log` file to the game directory and shows a dialog pointing to it. This file is never read or transmitted by the application; it exists solely for the player or publisher to attach when reporting a bug.

The Steam SDK (`Steamworks.NET` / `steam_api64.dll`) communicates with the local Steam client process via Steam's IPC mechanism. Steam's own data collection — playtime tracking, achievement sync, cloud save sync — is handled by Steam and governed by [Valve's Privacy Policy](https://store.steampowered.com/privacy_agreement/). NEShim has no visibility into or control over what Steam reports to Valve.

**For Steam store privacy policy declarations:** NEShim itself collects no data. Any data collection that applies comes from Steam and is covered by Valve's policy.

---

## BizHawk integration

BizHawk is a faithful port of the NES subsystem from the BizHawk multi-system emulator. It lives in the `BizHawk/` project and is treated as a read-only dependency. Do not modify BizHawk source unless fixing a direct compatibility issue — use adapter/wrapper classes in `NEShim/Emulation/` instead.

### Upstream sync policy

BizHawk is treated as a frozen vendored dependency. There is no proactive upstream sync cadence — the NES core (MOS 6502, PPU, APU, mapper library) is decades-stable and changes minimally. A sync is warranted only in two cases:

- **Emulation accuracy**: a specific bug affecting the published game has been fixed upstream in BizHawk.
- **Security**: a vulnerability with a plausible threat model is confirmed. BizHawk's attack surface is limited to reading ROM files and save-state files from the local filesystem — there is no network exposure. A realistic exploit requires a player to intentionally load a maliciously crafted save file, which is a negligible risk for a single-game commercial release where save states are written by the emulator itself. If a fix is warranted, cherry-pick the specific commit(s) only — do not bulk-merge upstream.

**How to apply a fix:** identify the upstream BizHawk commit(s) that address the issue; apply only those changes to `BizHawk/`; run `dotnet test`; smoke-test the published game end-to-end before releasing.

Key interfaces consumed:

| Interface | How NEShim uses it |
|---|---|
| `IVideoProvider` | `GetVideoBuffer()` → raw pixel data after each frame |
| `ISoundProvider` | `GetSamplesSync()` → PCM audio samples after each frame |
| `IStatable` | `SaveStateBinary()` / `LoadStateBinary()` for save states |
| `ISaveRam` | `CloneSaveRam()` / `StoreSaveRam()` / `SaveRamModified` for battery RAM |
| `IMemoryDomains` | `domains["System Bus"]` → `MemoryDomain.PeekByte(addr)` for achievement triggers |

`EmulatorHost` resolves all interfaces from the NES core's `IEmulatorServiceProvider` at startup and exposes them as typed properties. Consumers never reference the `NES` class directly.

---

## Adding a new subsystem

1. Create a class in the appropriate namespace (see the namespace map above).
2. If it needs per-frame work, add it to `EmulationThread` — pass it through the constructor and call it in `Loop()`.
3. If it needs main-thread lifecycle work (e.g., disposal), wire it in `NEShimApp.InitializeEmulator()` and dispose in `NEShimApp.Shutdown()`.
4. Use `MarshalToMainThread` to marshal any main-thread updates from the emulation thread.
5. Write unit tests in `NEShim.Tests/` mirroring the source path. If the subsystem requires I/O, put tests in `NEShim.Tests/Integration/`.

---

## Adding a new audio processor

1. Implement `IAudioProcessor` in `NEShim/Audio/`. Constructor must accept `int sampleRate = 44100` so tests can override it.
2. Add a new value to `AudioFilterMode` in `NEShim/Audio/AudioFilterMode.cs`. Add the matching `Parse()` case and, if the name is multi-word, a `DisplayName()` case in `AudioFilterModeParser`.
3. Add the new mode to the `CreateProcessor` switch in `NEShimApp.cs`.
4. No menu changes are needed — both `SoundHandler` classes read `Enum.GetValues<AudioFilterMode>()` dynamically. The new mode appears automatically in the Audio Filter sub-screen of both the in-game pause menu and the main menu.

---

## Adding a new video filter (structural)

Both renderers share the same `VideoFilterMode` enum and menu. To support a new filter on both paths, touch both the D3D11 and SDL layers. Pre-compiled shader binaries (`.cso` and `.spv`) are checked into source control; no compiler tooling is needed on CI or when only the application code changes.

> **Shader tooling:** modifying shader source requires `fxc.exe` (Windows 10 SDK) for DXBC (`.cso`) and `dxc.exe` (`Microsoft.Direct3D.DXC` NuGet) for SPIR-V (`.spv`). Standard builds use the pre-compiled files and do not require either tool.

**Both paths (required for every new filter):**

1. Add an enum value to `VideoFilterMode` in `NEShim/Rendering/VideoFilterMode.cs`. Add `Parse()` and `DisplayName()` cases in `VideoFilterModeParser`. Append the value to `D3D11Supported` (used by the menu for both renderers).
2. Write `*.ps.hlsl` HLSL source in `NEShim/Rendering/Shaders/Dx11/`. The shader must `#include "ColorGrade.hlsli"` and call `ApplyColorGrade(color, colorMode)` as its final step. Use the 4-float `cbuffer FilterParams : register(b0)` layout.

**D3D11 path (DXBC):**

3. Create a class implementing `ID3D11Filter` in `NEShim/Rendering/Filters/Dx11/`. Provide `FilterMode`, `PixelAspectRatio`, `PixelShaderResourceName` (embedded `.cso` resource name; null → passthrough shader), and `WriteBaseParams`. Override `UseLinearSampler => true` for sampler-only filters (e.g. bilinear). Override `NotifyFrame(int)` only for animated filters (e.g. NTSC noise phase).
4. Add the case to `D3D11FilterFactory.Create()`.
5. Register in `NEShim.csproj`: add `.hlsl` as `<None>`, compiled `.cso` as `<EmbeddedResource LogicalName="...">`, and add the `<Exec>` entry in the `CompileShaders` MSBuild target.

**SDL path (SPIR-V):**

6. Compile the same HLSL to SPIR-V with `dxc.exe -spirv` targeting Vulkan binding annotations. Save the output as `*.ps.spv` in `NEShim/Rendering/Shaders/Vulkan/`.
7. Create a class implementing `ISdlFilter` in `NEShim/Rendering/Filters/SDL/`. Provide `FilterMode`, `PixelAspectRatio`, `PixelShaderResourceName` (embedded `.spv` resource name; null → no shader pass), `NumFragmentSamplers`, `NumFragmentUniformBuffers`, and `WriteUniformData(Span<float>, nesWidth, nesHeight)`.
8. Add the case to `SdlFilterFactory.Create()`.
9. Register the `.spv` in `NEShim.csproj` as `<EmbeddedResource LogicalName="...">` following the same pattern as existing SPIR-V resources.

**No menu changes are needed** — the Video Filter sub-menu reads `VideoFilterModeParser.D3D11Supported` dynamically; the new filter appears on both paths automatically.

> **Cbuffer / uniform constraint:** `b0` is fixed at 4 floats. Slots [0..2] are yours via `WriteBaseParams()` / `WriteUniformData()`; slot [3] is the colour mode and is written by the renderer. If a filter genuinely needs more than 3 configuration floats, revise this rule explicitly in `CLAUDE.md` — do not add a second constant buffer silently.

## Adding a new motion effect

Motion effects implement `IMotionEffect` and are registered in two factories (one per renderer path).

**CPU quad-offset effects** (no shader; simplest to add):

1. Implement `IMotionEffect` in `NEShim/Rendering/MotionEffects/`. Override `GetFrameOffset(FrameLayout)` to return a `(dx, dy)` clip-space offset. Override `NotifyLayout(FrameLayout)` if the amplitude should scale with viewport size.
2. Add a value to `VideoMotionEffectMode`. Add `Parse()` and `DisplayName()` cases in `VideoMotionEffectModeParser`.
3. Add cases to `MotionEffectFactory.Create()` (D3D11) and `SdlMotionEffectFactory.Create()` (SDL). Both factories return the same implementation — CPU effects have no renderer-specific code.

**Shader-backed effects** (additional render pass; requires `.hlsl` + `.spv`):

4. Additionally implement `IMotionEffect.PixelShaderResourceName` (D3D11 `.cso` name) and `WriteShaderParams(Span<float>)`. The renderer allocates an intermediate RT and a dedicated pixel shader pass when this is non-null.
5. For the SDL path, also implement `ISdlMotionEffect` (adds `SpvResourceName`, `NumFragmentSamplers`, `NumFragmentUniformBuffers`). Compile the HLSL to `.spv` and embed it.
6. Add both `.cso` and `.spv` as `<EmbeddedResource>` entries in `NEShim.csproj` following existing examples (e.g. `MagneticDistortion`).

> **PhosphorPersistence cannot be implemented on SDL_GPU.** It requires two texture samplers bound simultaneously (ping-pong temporal buffer), which cannot be expressed via `SDL_GPURenderState`. If you add a similar temporal-accumulation effect, it must be D3D11-only: return it from `MotionEffectFactory.Create()` and return `NoneMotionEffect` (with a log entry) from `SdlMotionEffectFactory.Create()`.

## Adding a new color effect

Color effects are cbuffer values consumed inside `ColorGrade.hlsli` — not separate shader files. The include is shared between the DXBC (Dx11/) and SPIR-V (Vulkan/) shader trees, so a new color mode requires updating both compiled outputs.

1. Add an enum value to `VideoColorFilterMode` in `NEShim/Rendering/VideoColorFilterMode.cs`. Add `Parse()` and `DisplayName()` cases in `VideoColorFilterModeParser`.
2. Add the corresponding branch to `ApplyColorGrade()` in `NEShim/Rendering/Shaders/Dx11/ColorGrade.hlsli`.
3. Recompile all DXBC shaders that include `ColorGrade.hlsli` (`fxc.exe`) and all SPIR-V shaders that include it (`dxc.exe -spirv`). Commit the updated `.cso` and `.spv` files.
4. No menu or renderer changes are needed — the Color Effect sub-menu reads `VideoColorFilterModeParser.AllModes` dynamically, and the renderer always passes `(float)_activeColorMode` into the uniform buffer.

---

## Key design rules

- **State machines** hold state and drive transitions; **renderers** draw. Never mix these.
- **Components communicate upward** via C# events (`Opened`, `Closed`, `NewGameChosen`, etc.). Wiring is in `NEShimApp.InitializeEmulator()`.
- **No BizHawk modifications** unless fixing a compatibility issue.
- **No magic numbers** — give all dimensions, timing constants, and UI sizes a named `const`.
- **Nullable reference types** are enabled. Use `?` annotations throughout. Avoid `!` except at genuine interop boundaries.
- **Method length** — keep methods under ~30 lines. Extract named helpers.
- **`IDisposable` discipline** — every `IDisposable` created inside a method must be in a `using` declaration. Classes that own native resources (SDL surfaces, audio streams, D3D11 objects) must implement `IDisposable` and be disposed in `NEShimApp.Shutdown()`.
