---
layout: default
title: Building from source
parent: Publishing
nav_order: 2
description: "Build a custom NEShim binary for your game — custom icon, HMAC key rotation, and full assembly rename."
---

# Building from source

Use this path if you have the NEShim source and want to build your own binary. Required for a custom embedded exe icon, renaming the underlying assembly, or rotating the HMAC key before a public release.

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
| Taskbar, title bar, alt-tab thumbnail | `Form.Icon` loaded from the managed embedded resource at startup | All builds, including debug |

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

## 5. `steam_api64.dll`

`steam_api64.dll` is **not** stored in the repository (Valve SDK license). After `dotnet publish`, copy it from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe. Use the copy bundled with the wrapper — it is matched to the wrapper version and must not be sourced separately from the Steamworks SDK partner dashboard. The current build targets **Steamworks.NET 2025.163.0**.

Include it in your Steam depot when uploading; Valve does not inject it automatically. Once it is in your depot, Steam distributes it to players as part of the normal game install.

If you ever need to upgrade Steamworks.NET, replace `lib/Steamworks.NET.dll` with the new version from the [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) and supply the matching `steam_api64.dll` from the same zip at packaging time — they must be kept in sync.

---

## 6. Configure Steam Auto-Cloud

NEShim reads and writes save files to the local filesystem only. Cloud sync is handled entirely by **Steam Auto-Cloud** configured in the Steamworks partner dashboard — no code changes are required.

### Files to sync

| Path pattern | Contents |
|---|---|
| `saves\*` | Manual save states (`slot0.state` … `slot7.state`), slot metadata (`.meta`), and the auto-save (`autosave.state`) |
| `game.srm` | Battery-backed RAM — the cartridge save for games like Zelda and Metroid |

Do **not** sync `config.json`. Settings like `windowMode` and `volume` are machine-specific; syncing them will overwrite a player's preferences on every machine they use.

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
seal-achievements --gen-keypair
```

Output:

```
Private key (keep secret — never commit; store in 1Password, a local file, or a CI secret):
<base64>

Public key (embed in AchievementSigner.DefaultPublicKeyBase64 OR set as achievementPublicKey in config.json):
<base64>
```

1. Store the private key securely outside source control (local file, 1Password, or CI secret).
2. Set `EmbeddedPublicKeyBase64` in `NEShim/NEShim.AchievementSigning/AchievementSigner.cs` to the printed public key:

   ```csharp
   public const string? EmbeddedPublicKeyBase64 = "MFkwEwYHKo..."; // your public key
   ```

   This bakes the key into the binary. It takes precedence over `achievementPublicKey` in config.json and cannot be overridden without recompiling.

3. Rebuild the solution.
4. Re-seal all `achievements.json` files: `seal-achievements --key-file private_key.txt achievements.json`.

The keypair only needs to be generated once for the lifetime of the game. Achievements will not fire until a key is configured. See [Achievement system — Key management](achievements.md#key-management).

---

## 10. Set up achievements in Steamworks

Before achievements can fire in-game, they must be registered in the Steamworks partner dashboard:

1. Navigate to **Achievements** for your app.
2. Create each achievement with an **API Name** (e.g. `ACH_FIRST_WIN`). This name is the `steamId` field in `achievements.json`.
3. Add a name, description, and icon for each achievement.
4. Publish the achievements from the dashboard.

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
seal-achievements --key-file private_key.txt achievements.json
```

Verify all definitions are listed as `[sealed]` in the output. Never edit `achievements.json` after sealing without re-sealing — any changed definition will fail signature verification and silently stop firing.

---

## 12. Prepare artwork and audio assets

All artwork paths in `config.json` are relative to the executable directory.

| Config field | Purpose | Notes |
|---|---|---|
| `mainMenuBackgroundPath` | Full-screen background on the pre-game menu | Any common image format. Stretched/filled to the window size. |
| `sidebarLeftPath` | Image in the left letterbox bar during gameplay | Scaled to fill the full bar area (cover, maintaining aspect ratio), centered, overflow cropped. |
| `sidebarRightPath` | Image in the right letterbox bar during gameplay | Same rules as left sidebar. |
| `mainMenuMusicPath` | Looping audio for the pre-game menu | MP3 or WAV recommended. Plays with fade-in/fade-out transitions. |

---

## 13. Verify audio settings

| Setting | Recommendation |
|---|---|
| `volume` | Set a comfortable default (e.g. 80) so the game doesn't start at maximum volume. |
| `soundScrubberEnabled` | Test both settings. On high-quality speakers the scrubber mode (`true`) is warmer. On laptop or TV speakers the default NES filter (`false`) may be fine. |

---

## 14. Build and publish

```bash
dotnet publish NEShim/NEShim/NEShim.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o publish/MyGame
```

After the build completes, copy your game assets (`config.json`, `achievements.json`, `game.nes`, artwork, audio) into the output directory, then copy `steam_api64.dll` from the [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) alongside the exe (see [step 5](#5-steam_api64dll)).

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

## Release checklist

- [ ] `windowTitle` set in `config.json`
- [ ] `icon.ico` replaced with your game artwork
- [ ] `<AssemblyName>` changed in `NEShim.csproj` if renaming the assembly (optional)
- [ ] Signing keypair generated with `seal-achievements --gen-keypair`; public key set in `AchievementSigner.EmbeddedPublicKeyBase64` and solution rebuilt; private key stored outside source control
- [ ] `steam_appid.txt` updated with your production App ID
- [ ] `game_actions_0.vdf` renamed to `game_actions_<appid>.vdf` in source
- [ ] `steam_api64.dll` copied from [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory and included in your Steam depot
- [ ] Steam Auto-Cloud configured in the Steamworks dashboard (`saves\*` and `game.srm` under `GameInstall` root; `config.json` excluded)
- [ ] Renamed VDF uploaded to Steamworks dashboard under **Steam Input → Default Configuration**
- [ ] Each `controller_bindings/*.vdf` uploaded as Default Configuration for its controller type
- [ ] All achievements created in the Steamworks dashboard with matching API names
- [ ] `achievements.json` authored and sealed with `seal-achievements --key-file <keyfile>`
- [ ] Artwork and music assets in place and referenced in `config.json`
- [ ] Audio defaults verified in `config.json`
- [ ] Release build passes local smoke test (saves, Steam overlay, achievements)
- [ ] `THIRD-PARTY-NOTICES.md` updated if any new dependencies were added

---

## Deployed file layout

If you set `<AssemblyName>MyGame</AssemblyName>`, the output will look like:

```
MyGame/
├── MyGame.exe
├── MyGame.dll
├── MyGame.deps.json
├── MyGame.runtimeconfig.json
├── NEShim.AchievementSigning.dll
├── BizHawk.dll
├── steam_api64.dll             ← from Steamworks.NET release zip; must be included in your Steam depot
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

Without `<AssemblyName>`, replace the top four entries with `MyGame.exe` (renamed manually) and `NEShim.dll`, `NEShim.deps.json`, `NEShim.runtimeconfig.json` (unchanged).
