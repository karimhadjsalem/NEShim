# Linux Native DLLs

Place the following pre-built Linux x64 shared libraries here before building or publishing
for Linux. These are sourced from the BizHawk project (TASEmulators/BizHawk, Assets/dll/):

| File | Source |
|---|---|
| `libblip_buf.so` | BizHawk Linux build (TASEmulators/blip_buf, MIT) |
| `libbizhash.so` | BizHawk Linux build (BizHawk contributors, MIT) |

These files are not committed to source control (binary blobs). The Windows counterparts
(`blip_buf.dll`, `libbizhash.dll`) live in `../` (the parent `dll/` directory).

`BizHawk.csproj` copies whichever set matches the build target:
- Windows build (`$(OS) == Windows_NT` or no `-r` flag): copies `blip_buf.dll` + `libbizhash.dll`
- Linux build (`-r linux-x64` or native Linux host): copies `libblip_buf.so` + `libbizhash.so`

P/Invoke name resolution: `[DllImport("blip_buf")]` resolves to `libblip_buf.so` on Linux
because .NET probes with the `lib` prefix automatically. `[DllImport("libbizhash")]` resolves
to `libbizhash.so` directly (the `lib` prefix is already in the name).
