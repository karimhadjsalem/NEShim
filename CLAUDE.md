# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

NEShim is a Windows application that wraps the BizHawk NES emulation core to allow integration with external SDKs (e.g., Steamworks). The BizHawk emulation code lives in `BizHawk/` and was adapted from the BizHawk multi-system emulator — it is the authoritative NES emulation layer and generally should not be modified unless fixing compatibility issues. The BizHawk code is treated as a frozen vendored dependency with no proactive upstream sync cadence — the NES core is decades-stable. Sync only when a specific emulation accuracy bug or confirmed security issue warrants it; cherry-pick the specific fix, do not bulk-merge.

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

The main application targets `net9.0-windows` and requires Windows (uses Windows Forms). The BizHawk library targets `net8.0`.

### Testing on Proton / Steam Deck — use the publish script, not `dotnet build`

**Do not use `dotnet build` output for performance testing on Proton or Steam Deck.** The difference in emulation performance between a local build and a published build is significant and has caused real confusion (local builds appeared to have framerate problems that the release did not).

Two build flags in `local-publish.ps1` are responsible:

- **`--self-contained true`** — bundles the exact .NET 9 runtime the app was built against. A framework-dependent `dotnet build` output relies on whatever wine-mono or dotnet-wine provides, which may be a different version with different GC and thread scheduler behavior.
- **`-p:PublishReadyToRun=true`** — pre-compiles managed IL to native x64 code at build time, eliminating JIT work at runtime. On Proton/Wine, JIT is expensive because every JIT code-generation step calls `VirtualAlloc`/`VirtualProtect`, which Wine must intercept and translate. Without ReadyToRun, these calls happen on first entry to each method, causing frame spikes whenever a new code path is hit (ROM load, menu open, achievement unlock, etc.).

When testing on Proton or Steam Deck, always run `local-publish.ps1` and copy that output to the device. A raw `dotnet build` output is only valid for iterating on logic and running tests on Windows.

## Project Structure

```
NEShim.sln
NEShim/                    — Windows Forms GUI (entry point: Program.cs → MainForm.cs)
NEShim.Tests/              — NUnit test project
NEShim.AchievementSigning/ — ECDSA-P256 signing library (AchievementSigner, AchievementDef)
NEShim.SealAchievements/   — CLI tool for stamping achievement signatures (seal-achievements)
BizHawk/                   — NES emulation core (adapted from BizHawk emulator)
ref/                       — Reference binaries (BizHawk, Nintaco, tools) — not compiled
```

## Architecture

### NEShim Application Layer (`NEShim/`)
A thin Windows Forms shell. `MainForm.cs` owns startup, wires all components together, and handles the window lifecycle. The intent is for this layer to own SDK integrations (Steamworks, etc.) while delegating all emulation to BizHawk.

Key subsystems and their responsibilities:

| Namespace | Responsibility |
|---|---|
| `NEShim.Config` | POCO config model + JSON load/save |
| `NEShim.Emulation` | BizHawk bridge (`EmulatorHost`), controller adapter, stubs |
| `NEShim.GameLoop` | `EmulationThread` — timing, hotkeys, pause logic |
| `NEShim.Rendering` | `IFrameRenderer` strategy (`D3D11Renderer` primary / `GdiRenderer` fallback), `IMenuSceneProvider` pull interface, `FrameBuffer` (double-buffer), `GamePanel` (GDI+ fallback surface), `D3DOverlayHook` (Steam overlay swap chain), scalers |
| `NEShim.Audio` | NAudio ring-buffer bridge (`AudioPlayer`) |
| `NEShim.Input` | `InputManager` (keyboard + XInput), `InputSnapshot` |
| `NEShim.Saves` | `SaveStateManager` (8 slots + auto), `SaveRamManager` |
| `NEShim.Achievements` | `AchievementConfigLoader` — parses and signature-verifies `achievements.json`; `AchievementManager` — per-frame memory-watch evaluation and Steam unlock |
| `NEShim.Localization` | `LocalizationData` — POCO with all UI strings and font family; `LocalizationLoader` — loads `lang/<language>.json` with English fallback |
| `NEShim.UI` | `InGameMenu`, `MainMenuScreen` state machines + stateless renderers; `IMenuInputTarget` (gamepad dispatch interface implemented by `MainForm`) |
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

**State machine** — `InGameMenu` and `MainMenuScreen` each own a `Screen` enum and dispatch to a per-screen `ScreenHandler` (nested private class). Each handler encapsulates one screen's title, items, enabled-state logic, and activation logic. Adding a new screen requires: add an enum value, add a handler class, add one entry to `BuildHandlers()`. Rendering is always delegated to a paired stateless `*Renderer` class that takes the state object as a read-only parameter. Never put rendering logic inside a state machine, and never put state mutation inside a renderer.

**Observer (events)** — Components communicate upward via C# events (`NewGameChosen`, `ResumeChosen`, `Opened`, `Closed`). Wiring is done in `MainForm.InitializeEmulator()`, keeping components decoupled.

**Double-buffer** — `FrameBuffer` keeps a back buffer (emulation thread writes) and a front buffer (paint thread reads). `Swap()` atomically exchanges them under a `SpinLock`. Never read from the back buffer on the paint thread or write to the front buffer on the emulation thread.

**Pause flags** — `EmulationThread.PauseReasons` is a `[Flags]` enum. Any non-zero value blocks the loop on a `ManualResetEventSlim`. Always use `SetPauseReason(reason, active)` rather than setting bits directly — it handles the CAS loop and the audio mute side-effect.

**Stateless renderer** — `MainMenuRenderer` and `MenuRenderer` are `internal static` classes with a single `Draw(Graphics, Rectangle, <StateType>)` entry point. They create and dispose all GDI+ resources within the call. Do not cache brushes, pens, or fonts across calls in these classes.

**Pull scene interface (`IMenuSceneProvider`)** — `D3D11Renderer` does not know which UI scene (logo, main menu, in-game menu) is active. Instead it calls `IMenuSceneProvider.GetActiveScenePainter()` each frame. `MainForm` implements this: it returns a paint delegate (`Action<Graphics, Rectangle>`) for whichever scene is current, or `null` during gameplay (zero overhead on the hot path). The delegate is invoked inside `RenderOverlayBitmap()` before the FPS/toast/achievement overlays, so the scene is always painted under the HUD. Never add scene-detection logic to `D3D11Renderer` itself.

**Menu input interface (`IMenuInputTarget`)** — `EmulationThread` dispatches gamepad navigation without knowing about `GamePanel` or `MainForm` directly. It calls `IMenuInputTarget.IsWaitingForGamepadButton`, `HandleGamepadNav`, and `HandleGamepadButtonPress`. `MainForm` implements this interface and routes calls to whichever state machine is active. All implementations are explicit (e.g., `void UI.IMenuInputTarget.HandleGamepadNav(...)`) to avoid CS0051 accessibility issues caused by the internal `MenuNavInput` type appearing in a method signature.

### D3D11 rendering and Steam overlay architecture

`D3DOverlayHook` creates a D3D11 device and swap chain (using `SwapEffect.FlipDiscard` — required for DXVK on Proton) bound to `MainForm.Handle`. `RendererFactory.Create` tries to construct a `D3D11Renderer` first; if the hook device or swap chain is null, or the constructor throws, it falls back to `GdiRenderer`. `PlatformDetector.IsD3D11Active` is set once at startup to reflect which path was taken.

**D3D11 is the primary path for all rendering** — not just NES frames. The logo splash, main menu, in-game menu, and all HUD overlays (FPS, toasts, achievements) are all composed by `D3D11Renderer`. There is no GDI+ fallback within D3D11 mode; the GDI+ path (`GdiRenderer`) is a complete self-contained alternative that takes over entirely when D3D11 is unavailable.

**Frame delivery:** The emulation thread batches `UploadFrame` and `Tick` together in a single `BeginInvoke` on `MainForm`, so Present fires immediately after the texture is ready — tightly coupled to emulation timing with no clock drift. The `_steamTimer` (~60 Hz, UI thread) calls `SteamManager.Tick()` every tick but only calls `Renderer.Tick` when the emulation loop is paused, keeping the Steam overlay hook alive without a running emulation loop to supply `BeginInvoke` calls.

**Overlay texture pipeline:** `D3D11Renderer` maintains a separate BGRA overlay texture the same size as the swap chain. `DrawOverlay()` checks whether any scene (via `IMenuSceneProvider.GetActiveScenePainter()`) or transient HUD element (FPS, toast, achievement) is active. If so, it calls `RenderOverlayBitmap()` — which paints GDI+ content into a CPU-side `Bitmap` (scene first, HUD on top), uploads it, and alpha-blends the overlay quad over the NES frame. `MarkOverlayDirty()` forces an overlay repaint on the next `DrawAndPresent` tick (called from `MainForm` whenever menu state changes). The scene painter is always re-invoked each frame while a scene is active, since cursor movement does not call `MarkOverlayDirty`.

**Pixel format:** BizHawk's `IVideoProvider` returns `int[]` where each int is `0xAARRGGBB`. In little-endian memory the bytes are `[B, G, R, A]` — BGRA — which maps directly to `B8G8R8A8_UNorm` with no byte-swapping. Row copy in `UploadFrame` always uses `MappedSubresource.RowPitch` (DXVK may align rows wider than `width × 4`).

**`GamePanel` in D3D11 mode:** `GamePanel` is permanently hidden (`Visible = false`) in D3D11 mode, including when the Steam overlay is active. Steam's overlay hooks `IDXGISwapChain::Present` and composites itself directly into the swap chain buffer; a visible GDI child window (GamePanel) would sit above the swap chain in DWM's Z-order and cover the overlay, which is why it must stay hidden. Menus, the logo, and the NES frame are all rendered by `D3D11Renderer` — `GamePanel` is not involved. `GamePanel.SetMenu`/`SetMainMenu`/`SetLogoScreen` calls from `MainForm` are harmless (the panel just caches references it never draws).

**GDI+ fallback (`GdiRenderer`):** When D3D11 initialisation fails, `GdiRenderer` owns all rendering through `GamePanel.OnPaint`. `SetMenuSceneProvider` and `MarkOverlayDirty` are no-ops on `GdiRenderer`; menu/logo paint is triggered by `_gamePanel.Invalidate()` instead, and `GamePanel.OnPaint` dispatches to the same stateless renderer classes (`MenuRenderer`, `MainMenuRenderer`, `LogoRenderer`). `GdiRenderer.OwnsFrameSurface = false` causes `UpdateGamePanelVisibility()` to return early, leaving `GamePanel` visible at all times.

**No mouse input:** Mouse navigation has been removed from all menus in both rendering paths. `Cursor.Hide()` is called once at `MainForm` startup and the cursor is never shown again. There are no `HandleMouseMove` or `HandleMouseClick` methods on `InGameMenu`, `MainMenuScreen`, or `GamePanel`. All menu navigation is keyboard or gamepad only.

**`EmulationThread` decoupling:** `EmulationThread` holds no reference to `GamePanel`. It takes a `Control _uiMarshal` (for `BeginInvoke` — `MainForm` is passed) and an `IMenuInputTarget _menuInput` (for gamepad dispatch — also `MainForm`). `MainForm` implements `IMenuInputTarget` explicitly to avoid CS0051.

**Device loss recovery:** `D3D11Renderer.DrawAndPresent()` fires `DeviceLost` on `DXGI_ERROR_DEVICE_REMOVED/RESET`. `MainForm` handles it by setting `PauseReasons.DeviceLost`, disposing both renderer and hook, recreating them (calling `_renderer.SetMenuSceneProvider(this)` again on the new renderer), then clearing the pause reason.

`D3DOverlayHook` must be initialised **after** `SetWindowMode` so the swap chain dimensions match the final window size. `D3D11Renderer` is constructed immediately after. A `Form.Resize` handler calls `D3D11Renderer.Resize()` which calls `ResizeBuffers` and recreates the RTV.

**`PlatformDetector.IsD3D11Active`** is set once at startup after `D3D11Renderer` is (or isn't) constructed. All 2.0+ video filters (CRT, palette shaders) are D3D11-only and must gate on this flag before offering themselves in any menu. The GDI+ fallback path has no filter support.

**`forceRenderer` config flag** — developer option, not exposed in any menu. `"auto"` (default) tries D3D11 and falls back to GDI+. `"gdi"` skips D3D11 entirely, useful when isolating D3D11-specific rendering bugs. `"d3d11"` is equivalent to `"auto"` (D3D11 is already preferred); it documents intent but still falls back to GDI+ if D3D11 init throws.

**Slow-frame timing log:** When `enableLogging` is true, `EmulationThread` logs any frame whose total work time exceeds 14 ms (2.67 ms below the 16.67 ms budget). Each log entry breaks down time into `input`, `runFrame`, `video`, and `audio` segments. This is the first place to look when investigating FPS regressions. Note: in a Debug build, `runFrame` typically takes 17–30 ms due to unoptimised BizHawk JIT output — this is expected and is not a bug. Always use a Release build for performance testing.

**Proton/DXVK notes:**
- `SwapEffect.FlipDiscard` is required — legacy `Discard` is emulated via a slower blit path in DXVK.
- `UploadFrame` copies row-by-row using `RowPitch` — DXVK aligns texture rows for Vulkan compatibility.
- DXBC passthrough shaders compile to SPIR-V on first Proton launch (cached in Steam shader cache); near-instant due to trivial shader complexity.
- Use `local-publish.ps1`, not raw `dotnet build`, for Proton performance testing.

`SteamAPI.RestartAppIfNecessary(appId)` is called in `Program.Main` before `Application.Run`. It reads the App ID from `steam_appid.txt`. If the game was not launched via Steam, the call returns `true` and the process exits so Steam can relaunch it with the overlay DLL already injected.

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
- **Dispose discipline**: Every `IDisposable` created inside a method must be in a `using` declaration or `using` block. Classes that own `Bitmap`, audio, or host resources must implement `IDisposable` and be disposed in `MainForm.OnFormClosing`.
- **Thread safety**: Emulation thread and UI thread share `_pauseReasonBits` (volatile `int` + CAS) and `FrameBuffer` (SpinLock). All other mutable state is owned by one thread. Use `BeginInvoke` to marshal work to the UI thread; never call WinForms methods directly from the emulation thread.
- **One class per file**: each top-level class, interface, enum, or record gets its own `.cs` file named after the type. Acceptable exceptions are small private helper types that are tightly coupled to a single containing class (e.g., a `ScreenHandler` subclass nested inside a state machine) — these may stay in the same file as their owner. Do not add a second public or internal top-level type to an existing file.
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

## Publishing Checklist

Before building a release for a specific game:

- **Window title**: set `WindowTitle` in `config.json` to the game's name.
- **Language**: `lang/*.json` files ship alongside the exe. The active language is read from Steam at startup; set `language` in `config.json` as a fallback for non-Steam launches. Nine languages are built in (english, french, german, spanish, japanese, korean, russian, schinese, portuguese). Add translated achievement names in the Steamworks dashboard — the unlock notification pulls the display name from Steam automatically.
- **Exe icon**: set `<ApplicationIcon>path/to/icon.ico</ApplicationIcon>` in `NEShim/NEShim.csproj` and place a valid `.ico` file at that path. This controls the icon shown in Windows Explorer, the taskbar, alt-tab, and Steam. Do not attempt to configure the icon at runtime — only the compile-time embedded icon affects the exe's file icon and Steam library entry.
- **Signing keypair**: run `seal-achievements --gen-keypair` once per game. Set the printed public key as `achievementPublicKey` in `config.json` (pre-built release) or in `AchievementSigner.EmbeddedPublicKeyBase64` and rebuild (source build). Store the private key outside source control. Achievements are disabled until a key is configured — there is no shipped default.
- **Achievements**: edit `achievements.json`, then run `seal-achievements --key-file private_key.txt achievements.json` to stamp ECDSA-P256 signatures. Re-seal any time a definition changes.
- **steam_appid.txt**: the file in the output directory must contain the real Steam App ID (not `0`). `SteamAPI.RestartAppIfNecessary` and `SteamAPI.Init` both read this file. During development the source-tree copy contains `0` (skips restart, still inits if Steam is running); the publish pipeline must replace it with the real ID.
- **steam_api64.dll**: not included in the repository. After `dotnet publish`, copy `steam_api64.dll` from the [Steamworks.NET GitHub release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe. Use the copy bundled with the wrapper (matched version); do not pull it from the Steamworks SDK partner dashboard separately.

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
