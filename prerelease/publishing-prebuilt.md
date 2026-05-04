---
layout: default
title: Pre-built release
parent: Publishing
grand_parent: Pre-release
nav_order: 1
description: "Configure a pre-built NEShim binary for your game without recompiling."
---

# Pre-built release

Use this path if you downloaded a packaged NEShim release and want to configure it for your game without recompiling.

---

## 1. Rename the executable

If you want your game to appear as `MyGame.exe` rather than `NEShim.exe`, rename **only the exe**:

```
NEShim.exe → MyGame.exe
```

The app host has the assembly name (`NEShim`) baked into it as a binary string at publish time. It always looks for `NEShim.dll`, `NEShim.runtimeconfig.json`, and `NEShim.deps.json` by that fixed name regardless of what the exe file is called. Renaming those files will prevent the app from launching.

If you want the underlying assembly name to change as well (so that `NEShim.dll` itself is renamed), that requires a source build with `<AssemblyName>` changed in the csproj — see [Building from source](publishing-source).

The exe icon in a pre-built release is fixed at whatever was embedded when the binary was built. If you need a custom exe icon, use [Building from source](publishing-source) instead. The taskbar and window icon at runtime are set correctly by the binary regardless.

The `.pdb` files are debug symbols and can be omitted from distribution builds entirely.

---

## 2. Set the window title

In `config.json`, set `windowTitle` to your game's name:

```json
{
  "windowTitle": "My Game Title"
}
```

---

## 3. Configure Steam App ID

1. Register your game in the Steamworks partner dashboard and obtain your App ID.
2. Replace the contents of `steam_appid.txt` (in the output directory, next to the exe) with your App ID — a plain integer, no trailing newline:

```
1234560
```

---

## 4. `steam_api64.dll`

`steam_api64.dll` is **not** included in the NEShim release package. Before uploading to Steam, copy it from the [Steamworks.NET 2025.163.0 release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory alongside the exe. Use the copy bundled with the wrapper — it is matched to the wrapper version. The current build targets **Steamworks.NET 2025.163.0**.

Include it in your Steam depot when uploading; Valve does not inject it automatically. Once it is in your depot, Steam distributes it to players as part of the normal game install.

If you ever need to upgrade to a newer Steamworks.NET version, use the copy bundled inside the [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) — it is pre-matched to the wrapper version.

---

## 5. Configure Steam Auto-Cloud

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

## 6. Rename the Steam Input VDF

In the output directory, rename `game_actions_0.vdf` to `game_actions_<YourAppID>.vdf`. The file contents do not change — only the filename.

---

## 7. Configure Steam Input

Upload the renamed VDF and the default controller bindings to the Steamworks partner dashboard.

### Upload the action definition file

1. Open the Steamworks partner dashboard for your app.
2. Go to **Steam Input → Default Configuration**.
3. Upload `game_actions_<YourAppID>.vdf` as the **Game Actions** file.

The VDF defines two action sets — `Gameplay` and `Menu` — that NEShim switches between automatically. Optionally customise the `localization` block with your game's terminology.

### Upload default controller bindings

The `controller_bindings/` directory contains a pre-built default configuration for each supported controller type. Upload each file in the Steamworks dashboard as the **Default Configuration** for its controller type:

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

## 8. Set up achievements in Steamworks

Before achievements can fire in-game, they must be registered in the Steamworks partner dashboard:

1. Navigate to **Achievements** for your app.
2. Create each achievement with an **API Name** (e.g. `ACH_FIRST_WIN`). This name is the `steamId` field in `achievements.json`.
3. Add a name, description, and icon for each achievement.
4. Publish the achievements from the dashboard.

---

## 9. Author and seal `achievements.json`

1. Create `achievements.json` in the output directory (alongside the exe).
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

### Protecting your achievements

There is no default signing key — achievements are disabled until you configure one. Set `achievementPublicKey` in `config.json` to enable and protect achievements without rebuilding from source:

1. Generate your own keypair:

   ```bash
   seal-achievements --gen-keypair
   ```

2. Set `achievementPublicKey` in `config.json` to the printed public key:

   ```json
   {
     "achievementPublicKey": "MFkwEwYHKo..."
   }
   ```

3. Re-seal your `achievements.json` with your private key:

   ```bash
   seal-achievements --key-file private_key.txt achievements.json
   ```

Store the private key securely (1Password, local file outside source control, or CI secret). See [Achievement system — Key management](achievements.md#key-management).

If you need the public key baked into the binary itself, use [Building from source](publishing-source) instead.

---

## 10. Prepare artwork and audio assets

All artwork paths in `config.json` are relative to the executable directory.

| Config field | Purpose | Notes |
|---|---|---|
| `mainMenuBackgroundPath` | Full-screen background on the pre-game menu | Any common image format. Stretched/filled to the window size. |
| `sidebarLeftPath` | Image in the left letterbox bar during gameplay | Scaled to fill the full bar area (cover, maintaining aspect ratio), centered, overflow cropped. |
| `sidebarRightPath` | Image in the right letterbox bar during gameplay | Same rules as left sidebar. |
| `mainMenuMusicPath` | Looping audio for the pre-game menu | MP3 or WAV recommended. Plays with fade-in/fade-out transitions. |

---

## 11. Verify audio settings

| Setting | Recommendation |
|---|---|
| `volume` | Set a comfortable default (e.g. 80) so the game doesn't start at maximum volume. |
| `soundScrubberEnabled` | Test both settings. On high-quality speakers the scrubber mode (`true`) is warmer. On laptop or TV speakers the default NES filter (`false`) may be fine. |

---

## 12. Test

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

- [ ] `NEShim.exe` renamed to `MyGame.exe` (only the exe; all other `NEShim.*` files stay as-is)
- [ ] `windowTitle` set in `config.json`
- [ ] `steam_appid.txt` updated with your production App ID
- [ ] `steam_api64.dll` copied from [Steamworks.NET release zip](https://github.com/rlabrecque/Steamworks.NET/releases) into the output directory and included in your Steam depot
- [ ] Steam Auto-Cloud configured in the Steamworks dashboard (`saves\*` and `game.srm` under `GameInstall` root; `config.json` excluded)
- [ ] `game_actions_0.vdf` renamed to `game_actions_<appid>.vdf`
- [ ] Renamed VDF uploaded to Steamworks dashboard under **Steam Input → Default Configuration**
- [ ] Each `controller_bindings/*.vdf` uploaded as Default Configuration for its controller type
- [ ] All achievements created in the Steamworks dashboard with matching API names
- [ ] Signing keypair generated with `seal-achievements --gen-keypair`; `achievementPublicKey` set in `config.json`; private key stored outside source control (if protecting achievements)
- [ ] `achievements.json` authored and sealed with `seal-achievements --key-file <keyfile>`
- [ ] Artwork and music assets in place and referenced in `config.json`
- [ ] Audio defaults verified in `config.json`
- [ ] Release passes local smoke test (saves, Steam overlay, achievements)

---

## Deployed file layout

```
MyGame/
├── MyGame.exe                  ← renamed from NEShim.exe (only the exe can be renamed)
├── NEShim.dll                  ← must keep this name; baked into the app host
├── NEShim.deps.json            ← must keep this name
├── NEShim.runtimeconfig.json   ← must keep this name
├── NEShim.AchievementSigning.dll
├── BizHawk.dll
├── steam_api64.dll             ← from Steamworks.NET release zip; must be included in your depot
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
