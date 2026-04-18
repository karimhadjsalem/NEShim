---
layout: default
title: Publishing
nav_order: 4
description: "Step-by-step checklist for packaging and releasing a game on Steam using NEShim."
---

# Publishing guide

There are two ways to ship a game with NEShim:

- **[Pre-built release](#pre-built-release)** — download a packaged NEShim binary, drop in your ROM and assets, and configure `config.json`. No compiler or .NET SDK required.
- **[Building from source](#building-from-source)** — clone the repository, customise the project (icon, HMAC key, etc.), and build your own binary. Required if you want a custom exe icon embedded in the file or need to rotate the HMAC key before shipping.

Work through the path that matches your situation. Each path is a complete, self-contained checklist.

---

## Pre-built release

Use this path if you downloaded a packaged NEShim release and want to configure it for your game without recompiling.

### 1. Rename the executable

If you want your game to appear as `MyGame.exe` rather than `NEShim.exe`, rename these four files together — the .NET app host derives the names of its correlated files from its own filename at runtime:

| Rename from | Rename to |
|---|---|
| `NEShim.exe` | `MyGame.exe` |
| `NEShim.dll` | `MyGame.dll` |
| `NEShim.deps.json` | `MyGame.deps.json` |
| `NEShim.runtimeconfig.json` | `MyGame.runtimeconfig.json` |

All other files — `NEShim.AchievementSigning.dll`, `BizHawk.dll`, runtime DLLs, NAudio, Steamworks.NET — are referenced by their own assembly names and do not need to change. The `.pdb` files are debug symbols and can be omitted from distribution builds entirely.

The exe icon in a pre-built release is fixed. If you need a custom exe icon embedded in the file itself, use the [Building from source](#building-from-source) path instead. The taskbar and window icon at runtime are controlled separately and are already set correctly by the pre-built binary (see [Icon behaviour](#icon-behaviour)).

### 2. Set the window title

In `config.json`, set `windowTitle` to your game's name:

```json
{
  "windowTitle": "My Game Title"
}
```

### 3. Configure Steam App ID

1. Register your game in the Steamworks partner dashboard and obtain your App ID.
2. Replace the contents of `steam_appid.txt` (in the output directory, next to the exe) with your App ID — a plain integer, no trailing newline:

```
1234560
```

### 4. Obtain `steam_api64.dll`

Steamworks.NET P/Invokes into the native `steam_api64.dll` at runtime. This file is **not** included in a NEShim release (Valve SDK license) and must be added manually.

1. Download the Steamworks SDK from the [Steamworks partner dashboard](https://partner.steamgames.com/).
2. Copy `sdk/redistributable_bin/win64/steam_api64.dll` into the output directory (next to the exe).

When you deploy through Steam, the Steam client delivers this DLL to players automatically as part of your depot.

### 5. Configure Steam Auto-Cloud

See [steps 5–5c in the source path](#5-configure-steam-auto-cloud-1) — the Steamworks dashboard configuration is identical regardless of which path you used. The only difference is that for a pre-built release the VDF file rename (step 6 below) is done in the output directory rather than in the source tree.

### 6. Rename the Steam Input VDF

In the output directory, rename `game_actions_0.vdf` to `game_actions_<YourAppID>.vdf`. The file contents do not change — only the filename.

### 7. Configure Steam Input in the dashboard

Upload the renamed VDF via the Steamworks partner dashboard under **Steam Input → Default Configuration**. See [Steam Input](#7-configure-steam-input-optional-but-recommended-1) in the source path for full details; the dashboard steps are identical.

### 8. Set up achievements in Steamworks

See [step 7 in the source path](#7-set-up-achievements-in-steamworks) — the Steamworks dashboard steps are identical.

### 9. Author and seal `achievements.json`

See [step 9 in the source path](#9-author-and-seal-achievementsjson) — the file format and sealing process are identical. Note that a pre-built release ships with a known HMAC key. The signed achievements will verify correctly, but anyone with access to the key can forge signatures. If this matters for your project, use the [Building from source](#building-from-source) path and rotate the key.

### 10. Prepare artwork and audio assets

See [step 10 in the source path](#10-prepare-artwork-assets) — identical for both paths.

### 11. Set audio defaults

See [step 11 in the source path](#11-verify-audio-settings) — identical for both paths.

### 12. Test

See [step 13 in the source path](#13-test-the-release-build) — identical for both paths.

### Pre-built release checklist

- [ ] Exe and its three correlated files renamed (`NEShim` → `MyGame`)
- [ ] `windowTitle` set in `config.json`
- [ ] `steam_appid.txt` updated with your production App ID
- [ ] `steam_api64.dll` copied into the output directory
- [ ] Steam Auto-Cloud configured in the Steamworks dashboard (`saves\*` and `game.srm` under `GameInstall` root; `config.json` excluded)
- [ ] `game_actions_0.vdf` renamed to `game_actions_<appid>.vdf`
- [ ] Steam Input VDF uploaded to Steamworks dashboard (optional)
- [ ] All achievements created in the Steamworks dashboard with matching API names
- [ ] `achievements.json` authored and sealed with `seal-achievements`
- [ ] Artwork and music assets in place and referenced in `config.json`
- [ ] Audio defaults verified in `config.json`
- [ ] Release passes local smoke test (saves, Steam overlay, achievements)

---

## Building from source

Use this path if you have the NEShim source and want to build your own binary. This is required for a custom embedded exe icon and to rotate the HMAC key before a public release.

### 1. Set the window title

In `config.json`, set `windowTitle` to your game's name:

```json
{
  "windowTitle": "My Game Title"
}
```

### 2. Set the executable icon

Replace `NEShim/NEShim/icon.ico` with your game's icon. No project file changes are required — the csproj already references this file for both the embedded exe icon and the runtime window icon.

The `.ico` file must contain at minimum: 16×16, 32×32, 48×48, and 256×256. Most icon editors export all sizes in one pass.

#### Icon behaviour

The icon file serves two purposes, handled separately:

| Context | Mechanism | When it applies |
|---|---|---|
| Windows Explorer file icon, Steam library | Win32 resource embedded in the exe at publish time | `dotnet publish` (self-contained) builds only |
| Taskbar, title bar, alt-tab thumbnail | `Form.Icon` loaded from the managed embedded resource at startup | All builds, including debug |

Both are driven from the same `icon.ico` file. In debug builds the exe file shown in Explorer will still have a generic icon, but the running application's taskbar and window icon will show your artwork.

### 3. Configure Steam App ID

1. Register your game in the Steamworks partner dashboard and obtain your App ID.
2. Replace the contents of `NEShim/NEShim/steam_appid.txt` with your App ID — a plain integer, no trailing newline:

```
1234560
```

This file is copied to the output directory at build time. During development it lets the game connect to Steam without going through the Steam client's launch process.

### 4. Obtain `steam_api64.dll`

Steamworks.NET P/Invokes into the native `steam_api64.dll` at runtime. This file is **not** included in the repository (Valve SDK license).

1. Download the Steamworks SDK from the [Steamworks partner dashboard](https://partner.steamgames.com/).
2. Copy `sdk/redistributable_bin/win64/steam_api64.dll` into your output directory (next to the exe) after building.
3. Do not commit this file to source control — add it to `.gitignore`.

When you deploy through Steam, the Steam client delivers this DLL to players automatically as part of your depot.

### 5. Configure Steam Auto-Cloud

NEShim reads and writes save files to the local filesystem only. Cloud sync is handled entirely by **Steam Auto-Cloud** configured in the Steamworks partner dashboard — no code changes are required.

#### Files to sync

| Path pattern | Contents |
|---|---|
| `saves\*` | Manual save states (`slot0.state` … `slot7.state`), slot metadata (`.meta`), and the auto-save (`autosave.state`) |
| `game.srm` | Battery-backed RAM — the cartridge save for games like Zelda and Metroid |

Do **not** sync `config.json`. Settings like `windowMode` and `volume` are machine-specific.

#### Steamworks dashboard setup

1. Navigate to your app and open **Cloud → Cloud Settings**.
2. Set the **Quota** to at least 10 MB (NES states are typically 10–50 KB each).
3. Under **Root Overrides**, add two entries with root `GameInstall`: one for `saves\*` and one for `game.srm`.
4. Publish the cloud configuration.

#### Limitations

- **Conflict resolution is opaque.** Steam uses last-write-wins. There is no in-game conflict UI.
- **The auto-save is not crash-safe.** It is written only on graceful exit. See [Auto-save](configuration.md#auto-save) in the configuration reference.
- **Manual slot saves are immediately safe.** Each manual save writes synchronously; Steam picks it up on the next sync.

### 6. Rename the Steam Input VDF

In the source tree, rename `NEShim/NEShim/game_actions_0.vdf` to `game_actions_<YourAppID>.vdf`. The file contents do not change — only the filename. The renamed file is copied to the output directory at build time.

### 7. Configure Steam Input (optional but recommended)

Upload the VDF via the Steamworks partner dashboard under **Steam Input → Default Configuration**.

The VDF defines two action sets — `Gameplay` and `Menu` — that NEShim switches between automatically. Optionally customise the `localization` block with your game's terminology. The action names in the VDF must match what `SteamInputManager` requests (see [Input system](input.md#steam-input)).

### 8. Generate a new HMAC key

The default HMAC key in the source is publicly known. Replace it before shipping any public build.

```bash
seal-achievements --gen-key
```

Paste the printed key into the `HmacKeyBase64` constant in `NEShim/NEShim.AchievementSigning/AchievementSigner.cs`:

```csharp
private const string HmacKeyBase64 = "YOUR_NEW_KEY_HERE=";
```

Rebuild after this change. The key only needs to be rotated once for the lifetime of the game.

### 9. Author and seal `achievements.json`

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

4. Seal the file:

```bash
seal-achievements achievements.json
```

Verify all definitions are listed as `[sealed]` in the output. Never edit `achievements.json` after sealing without re-sealing — any changed definition will fail signature verification and silently stop firing.

### 10. Prepare artwork assets

All artwork paths in `config.json` are relative to the executable directory.

| Config field | Purpose | Notes |
|---|---|---|
| `mainMenuBackgroundPath` | Full-screen background on the pre-game menu | Any common image format. Stretched/filled to the window size. |
| `sidebarLeftPath` | Image in the left letterbox bar during gameplay | Drawn at 1:1 pixel resolution, centered, cropped to bar width. |
| `sidebarRightPath` | Image in the right letterbox bar during gameplay | Same rules as left sidebar. |
| `mainMenuMusicPath` | Looping audio for the pre-game menu | MP3 or WAV recommended. Plays with fade-in/fade-out transitions. |

### 11. Verify audio settings

| Setting | Recommendation |
|---|---|
| `volume` | Set a comfortable default (e.g. 80) so the game doesn't start at maximum volume. |
| `soundScrubberEnabled` | Test both settings. On high-quality speakers the scrubber mode (`true`) is warmer. On laptop or TV speakers the default NES filter (`false`) may be fine. |

### 12. Build and publish

```bash
dotnet publish NEShim/NEShim/NEShim.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o publish/MyGame
```

After the build completes, copy `steam_api64.dll` into the output directory (see [step 4](#4-obtain-steam_api64dll-1)), then copy your game assets (`config.json`, `achievements.json`, `game.nes`, artwork, audio) alongside it.

Optionally rename the exe and its correlated files — see [Deployed file layout](#deployed-file-layout) below.

### 13. Test the release build

Before uploading to Steam:

1. Copy the entire output directory to a machine without .NET installed to verify the self-contained runtime works.
2. Launch through Steam (not directly from Explorer) to verify:
   - Steam overlay appears when Shift+Tab is pressed.
   - Gamepad input works via Steam Input if configured.
   - Achievements fire when conditions are met.
   - The game icon appears correctly in the Steam library.
3. Verify the auto-save and save state slots work (save, quit, reload).
4. Verify battery RAM persistence if the game uses it.

### Source build checklist

- [ ] `windowTitle` set in `config.json`
- [ ] `icon.ico` replaced with your game artwork
- [ ] HMAC key rotated in `AchievementSigner.cs` and solution rebuilt
- [ ] `steam_appid.txt` updated with your production App ID
- [ ] `game_actions_0.vdf` renamed to `game_actions_<appid>.vdf` in source
- [ ] `steam_api64.dll` copied into the output directory
- [ ] Steam Auto-Cloud configured in the Steamworks dashboard (`saves\*` and `game.srm` under `GameInstall` root; `config.json` excluded)
- [ ] Steam Input VDF uploaded to Steamworks dashboard (optional)
- [ ] All achievements created in the Steamworks dashboard with matching API names
- [ ] `achievements.json` authored and sealed with `seal-achievements`
- [ ] Artwork and music assets in place and referenced in `config.json`
- [ ] Audio defaults verified in `config.json`
- [ ] Release build passes local smoke test (saves, Steam overlay, achievements)
- [ ] `THIRD-PARTY-NOTICES.md` updated if any new dependencies were added

---

## Deployed file layout

A minimal deployment looks like:

```
MyGame/
├── MyGame.exe                  ← renamed from NEShim.exe
├── MyGame.dll                  ← renamed from NEShim.dll
├── MyGame.deps.json            ← renamed from NEShim.deps.json
├── MyGame.runtimeconfig.json   ← renamed from NEShim.runtimeconfig.json
├── NEShim.AchievementSigning.dll
├── BizHawk.dll
├── steam_api64.dll             ← from Steamworks SDK; not in repo
├── steam_appid.txt
├── game_actions_1234560.vdf
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

The four renamed files must always match each other — the app host derives its correlated filenames from its own name at runtime.
