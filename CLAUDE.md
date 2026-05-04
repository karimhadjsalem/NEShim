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
| `NEShim.Rendering` | `FrameBuffer` (double-buffer), `GamePanel` (WinForms display), `D3DOverlayHook` (Steam overlay surface) |
| `NEShim.Audio` | NAudio ring-buffer bridge (`AudioPlayer`) |
| `NEShim.Input` | `InputManager` (keyboard + XInput), `InputSnapshot` |
| `NEShim.Saves` | `SaveStateManager` (8 slots + auto), `SaveRamManager` |
| `NEShim.Achievements` | `AchievementConfigLoader` — parses and signature-verifies `achievements.json`; `AchievementManager` — per-frame memory-watch evaluation and Steam unlock |
| `NEShim.UI` | `InGameMenu`, `MainMenuScreen` state machines + stateless renderers |
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

**State machine** — `InGameMenu` and `MainMenuScreen` each own a `CurrentScreen` enum and an `Activate()` method that drives transitions. Rendering is always delegated to a paired stateless `*Renderer` class that takes the state object as a read-only parameter. Never put rendering logic inside a state machine, and never put state mutation inside a renderer.

**Observer (events)** — Components communicate upward via C# events (`NewGameChosen`, `ResumeChosen`, `Opened`, `Closed`). Wiring is done in `MainForm.InitializeEmulator()`, keeping components decoupled.

**Double-buffer** — `FrameBuffer` keeps a back buffer (emulation thread writes) and a front buffer (paint thread reads). `Swap()` atomically exchanges them under a `SpinLock`. Never read from the back buffer on the paint thread or write to the front buffer on the emulation thread.

**Pause flags** — `EmulationThread.PauseReasons` is a `[Flags]` enum. Any non-zero value blocks the loop on a `ManualResetEventSlim`. Always use `SetPauseReason(reason, active)` rather than setting bits directly — it handles the CAS loop and the audio mute side-effect.

**Stateless renderer** — `MainMenuRenderer` and `MenuRenderer` are `internal static` classes with a single `Draw(Graphics, Rectangle, <StateType>)` entry point. They create and dispose all GDI+ resources within the call. Do not cache brushes, pens, or fonts across calls in these classes.

### Steam overlay architecture

The Steam overlay requires a D3D/OpenGL `Present()` call to hook — pure GDI+ apps have none, so `D3DOverlayHook` creates a minimal D3D11 swap chain bound to `MainForm.Handle`. `SteamManager.Tick()` calls `SteamAPI.RunCallbacks()` followed by `D3DOverlayHook.Present()` from a `System.Windows.Forms.Timer` (~60 Hz) on the UI thread. Steam must be initialised and ticked on the same thread.

When the overlay activates (`GameOverlayActivated_t`), `GamePanel` is hidden so DWM exposes the swap chain surface. Steam's `GameOverlayRenderer64.dll` renders the overlay UI directly into the swap chain's back buffer via a vtable hook on `IDXGISwapChain::Present`. Without hiding `GamePanel`, the GDI+ child is composited above the swap chain by DWM and the overlay is invisible.

`D3DOverlayHook` must be initialised **after** `SetWindowMode` so the swap chain dimensions match the final window size. A `Form.Resize` handler calls `D3DOverlayHook.Resize` to keep the swap chain size correct when the user toggles windowed/fullscreen.

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
