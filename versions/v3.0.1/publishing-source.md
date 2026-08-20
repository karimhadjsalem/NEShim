---
layout: default
title: Building from source
parent: Publishing
grand_parent: v3.0.1
nav_order: 2
description: "Build a custom NEShim binary for your game — custom icon, baked-in signing key, and full assembly rename."
---

# Building from source

Use this path if you have the NEShim source and want to build your own binary. Required for a custom embedded exe icon, renaming the underlying assembly, or baking the signing public key into the binary.

---

## 1. Set the window title

In `config.json`, set `windowTitle` to your game's name:

```json
{
  "windowTitle": "My Game Title"
}
```

---

## 2. Set the executable icon

Replace `NEShim/NEShim/icon.ico` with your game's icon. No project file changes are required — the csproj already references this file for both the embedded exe icon and the runtime window icon.

The `.ico` file must contain at minimum: 16×16, 32×32, 48×48, and 256×256. Most icon editors export all sizes in one pass.

### Icon behaviour

| Context | Mechanism | When it applies |
|---|---|---|
| Windows Explorer file icon, Steam library | Win32 resource embedded in the exe at publish time | `dotnet publish` (self-contained) builds only |
| Taskbar, title bar, alt-tab thumbnail | Icon loaded from the managed embedded resource and set on the SDL window at startup | All builds, including debug |

Both are driven from the same `icon.ico` file. In debug builds the exe file in Explorer will still show a generic icon, but the running application's taskbar and window icon will show your artwork.

---

## 3. Rename the assembly (optional)

By default the output binary is `NEShim.exe` / `NEShim.dll`. If you want everything to appear under your game's name, change `<AssemblyName>` in `NEShim/NEShim/NEShim.csproj`:

```xml
<PropertyGroup>
  <AssemblyName>MyGame</AssemblyName>
</PropertyGroup>
```

Rebuild after this change. The publish output will contain `MyGame.exe`, `MyGame.dll`, `MyGame.deps.json`, and `MyGame.runtimeconfig.json`. All four names are derived from `<AssemblyName>` at build time — do not rename them individually after the build.

If you skip this step, only `NEShim.exe` can be renamed manually (see [Deployed file layout](#deployed-file-layout)).

---

## 4. Configure Steam App ID

1. Register your game in the Steamworks partner dashboard and obtain your App ID.
2. Replace the contents of `NEShim/NEShim/steam_appid.txt` with your App ID — a plain integer, no trailing newline:

```
1234560
```

This file is copied to the output directory at build time. During development it lets the game connect to Steam without going through the Steam client's launch process.

---

## 5. Steamworks native library

The Steamworks native library is **not** stored in the repository (Valve SDK license). After `dotnet publish`, copy it from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into each platform's output directory alongside the exe. Use the copy bundled with the wrapper — it is matched to the wrapper version and must not be sourced separately from the Steamworks SDK partner dashboard. The current build targets **Steamworks.NET 2025.163.0**.

| Platform | File |
|---|---|
| Windows (`-r win-x64`) | `steam_api64.dll` |
| Linux (`-r linux-x64`) | `libsteam_api.so`, **renamed to `libsteam_api64.so`** |

**Linux naming gotcha:** the zip's Linux redistributable is named `libsteam_api.so`, but the Steamworks.NET wrapper's `DllImport` targets are compiled against the literal name `steam_api64`, which .NET's Linux native-library resolution maps to `libsteam_api64.so`. Deploying the file under its zip-default name causes `SteamAPI.Init()` to throw internally (native library not found); `SteamManager.Initialize` catches this, logs it, and disables Steam entirely — the app keeps running with no crash, but achievements, overlay, and DLC ownership checks are all silently unavailable. Rename it before including it in your Linux depot.

Include the appropriate file in each platform's Steam depot; Valve does not inject it automatically. Once it is in your depot, Steam distributes it to players as part of the normal game install.

If you ever need to upgrade Steamworks.NET, replace `lib/Steamworks.NET.dll` with the new version from the [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) and supply the matching native library from the same zip at packaging time — they must be kept in sync.

---

## 6. Configure Steam Auto-Cloud

NEShim reads and writes save files to the local filesystem only. Cloud sync is handled entirely by **Steam Auto-Cloud** configured in the Steamworks partner dashboard — no code changes are required.

### Files to sync

| Path pattern | Contents |
|---|---|
| `saves\*` | Manual save states (`slot0.state` … `slot7.state`), slot metadata (`.meta`), and the auto-save (`autosave.state`) |
| `game.srm` | Battery-backed RAM — the cartridge save for games like Zelda and Metroid |

**Do not sync `config.json`** — it is the publisher configuration layer and should remain as shipped. Player preferences (volume, video filter, input bindings, language, etc.) are stored in `user.json` under `%APPDATA%\<WindowTitle>\` (or the equivalent Proton path on Steam Deck). That directory is outside the Steam install tree, so Steam never touches it during updates or cloud sync — no Auto-Cloud rule is needed for it.

### Steamworks dashboard setup

1. Navigate to your app and open **Cloud → Cloud Settings**.
2. Set the **Quota** to at least 10 MB (NES states are typically 10–50 KB each).
3. Under **Root Overrides**, add two entries with root `GameInstall`: one for `saves\*` and one for `game.srm`.
4. Publish the cloud configuration.

### Limitations

- **Conflict resolution is opaque.** Steam uses last-write-wins. There is no in-game conflict UI.
- **A crash can lose up to ~5 minutes of progress.** The auto-save fires when the in-game menu opens, every ~5 minutes during active play, and on graceful exit — so the worst-case exposure window on a crash is one periodic interval. See [Auto-save](configuration.md#auto-save) in the configuration reference.
- **Manual slot saves are immediately safe.** Each manual save writes synchronously; Steam picks it up on the next sync.
- **NEShim collects no data.** There is no telemetry or automatic crash reporting. A `crash.log` is written locally on crash but never transmitted. For your Steam store privacy policy, any applicable data collection comes from Steam itself (playtime, achievements, cloud saves) and is covered by Valve's Privacy Policy. See [Network activity and telemetry](architecture.md#network-activity-and-telemetry).

---

## 7. Rename the Steam Input VDF

In the source tree, rename `NEShim/NEShim/game_actions_0.vdf` to `game_actions_<YourAppID>.vdf`. The file contents do not change — only the filename. The renamed file is copied to the output directory at build time.

---

## 8. Configure Steam Input

Upload the action definition file and the default controller bindings to the Steamworks partner dashboard.

### Upload the action definition file

1. Open the Steamworks partner dashboard for your app.
2. Go to **Steam Input → Default Configuration**.
3. Upload `game_actions_<YourAppID>.vdf` as the **Game Actions** file.

The VDF defines two action sets — `Gameplay` and `Menu` — that NEShim switches between automatically. Optionally customise the `localization` block with your game's terminology.

### Upload default controller bindings

The `controller_bindings/` directory (built to the output directory automatically) contains a pre-built default configuration for each supported controller type. Upload each file in the Steamworks dashboard as the **Default Configuration** for its controller type:

| File | Controller type |
|---|---|
| `xbox360.vdf` | Xbox 360 |
| `xboxone.vdf` | Xbox One / Xbox Series X\|S / Xbox One Elite |
| `neptune.vdf` | Steam Deck |
| `ps4.vdf` | PlayStation 4 DualShock 4 |
| `ps5.vdf` | PlayStation 5 DualSense |
| `switch_pro.vdf` | Nintendo Switch Pro Controller |
| `steam_controller.vdf` | Valve Steam Controller |

Without these defaults, players must configure their controller bindings manually from the Steam overlay. With them, supported controllers work immediately at first launch.

---

## 9. Generate a signing keypair

NEShim uses ECDSA-P256 asymmetric signing. The default keypair in the source is publicly known — generate your own before shipping.

```bash
pub-utils --gen-keypair
```

Output:

```
Private key (keep secret — never commit; store in 1Password, a local file, or a CI secret):
<base64>

Public key (embed in AchievementSigner.EmbeddedPublicKeyBase64 OR set as achievementPublicKey in config.json):
<base64>
```

1. Store the private key securely outside source control (local file, 1Password, or CI secret).
2. Set `EmbeddedPublicKeyBase64` in `NEShim/NEShim.Signing/AchievementSigner.cs` to the printed public key:

   ```csharp
   public const string? EmbeddedPublicKeyBase64 = "MFkwEwYHKo..."; // your public key
   ```

   This bakes the key into the binary. It takes precedence over `achievementPublicKey` in config.json and cannot be overridden without recompiling.

3. Rebuild the solution.
4. Re-seal all `achievements.json` files: `pub-utils --key-file private_key.txt achievements.json`.

The keypair only needs to be generated once for the lifetime of the game. Achievements will not fire until a key is configured. See [Achievement system — Key management](achievements.md#key-management).

---

## 10. Set up achievements in Steamworks

Before achievements can fire in-game, they must be registered in the Steamworks partner dashboard:

1. Navigate to **Achievements** for your app.
2. Create each achievement with an **API Name** (e.g. `ACH_FIRST_WIN`). This name is the `steamId` field in `achievements.json`.
3. Add a name, description, and icon for each achievement.
4. Add translated names and descriptions for each supported language. Steam returns the achievement display name in the user's current game language — the unlock pop-up shown in-game pulls the name directly from Steam, so translations entered here appear automatically with no extra code. Missing languages fall back to the English name.
5. Publish the achievements from the dashboard.

---

## 11. Author and seal `achievements.json`

1. Create `achievements.json` in the game's output directory (alongside the exe).
2. Compute your ROM's SHA1 hash (see [Finding the ROM SHA1 hash](achievements.md#finding-the-rom-sha1-hash)).
3. Author the achievement definitions. See [Achievement system](achievements.md) for the full field reference.

Example:

```json
{
  "A1B2C3D4E5F60718293A4B5C6D7E8F90A1B2C3D4": {
    "memoryDomain": "System Bus",
    "achievements": [
      {
        "steamId":    "ACH_FIRST_WIN",
        "address":    255,
        "bytes":      1,
        "encoding":   "binary",
        "comparison": "equals",
        "value":      1
      }
    ]
  }
}
```

4. Seal the file using your private key:

```bash
pub-utils --key-file private_key.txt achievements.json
```

Verify all definitions are listed as `[sealed]` in the output. Never edit `achievements.json` after sealing without re-sealing — any changed definition will fail signature verification and silently stop firing.

---

## 12. Prepare artwork and audio assets

All artwork paths in `config.json` are relative to the executable directory.

| Config field | Purpose | Recommended size |
|---|---|---|
| `mainMenuBackgroundPath` | Full-screen background on the pre-game menu | **1920×1080 px** (16:9). Stretched to fill the window — aspect-ratio distortion occurs if source doesn't match the window. |
| `sidebarLeftPath` | Image in the left letterbox bar during gameplay | **~302×1080 px at 1080p** (1:3.6 portrait). Cover-scaled; see sizing note below. |
| `sidebarRightPath` | Image in the right letterbox bar during gameplay | Same as left sidebar. |
| `mainMenuMusicPath` | Looping audio for the pre-game menu | MP3 or WAV. Plays with fade-in/fade-out transitions. |

**Sidebar image sizing:** Each sidebar bar spans the full window height and is a narrow portrait strip. With the default Pixel Perfect (8:7 PAR) filter on a 16:9 display, each bar is approximately **~201×720 px at 720p**, **~302×1080 px at 1080p**, and **~402×1440 px at 1440p** (all `overscanMode: "Normal"`, 240 rows). On Steam Deck (16:10 at 1280×800) bars are narrower — approximately **~152×800 px**. The bars are about 10–16 px narrower in Overscan mode (224 rows). Images are cover-scaled, so exact pixel counts don't need to match; design at an aspect ratio of roughly **1:3.6 portrait** for 16:9 displays. On Steam Deck the bars are narrower relative to their height (~1:5.3), so keep key art near the centre column of your image. Sidebar art always renders at its original colours — Color Effects do not apply to it.

---

## 13. Verify audio settings

| Setting | Recommendation |
|---|---|
| `volume` | Set a comfortable default (e.g. 80) so the game doesn't start at maximum volume. |
| `audioFilter` | Choose a default filter. `"Default"` is the accurate NES hardware filter. `"Saturation"` is recommended for Steam Deck speakers. `"Warm"` suits speakers that benefit from a slightly softer sound. Unknown values throw a startup error. |

---

## 14. Build and publish

### Shader compilation (pre-build requirement)

#### DXBC shaders (D3D11 / Windows)

NEShim's D3D11 renderer uses HLSL shaders compiled to DXBC bytecode (`.cso` files) by MSBuild using `fxc.exe` from the Windows SDK **before** the C# build begins. The compiled bytecode is embedded in the assembly as a resource.

`fxc.exe` is automatically located by MSBuild at `$(WindowsSdkDir)bin\$(WindowsSDKVersion)\x64\fxc.exe`. **If the Windows SDK is not installed**, MSBuild falls back to the pre-compiled `.cso` files checked into source control:

```
[CompileShaders] fxc.exe not found at a Windows SDK path — using pre-compiled CSOs from source control.
```

**The Windows SDK is only required if you have modified `.hlsl` source files.** Install the **Windows 10 SDK** (≥ 10.0.19041) via the Visual Studio Installer or the standalone [Windows SDK download](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) before making shader changes.

On Proton/Steam Deck, DXVK compiles the DXBC bytecode to SPIR-V on first launch and caches it. The shaders are trivially simple; first-launch compile is near-instant.

#### SPIR-V shaders (SDL_GPU / Linux)

The SDL_GPU renderer (Linux) uses SPIR-V shaders (`.spv` files) compiled from the same HLSL source by `dxc.exe` via the `Microsoft.Direct3D.DXC` NuGet package. The `CompileSpirvShaders` MSBuild target runs `dxc.exe` automatically if the tool is available. Pre-compiled `.spv` files are checked into source control and used when `dxc.exe` is not found, following the same fallback pattern as DXBC shaders.

**DXC is only required if you have modified `.hlsl` source files.** Standard builds use the pre-compiled `.spv` files and do not need DXC.

### Publish commands

```bash
# Windows — self-contained, ReadyToRun, win-x64
dotnet publish NEShim/NEShim/NEShim.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishReadyToRun=true \
  -o publish/MyGame-win-x64

# Linux — self-contained, ReadyToRun, linux-x64
dotnet publish NEShim/NEShim/NEShim.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishReadyToRun=true \
  -o publish/MyGame-linux-x64

# Or publish all platforms at once using the publish script:
.\local-publish.ps1 1.0.0
```

`PublishReadyToRun` pre-compiles managed IL to native code at build time. Without it, the .NET JIT compiles methods on first call at runtime — on Proton/Wine this is significantly more expensive because every JIT step calls `VirtualAlloc`/`VirtualProtect`, which Wine intercepts and translates. The result is noticeable frame spikes on ROM load, menu transitions, and achievement unlocks. Always include this flag; **do not use plain `dotnet build` output for performance testing on Proton or Steam Deck**.

**Cross-compiling from Windows:** you can build both `win-x64` and `linux-x64` from the same Windows machine — platform-specific files (the D3D11 renderer vs. the SDL_GPU/Vulkan renderer and its Linux native libraries) are selected based on the `-r` target you pass, not the machine you're building on, so a `linux-x64` build produced this way is a real, functional Linux build. This is also how the official release pipeline works — it publishes both platforms from a single Windows runner.

After the build completes, copy your game assets (`config.json`, `achievements.json`, `game.nes`, artwork, audio) into each platform's output directory, then copy the matching Steamworks native library from the [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) alongside the exe (see [step 5](#5-steamworks-native-library)).

---

## 15. Test the release build

Before uploading to Steam:

1. Copy the entire output directory to a machine without .NET installed to verify the self-contained runtime works.
2. Launch through Steam (not directly from Explorer) to verify:
   - Steam overlay appears when Shift+Tab is pressed.
   - Gamepad input works via Steam Input if configured.
   - Achievements fire when conditions are met.
   - The game icon appears correctly in the Steam library.
3. Verify the auto-save and save state slots work (save, quit, reload).
4. Verify battery RAM persistence if the game uses it.

---

## Steam Deck

NEShim runs on Steam Deck natively via the Linux x64 build (SDL_GPU/Vulkan path) or via Proton using the Windows x64 build (D3D11/DXVK path). No configuration changes are required for either path.

**Use the published build for Deck testing, not a plain `dotnet build` output.** The `-p:PublishReadyToRun=true` flag in [step 14](#14-build-and-publish) makes a significant difference on Proton — without it, JIT overhead causes frame spikes that are not present in the shipped binary. Testing with `dotnet build` output and concluding there is a performance problem is a common mistake.

For the native Linux build, copy the linux-x64 publish output to the Deck. No Proton layer is involved — the SDL_GPU/Vulkan renderer communicates directly with the GPU.


---

## Release checklist

- [ ] `windowTitle` set in `config.json`
- [ ] `icon.ico` replaced with your game artwork
- [ ] `<AssemblyName>` changed in `NEShim.csproj` if renaming the assembly (optional)
- [ ] Signing keypair generated with `pub-utils --gen-keypair`; public key set in `AchievementSigner.EmbeddedPublicKeyBase64` and solution rebuilt; private key stored outside source control
- [ ] `steam_appid.txt` updated with your production App ID
- [ ] `game_actions_0.vdf` renamed to `game_actions_<appid>.vdf` in source
- [ ] Steamworks native library copied from [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into each platform's output directory (`steam_api64.dll` for Windows, `libsteam_api.so` for Linux **renamed to `libsteam_api64.so`**) and included in the corresponding Steam depots
- [ ] Steam Auto-Cloud configured in the Steamworks dashboard (`saves\*` and `game.srm` under `GameInstall` root; `config.json` excluded — player preferences live in AppData `user.json`, which Steam cannot touch)
- [ ] Renamed VDF uploaded to Steamworks dashboard under **Steam Input → Default Configuration**
- [ ] Each `controller_bindings/*.vdf` uploaded as Default Configuration for its controller type
- [ ] All achievements created in the Steamworks dashboard with matching API names
- [ ] Translated achievement names and descriptions added in Steamworks dashboard for each supported language
- [ ] `achievements.json` authored and sealed with `pub-utils --key-file <keyfile>`
- [ ] `lang/*.json` files present for each supported language (built-in files are compiled in; confirm `lang\*.json` content items copy to output)
- [ ] Supported languages list set in Steamworks dashboard under **Store Presence → Basic Info**
- [ ] Localized store descriptions and screenshots uploaded in Steamworks dashboard for each supported language
- [ ] Artwork and music assets in place and referenced in `config.json`
- [ ] Audio defaults verified in `config.json`
- [ ] If shipping local multiplayer: `playerCount` set (1–4) in `config.json`; gamepad-first out of the box — hand-author `key` bindings for players 2–4 in `inputMappings` if local keyboard co-op is wanted; `playerCount` treated as fixed for the life of this release (changing it later breaks existing players' save states — see [Input — Local multiplayer](input.md#local-multiplayer))
- [ ] Release build passes local smoke test (saves, Steam overlay, achievements)
- [ ] Localization tested locally for each supported language (set `"language": "<code>"` in `config.json`, launch outside Steam)
- [ ] `THIRD-PARTY-NOTICES.md` updated if any new dependencies were added

---

## Deployed file layout

If you set `<AssemblyName>MyGame</AssemblyName>`, the Windows x64 output will look like:

```
MyGame-win-x64/
├── MyGame.exe
├── MyGame.dll
├── MyGame.deps.json
├── MyGame.runtimeconfig.json
├── NEShim.Signing.dll
├── BizHawk.dll
├── steam_api64.dll             ← from Steamworks.NET release zip; Windows depot only
├── steam_appid.txt
├── game_actions_1234560.vdf
├── controller_bindings/
│   ├── xbox360.vdf
│   ├── xboxone.vdf
│   ├── neptune.vdf
│   ├── ps4.vdf
│   ├── ps5.vdf
│   ├── switch_pro.vdf
│   └── steam_controller.vdf
├── config.json
├── achievements.json
├── game.nes
├── saves/                      ← created automatically on first save
├── game.srm                    ← created automatically if game uses battery RAM
├── art/
│   ├── menu_bg.png
│   ├── sidebar_left.png
│   └── sidebar_right.png
├── audio/
│   └── menu_theme.mp3
└── [.NET runtime files...]
```

The Linux x64 layout (`MyGame-linux-x64/`) is identical with two differences:
- The executable has no `.exe` extension (`MyGame` or `NEShim`)
- `libsteam_api64.so` replaces `steam_api64.dll` — note the name change: the Steamworks.NET release zip ships this file as `libsteam_api.so`, but it must be **renamed to `libsteam_api64.so`** in your depot (see "Linux naming gotcha" above)

Without `<AssemblyName>`, replace the top four entries with `MyGame.exe` (renamed manually) and `NEShim.dll`, `NEShim.deps.json`, `NEShim.runtimeconfig.json` (unchanged).
