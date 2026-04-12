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
A thin Windows Forms shell. `MainForm.cs` renders a full-screen menu (Start Game, Options, Exit). The intent is for this layer to own SDK integrations (Steamworks, etc.) while delegating all emulation to BizHawk.

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

### Key BizHawk Dependencies
- `CommunityToolkit.HighPerformance` — SIMD/span performance helpers
- `Newtonsoft.Json` — settings serialization
- BizHawk.Analyzer.dll (in `ReflectionCache/`) — custom Roslyn analyzer, do not remove
