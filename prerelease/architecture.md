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
| `NEShim` | `net9.0-windows` | Main application — Windows Forms shell, Steam wiring, game loop |
| `NEShim.AchievementSigning` | `net9.0` | Shared library — `AchievementDef` type, ECDSA-P256 signing/verification logic |
| `NEShim.SealAchievements` | `net9.0` | Developer CLI tool — stamps ECDSA-P256 signatures onto `achievements.json` |
| `NEShim.Tests` | `net9.0-windows` | NUnit test suite |
| `BizHawk` | `net8.0` | NES emulation core, adapted from the BizHawk multi-system emulator |

`NEShim.AchievementSigning` targets `net9.0` (no Windows dependency) so it can be referenced by both the main app and the sealer tool without pulling in Windows Forms.

---

## Namespace map

| Namespace | Responsibility |
|---|---|
| `NEShim.Config` | `AppConfig` POCO + `ConfigLoader` (JSON load/save) |
| `NEShim.Emulation` | `EmulatorHost` — owns the `NES` instance, exposes its services; adapters and stubs |
| `NEShim.GameLoop` | `EmulationThread` — timing, hotkeys, pause logic, per-frame orchestration |
| `NEShim.Rendering` | `IFrameRenderer` strategy (`D3D11Renderer` primary / `GdiRenderer` fallback), `IMenuSceneProvider` pull interface, `FrameBuffer` (double-buffer), `GamePanel` (GDI+ fallback surface), `D3DOverlayHook` (Steam overlay swap chain), scalers |
| `NEShim.Audio` | `AudioPlayer` (NAudio ring-buffer bridge), audio processors, main menu music |
| `NEShim.Input` | `InputManager` (keyboard + XInput), `InputSnapshot`, `XInputHelper` |
| `NEShim.Saves` | `SaveStateManager` (8 slots + auto), `SaveRamManager` |
| `NEShim.Platform` | `PlatformDetector` — Wine/Proton detection (`ntdll.dll::wine_get_version`), SteamDeck env var, and `IsD3D11Active` (set at startup — gates D3D11-only video filter availability); `PlatformDefaults` — config-override resolution for spin window and audio latency |
| `NEShim.UI` | `InGameMenu` + `MainMenuScreen` state machines; `MenuRenderer` + `MainMenuRenderer`; `IMenuInputTarget` (gamepad dispatch interface implemented by `MainForm`) |
| `NEShim.Steam` | `SteamManager` — init, overlay callbacks, UI-thread tick; `SteamInputManager` — action sets |
| `NEShim.Achievements` | `AchievementManager` — per-frame memory watcher; `AchievementConfigLoader` |

---

## Startup sequence

```
Program.cs
  └─ Application.Run(new MainForm())
       └─ MainForm.OnFormLoad()
            └─ MainForm.InitializeEmulator()
                 1. Load config.json → AppConfig
                 2. Load ROM, compute SHA1
                 3. EmulatorHost.Load() → wraps NES core
                 4. AchievementConfigLoader.Load(romHash) → verify sigs → AchievementManager?
                 5. SaveRamManager.LoadFromDisk()
                 6. SaveStateManager
                 7. FrameBuffer + GamePanel (display surface)
                 8. InputManager + keyboard event wiring
                 9. AudioPlayer + audio processors
                 9a. MainMenuScreen + MainMenuMusic
                 10. InGameMenu
                 11. EmulationThread (starts paused at MainMenu)
                 12. SteamManager.Initialize() → overlay callback wired; UI-thread timer started (~60 Hz)
                 13. SetWindowMode() → then D3DOverlayHook.Initialize(Handle, Width, Height)
                 14. D3D11Renderer constructed (reuses device + swap chain from D3DOverlayHook, if available)
                     PlatformDetector.IsD3D11Active set accordingly
                 15. audio.Start(), emulationThread.Start()
```

All components are wired together in `MainForm.InitializeEmulator()` which owns construction, event subscription, and lifetime management. There is no dependency injection container — wiring is explicit and centralised.

---

## Thread model

NEShim uses two threads:

### UI thread (Windows Forms message pump)

- Owns all `WinForms` controls including `GamePanel`.
- Receives keyboard events (`OnKeyDown`, `OnKeyUp`) and forwards them to `InputManager`.
- Processes repaint requests (`GamePanel.OnPaint`).
- Handles `WM_ACTIVATEAPP` (focus lost → pause reason).
- `MainForm.OnFormClosing` stops the emulation thread and writes persistence files.

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
9. **Frame dispatch (non-blocking, via `BeginInvoke` to UI thread):**
   - D3D11 active: `D3D11Renderer.UploadFrame(FrontBuffer)` then `D3D11Renderer.Tick(vsync: true)` — upload and present are batched in the same `BeginInvoke` call so Present fires immediately after the texture is ready, with no clock drift.
   - GDI+ fallback: `GamePanel.UpdateFrame()` — copies pixels into Bitmap and calls Invalidate; `GdiRenderer.Tick()` — `D3DOverlayHook.Present()` for Steam overlay heartbeat.
10. `AudioPlayer.Enqueue()` — push audio samples to ring buffer
11. FPS tracking
12. Frame timing — sleep + spin to hit the target timestamp

The **steamTimer** (~60 Hz, UI thread) calls `SteamManager.Tick()` (→ `SteamAPI.RunCallbacks()`) every tick, but only calls `Renderer.Tick()` when the emulation loop is **paused**. During gameplay, Present is driven by the `BeginInvoke` batch above, keeping it tightly coupled to frame production. When paused (menus, overlay, focus lost), no `BeginInvoke` calls are arriving, so the steamTimer drives Present to keep the Steam overlay hook alive.

Steam requires callbacks to be dispatched on the same thread that called `SteamAPI.Init()`.

**Cross-thread rules:**
- The emulation thread never calls WinForms methods directly — always via `BeginInvoke`.
- `InputManager._pressedKeys` is protected by a lock (keyboard events fire on the UI thread; reads happen on the emulation thread).
- `FrameBuffer` is protected by a `SpinLock` at swap time.
- `_pauseReasonBits` is a `volatile int` updated with CAS (`Interlocked.CompareExchange`) from either thread.

---

## Pause reasons

`EmulationThread.PauseReasons` is a `[Flags]` enum. The loop blocks whenever any bit is set:

| Bit | Name | Set when | Cleared when |
|---|---|---|---|
| 1 | `Menu` | In-game pause menu opened, or controller disconnected mid-game | Menu closed / disconnect overlay dismissed |
| 2 | `Overlay` | Steam overlay opened | Overlay closed |
| 4 | `FocusLost` | Window loses focus (`WM_ACTIVATEAPP`) | Window gains focus |
| 8 | `MainMenu` | App starts / user returns to main menu | User picks New Game or Resume |
| 16 | `DeviceLost` | D3D11 device removed (GPU driver reset, suspend/resume) | D3D11 reinitialised successfully |

`SetPauseReason(reason, active)` uses a CAS loop to atomically set or clear the bit. When the result is non-zero the audio is muted and the `ManualResetEventSlim` is reset; when it reaches zero the audio is unmuted and the event is set to unblock the loop.

---

## Frame buffer (double-buffer)

```
Emulation thread          UI thread (paint)
─────────────────         ──────────────────
WriteBack(pixels)  →  [back buffer]
Swap()             ←──── SpinLock ────→  FrontBuffer (read only)
                          │
                    GamePanel.OnPaint reads FrontBuffer
```

`WriteBack` copies the NES pixel array into the back buffer. `Swap` atomically flips `_frontIndex` under a `SpinLock`. The paint thread always reads from `FrontBuffer` — it never touches the back buffer.

When the pause menu is open, the emulation loop does not run `RunFrame`, so the front buffer holds the last frame before the pause. The frozen frame is also captured into a separate `int[]` copy via `CaptureFront()` when the menu opens, so the renderer can use it as a background under the semi-transparent overlay without race conditions.

---

## State machines and renderers

Both menus follow the same two-class pattern:

| Class | Responsibility |
|---|---|
| `InGameMenu` | Owns state (`Current`, `SelectedItem`, `IsOpen`). Handles all input (keyboard, gamepad). Drives transitions. Fires events. |
| `MenuRenderer` | Stateless, `internal static`. Single entry point `Draw(Graphics, Rectangle, InGameMenu)`. Creates and disposes all GDI+ resources within the call. |
| `MainMenuScreen` | Same as `InGameMenu` but for the pre-game menu. |
| `MainMenuRenderer` | Same as `MenuRenderer` for the pre-game menu. |

**Rule:** Never put rendering logic inside a state machine. Never put state mutation inside a renderer. This separation makes both independently testable — the state machines are tested without a graphics context; the renderers are not tested (they are pure GDI+ drawing).

### Per-screen handler pattern

Each menu uses a **per-screen handler** internally (nested private classes implementing an abstract `ScreenHandler` base). Each handler owns exactly one screen's title, item list, enabled-state logic, and activation logic. The state machine dispatches to the current screen's handler via a `Dictionary<Screen, ScreenHandler>` built at construction time.

This means adding a new screen requires only: add an enum value, add a handler class, add one entry to `BuildHandlers()`. There are no parallel switch statements to keep in sync.

Handlers are nested private classes and therefore have full access to all private fields and methods of their enclosing menu class.

---

## Audio

### Ring buffer bridge

`AudioPlayer` bridges the emulation thread (producer) and the NAudio driver thread (consumer) via a `short[]` ring buffer.

- **Producer:** `EmulationThread` calls `Enqueue(samples, count)` each frame.
- **Consumer:** NAudio's driver thread calls `Read(buffer, offset, count)` to pull samples.
- **Pause:** When `SetPaused(true)` is called, `Read` fills with silence and the ring buffer is drained to prevent stale audio playing on resume. The processor state is also reset to avoid a pop from DC offset in the filter memory.

### Audio processors

`IAudioProcessor` is a single-method interface:

```csharp
(short L, short R) Process(short monoSample);
void ResetState();
```

The active processor can be swapped at runtime via `AudioPlayer.SetProcessor()`. The new processor's state is reset before it takes effect to avoid pops. Two implementations ship:

| Class | Description |
|---|---|
| `NesFilterProcessor` | Emulates the NES hardware output filter: HP@37Hz → HP@39Hz → LP@14kHz. Accurate to the real hardware. |
| `SoundScrubberProcessor` | Modified filter for warmer sound: HP@80Hz → HP@80Hz → LP@14kHz → LP@8kHz. The raised HP cutoffs tighten bass transients; the extra LP stage removes harsh square-wave harmonics. |

### Main menu music

`MainMenuMusic` plays a looping audio file with smooth 1-second fade-in and 0.5-second fade-out transitions. Volume is split into `_fadeLevel` (0–1, driven by timer) and `_masterVolume` (user-controlled). The audible output is `_fadeLevel × _masterVolume`, so master volume changes during a fade behave correctly.

Looping is handled inside `LoopingSampleProvider` (an inner class) which seeks the source back to position 0 when it is exhausted. This avoids calling `Play()` from a WaveOut callback thread, which can cause re-entrancy issues.

---

## Steam overlay

Steam's overlay DLL (`GameOverlayRenderer64.dll`) hooks `IDXGISwapChain::Present` at the vtable level — without that hook, `SteamUtils.IsOverlayEnabled()` stays `false` and Shift+Tab does nothing.

**`D3DOverlayHook`** creates a D3D11 device and swap chain bound to `MainForm.Handle`. In D3D11 mode, `D3D11Renderer.DrawAndPresent()` calls `SwapChain.Present()` every ~16 ms, which Steam intercepts. In GDI+ fallback mode, `D3DOverlayHook.Present()` serves as a minimal heartbeat.

The swap chain uses `SwapEffect.FlipDiscard` (required for DXVK on Proton — see [Proton / Steam Deck notes](#proton--steam-deck-notes) below).

### GamePanel visibility in D3D11 mode

Steam renders its overlay UI directly into the swap chain's back buffer via the vtable hook. `GamePanel` is a GDI+ child control that DWM composites *above* the swap chain surface — so Steam's overlay content would be painted over by GDI+ if GamePanel were visible.

In D3D11 mode, `GamePanel` is **permanently hidden** (`Visible = false`). All rendering — NES frames, logo splash, main menu, in-game menu, and HUD overlays — goes through `D3D11Renderer` via the swap chain. The sole exception is when the Steam overlay is active (`PauseReasons.Overlay` set): GamePanel is made briefly visible so the overlay can composite over a GDI surface, then hidden again when the overlay closes.

In GDI+ fallback mode, GamePanel stays visible at all times and handles all rendering through `OnPaint`.

### Initialisation order

`D3DOverlayHook.Initialize(Handle, Width, Height)` must be called **after** `SetWindowMode()` so the swap chain is created at the window's final dimensions. `D3D11Renderer` is constructed immediately after `D3DOverlayHook.Initialize()`. A `Form.Resize` handler calls `D3D11Renderer.Resize()` (which calls `ResizeBuffers` internally) to keep the swap chain and viewport in sync with the window.

### Proton / Steam Deck notes

DXVK is the Vulkan translation layer Proton uses for D3D11. Key behaviors:

- **`SwapEffect.FlipDiscard` is required.** The legacy `Discard` effect is emulated in DXVK via a slower blit path. `FlipDiscard` maps cleanly to Vulkan's `VK_PRESENT_MODE_FIFO_KHR`.
- **`RowPitch` alignment.** DXVK aligns texture row pitches for Vulkan buffer compatibility. `D3D11Renderer.UploadFrame` always copies row-by-row using `MappedSubresource.RowPitch`, never assuming `width × 4`.
- **Shader cache.** DXVK compiles the passthrough DXBC shaders to SPIR-V on first launch and caches them in `~/.local/share/Steam/steamapps/shadercache/<appid>/`. The passthrough shaders are trivially simple; compilation is near-instant. Subsequent launches use the cached SPIR-V with no stutter.
- **Testing on Proton requires the publish script.** Run `local-publish.ps1` before copying to a Steam Deck. `dotnet build` output omits `--self-contained` and `PublishReadyToRun`, both of which matter significantly for frame-rate on Proton. See `CLAUDE.md` for details.

### `SteamAPI.RestartAppIfNecessary`

`Program.Main` calls `SteamAPI.RestartAppIfNecessary(appId)` before `Application.Run`. It reads the App ID from `steam_appid.txt`. If the game was launched directly (not via Steam), the call returns `true` and the process exits so Steam can relaunch it with `GameOverlayRenderer64.dll` already injected into the process before any D3D device is created.

---

## D3D11 renderer and device loss

### Ownership model

`D3DOverlayHook` owns the D3D11 device and DXGI swap chain — it creates them and disposes them. `D3D11Renderer` is constructed with a reference to both and owns all other rendering objects:

| Resource | Owner |
|---|---|
| `ID3D11Device`, `IDXGISwapChain` | `D3DOverlayHook` |
| `ID3D11DeviceContext` (immediate) | retrieved from device; not disposed |
| `ID3D11Texture2D` (NES texture), SRV | `D3D11Renderer` |
| `ID3D11RenderTargetView` | `D3D11Renderer` (recreated on resize) |
| Vertex buffer, VS, PS, input layout, sampler | `D3D11Renderer` |

`D3D11Renderer.Dispose()` releases only the objects it owns. `D3DOverlayHook.Dispose()` is called after `D3D11Renderer.Dispose()` in `MainForm.OnFormClosing`.

### Device loss recovery

`D3D11Renderer.DrawAndPresent()` checks the `Present()` HRESULT for `DXGI_ERROR_DEVICE_REMOVED` (0x887A0005) and `DXGI_ERROR_DEVICE_RESET` (0x887A0007). If either occurs:

1. `DeviceLost` event fires.
2. `MainForm.OnD3DDeviceLost` handles it: sets `PauseReasons.DeviceLost`, disposes `D3D11Renderer` then `D3DOverlayHook`.
3. Recreates `D3DOverlayHook` (new device + swap chain) and `D3D11Renderer`.
4. If recreation succeeds, clears `PauseReasons.DeviceLost` to resume emulation.

Device loss is rare on desktop (typically caused by a GPU driver reset or suspend/resume cycle). On Steam Deck it is more likely during system sleep.

---

## Rendering pipeline

### D3D11 path (primary)

```
NES pixel buffer (int[256×240], 0xAARRGGBB / BGRA in little-endian memory)
  └─ FrameBuffer.WriteBack + Swap (emulation thread)
       └─ BeginInvoke (UI thread) — upload and present batched together:
            ├─ D3D11Renderer.UploadFrame
            │    └─ Map(WriteDiscard) → row-by-row copy respecting RowPitch
            └─ D3D11Renderer.Tick(vsync: true)
                 └─ DrawAndPresent
                      ├─ Draw fullscreen NES quad — passthrough VS + PS (point-clamp sampler)
                      ├─ DrawOverlay — GDI+ Bitmap (menus / logo / HUD) alpha-blended over NES frame
                      └─ SwapChain.Present(syncInterval=1) — vsync on
```

`D3DOverlayHook` creates and owns the D3D11 device and swap chain. `D3D11Renderer` reuses them (passed via constructor) and owns all other rendering resources: NES texture, overlay texture, SRV, RTV, vertex buffer, shaders, input layout, and sampler. NES pixels are `B8G8R8A8_UNorm` — no byte-swapping needed.

**D3D11 renders everything** — not just the NES frame. The logo splash, main menu, in-game menu, toasts, achievement banners, and FPS overlay are all composited by `D3D11Renderer` via an overlay texture pipeline. `MainForm` implements `IMenuSceneProvider`, returning a paint delegate for whichever scene is active (or `null` during pure gameplay — zero overhead on the hot path). `GamePanel` is permanently hidden in D3D11 mode and plays no role in rendering.

### GDI+ path (fallback)

Used when D3D11 initialisation fails (no GPU, driver error).

```
NES pixel buffer (int[256×240], ARGB)
  └─ FrameBuffer.WriteBack + Swap
       └─ BeginInvoke → GamePanel.UpdateFrame (UI thread)
            └─ bitmap.LockBits → Marshal.Copy pixels into Bitmap
                 └─ GamePanel.OnPaint
                      ├─ If main menu visible → MainMenuRenderer.Draw()
                      ├─ Compute letterbox rect (8:7 pixel aspect ratio)
                      ├─ Draw sidebar images (optional)
                      ├─ IGraphicsScaler.Configure(g) — set interpolation mode
                      ├─ g.DrawImage(bitmap → letterboxed rect)
                      ├─ If pause menu open → MenuRenderer.Draw() overlay
                      ├─ Toast notification (if active)
                      ├─ Achievement notification (if active, 5-second banner)
                      └─ FPS overlay (if enabled)
```

**Aspect ratio:** The NES outputs 256×240 pixels, but NES pixels are not square — the display aspect ratio is `256 × (8/7) : 240 ≈ 8:7 → 1.212`. `GamePanel` computes a letterboxed destination rectangle that fills the window while preserving this ratio, producing black (or artwork) bars on the sides for widescreen displays.

**Scalers** (`IGraphicsScaler`) configure GDI+ interpolation mode before `DrawImage` (GDI+ fallback only):
- `NearestNeighborScaler` — pixel-perfect, no blur.
- `BilinearScaler` — smooth scaling for a softer look.

In D3D11 mode, the equivalent of point-clamp nearest-neighbour scaling is the `Filter.MinMagMipPoint` + `TextureAddressMode.Clamp` sampler in `D3D11Renderer`.

### Renderer mode flag

`PlatformDetector.IsD3D11Active` is set once at startup after `D3D11Renderer` is constructed:
- `true` — D3D11 device available; `D3D11Renderer` is the active frame renderer.
- `false` — D3D11 unavailable; GDI+ path is active.

All 2.0+ video filters (CRT scanlines, palette shaders, etc.) are D3D11-only. Before any menu offers a filter option, gate on `PlatformDetector.IsD3D11Active`. The GDI+ fallback intentionally has no filter support.

---

## Save system

### Save states

`SaveStateManager` wraps BizHawk's `IStatable` interface:

- **8 named slots** stored as `slot{n}.state` (binary) + `slot{n}.meta` (JSON timestamp).
- **Auto-save** stored as `autosave.state`. Written when the in-game menu opens, every ~5 minutes during active play (18,000-frame counter in `EmulationThread.Loop`), and on graceful exit — never while the pre-game main menu is showing.
- `ActiveSlot` is persisted to `config.json` on exit.

BizHawk's `IStatable` serialises the full emulator state (CPU registers, RAM, PPU, APU, mapper) to a `BinaryWriter`. Restoring from a state is immediate and cycle-accurate.

### Battery RAM

`SaveRamManager` wraps `ISaveRam`:

- `LoadFromDisk()` is called at startup, before the first frame. If no `.srm` file exists, the emulator starts with uninitialised save RAM (same as a fresh cartridge).
- `SaveToDisk()` is called on exit. It only writes the file if `ISaveRam.SaveRamModified` is true, avoiding unnecessary disk writes.

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
3. If it needs UI-thread lifecycle work (e.g., disposal), wire it in `MainForm.InitializeEmulator()` and dispose in `OnFormClosing`.
4. Use `BeginInvoke` to marshal any UI updates to the UI thread from the emulation thread.
5. Write unit tests in `NEShim.Tests/` mirroring the source path. If the subsystem requires I/O, put tests in `NEShim.Tests/Integration/`.

---

## Adding a new audio processor

1. Implement `IAudioProcessor` in `NEShim/Audio/`.
2. Instantiate it in `MainForm` alongside the existing processors (they are kept alive for zero-allocation runtime swaps).
3. Wire the toggle to `AppConfig`, the in-game Sound menu, and the main menu Sound screen.
4. Call `AudioPlayer.SetProcessor(newProcessor)` in the relevant toggle callback.

---

## Key design rules

- **State machines** hold state and drive transitions; **renderers** draw. Never mix these.
- **Components communicate upward** via C# events (`Opened`, `Closed`, `NewGameChosen`, etc.). Wiring is in `MainForm.InitializeEmulator()`.
- **No BizHawk modifications** unless fixing a compatibility issue.
- **No magic numbers** — give all dimensions, timing constants, and UI sizes a named `const`.
- **Nullable reference types** are enabled. Use `?` annotations throughout. Avoid `!` except at genuine interop boundaries.
- **Method length** — keep methods under ~30 lines. Extract named helpers.
- **`IDisposable` discipline** — every `IDisposable` created inside a method must be in a `using` declaration. Classes that own `Bitmap`, audio, or host resources must implement `IDisposable`.
