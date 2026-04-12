# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

NEShim is a Windows application that wraps the BizHawk NES emulation core to allow integration with external SDKs (e.g., Steamworks). The BizHawk emulation code lives in `BizHawk/` and was adapted from the BizHawk multi-system emulator — it is the authoritative NES emulation layer and generally should not be modified unless fixing compatibility issues.

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
NEShim/           — Windows Forms GUI (entry point: Program.cs → MainForm.cs)
NEShim.Tests/     — NUnit test project
BizHawk/          — NES emulation core (adapted from BizHawk emulator)
ReflectionCache/  — Prebuilt source generator / analyzer DLLs used by BizHawk
ref/              — Reference binaries (BizHawk, Nintaco, tools) — not compiled
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
| `NEShim.Rendering` | `FrameBuffer` (double-buffer), `GamePanel` (WinForms display) |
| `NEShim.Audio` | NAudio ring-buffer bridge (`AudioPlayer`) |
| `NEShim.Input` | `InputManager` (keyboard + XInput), `InputSnapshot` |
| `NEShim.Saves` | `SaveStateManager` (8 slots + auto), `SaveRamManager` |
| `NEShim.UI` | `InGameMenu`, `MainMenuScreen` state machines + stateless renderers |
| `NEShim.Steam` | `SteamManager` — init, overlay callbacks, per-frame tick |

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

### Coding conventions (NEShim layer only)

- **Naming**: `PascalCase` for types, methods, properties, and events; `_camelCase` for private fields; `camelCase` for locals and parameters.
- **Nullability**: Enable nullable reference types (`<Nullable>enable</Nullable>`). Use `?` annotations throughout. Avoid `!` (null-forgiving) except at genuine interop boundaries.
- **Method length**: Keep methods under ~30 lines. Extract named helpers rather than adding comments that describe blocks.
- **One responsibility per class**: State machines hold state and drive transitions; renderers draw; managers own lifecycle and I/O. Do not mix these.
- **No magic numbers**: Give all frame dimensions, timing constants, and UI sizes a named `const` or `static readonly` in the class that owns them.
- **Dispose discipline**: Every `IDisposable` created inside a method must be in a `using` declaration or `using` block. Classes that own `Bitmap`, audio, or host resources must implement `IDisposable` and be disposed in `MainForm.OnFormClosing`.
- **Thread safety**: Emulation thread and UI thread share `_pauseReasonBits` (volatile `int` + CAS) and `FrameBuffer` (SpinLock). All other mutable state is owned by one thread. Use `BeginInvoke` to marshal work to the UI thread; never call WinForms methods directly from the emulation thread.
- **Don't modify BizHawk source** unless fixing a direct compatibility issue. Prefer adapter/wrapper classes in `NEShim/Emulation/` to bridge BizHawk interfaces.

### Key BizHawk Dependencies
- `CommunityToolkit.HighPerformance` — SIMD/span performance helpers
- `Newtonsoft.Json` — settings serialization
- BizHawk.Analyzer.dll (in `ReflectionCache/`) — custom Roslyn analyzer, do not remove

## License Policy

This project is licensed **Apache 2.0** and is intended for commercial distribution via Steam. Every dependency compiled into the shipped binary must be compatible with commercial closed-source distribution.

### Permitted licenses for new dependencies
MIT, Apache 2.0, BSD 2-Clause, BSD 3-Clause, ISC, Unlicense/Public Domain. All current compiled dependencies already fall in this set:

| Package | License |
|---|---|
| BizHawk source (adapted) | MIT |
| NAudio | MIT |
| Steamworks.NET | MIT |
| CommunityToolkit.HighPerformance | MIT |
| Newtonsoft.Json | MIT |

### Prohibited licenses — do not add
- **GPL v1/v2/v3** — copyleft infects the entire binary; incompatible with commercial distribution
- **LGPL** — dynamic linking exception is ambiguous under .NET's AOT/bundling; avoid unless you have confirmed it can be safely isolated as a separate DLL that ships unmodified
- **AGPL** — network-service copyleft; prohibited
- **SSPL, BUSL, Commons Clause** — source-available but not commercially distributable
- **CC BY-NC / CC BY-SA** — non-commercial or share-alike restrictions

When evaluating a new NuGet package, check its repository license **and** the licenses of its transitive dependencies (`dotnet list package --include-transitive`).

### `ref/` directory warning
The `ref/` folder contains development-only reference tools that are **never compiled into the project**. Two of them are GPL-licensed:
- `ref/NClass_v2.04_bin/` — GPL v2 (UML diagramming tool)
- `ref/doxygen/` — GPL (documentation generator)

Do not copy code from these directories into `NEShim/` or `BizHawk/`.

### Steamworks SDK
The underlying Steamworks C++ SDK (wrapped by `Steamworks.NET`) is governed by the [Valve Steamworks SDK license](https://partner.steamgames.com/documentation/sdk_access_agreement). Key constraint: the SDK may only be used to distribute software through the Steam platform. This is separate from, and in addition to, the code license requirements above.
