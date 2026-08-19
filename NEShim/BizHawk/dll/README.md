# BizHawk Native Libraries

Native libraries required by P/Invoke in the emulation core (`LibBizHash`, `BlipBuffer`), laid out
per-RID as `dll/<rid>/<file>` — the same convention .NET itself uses for `runtimes/<rid>/native/`.
`BizHawk.csproj` copies whichever set matches the build target, keyed off `$(RuntimeIdentifier)`
(falling back to the host OS only when building with no explicit `-r`, e.g. plain `dotnet build`).

| RID | Files | Source |
|---|---|---|
| `dll/win-x64/` | `blip_buf.dll`, `libbizhash.dll` | BizHawk Windows build (TASEmulators/blip_buf; BizHawk contributors), MIT |
| `dll/linux-x64/` | `libblip_buf.so`, `libbizhash.so` | BizHawk Linux build (TASEmulators/blip_buf; BizHawk contributors), MIT |

Both sets are committed to source control (small, stable binaries, MIT-licensed — see
`THIRD-PARTY-NOTICES.md`). There is currently no macOS native build for these two libraries.

P/Invoke name resolution: `[DllImport("blip_buf")]` resolves to `blip_buf.dll` on Windows and
`libblip_buf.so` on Linux (.NET probes with the `lib` prefix automatically on Linux).
`[DllImport("libbizhash")]` resolves to `libbizhash.dll` / `libbizhash.so` directly (the `lib`
prefix is already in the name, so no extra probing is needed on either platform).
