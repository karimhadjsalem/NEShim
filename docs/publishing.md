---
layout: default
title: Publishing
nav_order: 4
description: "Step-by-step checklist for packaging and releasing a game on Steam using NEShim."
---

# Publishing guide

This page walks through everything required to package a game for Steam release using NEShim. Work through each section in order before building the release binary.

---

## 1. Set the window title

In `config.json`, set `windowTitle` to your game's name. This is the title shown in the Windows taskbar and title bar when the window is in windowed mode.

```json
{
  "windowTitle": "My Game Title"
}
```

---

## 2. Set the executable icon

The executable icon controls what appears in Windows Explorer, the taskbar, alt-tab, and in Steam's game library. This must be configured at **compile time** — runtime icon changes do not affect the file icon that Steam displays.

1. Create a `.ico` file with your game artwork. Include at minimum 16×16, 32×32, 48×48, and 256×256 sizes.
2. Place the `.ico` file in the `NEShim/NEShim/` directory (or any path relative to the project).
3. Add the `<ApplicationIcon>` element to `NEShim/NEShim/NEShim.csproj`:

```xml
<PropertyGroup>
  <ApplicationIcon>mygame.ico</ApplicationIcon>
</PropertyGroup>
```

4. Rebuild. The icon is now embedded in the exe.

---

## 3. Configure Steam App ID

1. Register your game in the Steamworks partner dashboard and obtain your App ID.
2. Replace the contents of `NEShim/NEShim/steam_appid.txt` with your App ID (a plain integer, no trailing newline):

```
1234560
```

This file is copied to the output directory at build time. During development it allows the game to connect to Steam without going through the Steam client's launch process.

---

## 4. Obtain `steam_api64.dll`

Steamworks.NET is a managed C# wrapper that P/Invokes into the native `steam_api64.dll` at runtime. This DLL is **not** included in the repository (Valve SDK license) and must be placed alongside the executable manually.

1. Download the Steamworks SDK from the [Steamworks partner dashboard](https://partner.steamgames.com/).
2. Copy `sdk/redistributable_bin/win64/steam_api64.dll` into your output directory (next to the exe).
3. Do not commit this file to source control — add it to `.gitignore`.

When you deploy through Steam, the Steam client delivers `steam_api64.dll` to players automatically as part of your depot. You only need to bundle it yourself for local development and non-Steam distribution.

---

## 6. Configure Steam Input (optional but recommended)

If you want Steam Controller support beyond basic XInput emulation:

1. Rename `game_actions_0.vdf` to `game_actions_<YourAppID>.vdf`.
2. Update the App ID in the filename only — the file contents define action names, not the App ID.
3. Optionally customise the `localization` block with your game's terminology.
4. Upload the VDF file via the Steamworks partner dashboard under **Steam Input → Default Configuration**.

The VDF defines two action sets — `Gameplay` and `Menu` — that NEShim switches between automatically. The action names in the VDF must match what `SteamInputManager` requests (see [Input system](input.md#steam-input)).

---

## 7. Set up achievements in Steamworks

Before achievements can fire in-game, they must be registered in the Steamworks partner dashboard:

1. In the dashboard, navigate to **Achievements** for your app.
2. Create each achievement with an **API Name** (e.g. `ACH_FIRST_WIN`). This name is the `steamId` field in `achievements.json`.
3. Add a name, description, and icon for each achievement.
4. Set the store page visibility as needed.
5. Publish the achievements from the Steamworks dashboard.

---

## 8. Generate a new HMAC key

The default HMAC key in the source is publicly known. Replace it before shipping any public build.

```bash
seal-achievements --gen-key
```

Copy the printed key value and paste it into the `HmacKeyBase64` constant in `NEShim/NEShim.AchievementSigning/AchievementSigner.cs`:

```csharp
private const string HmacKeyBase64 = "YOUR_NEW_KEY_HERE=";
```

Rebuild the solution after making this change. Keep your key private — it only needs to be changed once for the lifetime of the game.

---

## 9. Author and seal `achievements.json`

1. Create `achievements.json` in the game's output directory (alongside the exe).
2. Compute your ROM's SHA1 hash (see [Finding the ROM SHA1 hash](achievements.md#finding-the-rom-sha1-hash)).
3. Author the achievement definitions. See [Achievement system](achievements.md) for the full field reference.

Example file:

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

Verify all definitions are listed as `[sealed]` in the output.

5. Never edit `achievements.json` after sealing without re-sealing. Any changed definition will fail signature verification and silently stop firing.

---

## 10. Prepare artwork assets

All artwork paths in `config.json` are relative to the executable directory.

| Config field | Purpose | Notes |
|---|---|---|
| `mainMenuBackgroundPath` | Full-screen background on the pre-game menu | Any common image format. Stretched/filled to the window size. |
| `sidebarLeftPath` | Image in the left letterbox bar during gameplay | Drawn at 1:1 pixel resolution, centered, cropped to bar width. |
| `sidebarRightPath` | Image in the right letterbox bar during gameplay | Same rules as left sidebar. |
| `mainMenuMusicPath` | Looping audio for the pre-game menu | MP3 or WAV recommended. Plays with fade-in/fade-out transitions. |

---

## 11. Verify audio settings

| Setting | Recommendation |
|---|---|
| `volume` | Set a comfortable default (e.g. 80) so the game doesn't start at maximum volume. |
| `soundScrubberEnabled` | Test both settings. On high-quality speakers the scrubber mode (`true`) is warmer. On laptop or TV speakers the default NES filter (`false`) may be fine. |

---

## 12. Build and publish

```bash
# Self-contained win-x64 build
dotnet publish NEShim/NEShim/NEShim.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o publish/MyGame
```

The output directory will contain the executable, the .NET runtime files, and BizHawk binaries. After the build completes, copy `steam_api64.dll` into the output directory (see [step 4](#4-obtain-steam_api64dll)), then copy your game assets (`config.json`, `achievements.json`, `game.nes`, artwork, audio) alongside it.

---

## 13. Test the release build

Before uploading to Steam:

1. Copy the entire `publish/MyGame` directory to a machine that does not have .NET installed to verify the self-contained runtime works.
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
- [ ] `<ApplicationIcon>` set in `NEShim.csproj` and icon file in place
- [ ] `steam_appid.txt` contains your production App ID
- [ ] `steam_api64.dll` copied into the output directory from the Steamworks SDK (`sdk/redistributable_bin/win64/`)
- [ ] `game_actions_<appid>.vdf` renamed with correct App ID
- [ ] All achievements created in the Steamworks dashboard with matching API names
- [ ] HMAC key rotated in `AchievementSigner.cs` and solution rebuilt
- [ ] `achievements.json` authored and sealed with `seal-achievements`
- [ ] Artwork and music assets in place and referenced in `config.json`
- [ ] Release build passes local smoke test (saves, Steam overlay, achievements)
- [ ] `THIRD-PARTY-NOTICES.md` updated if any new dependencies were added

---

## Deployed file layout

A minimal deployment looks like:

```
MyGame/
├── MyGame.exe                  ← renamed from NEShim.exe
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
└── [.NET runtime files, BizHawk DLLs...]
```

The `.exe` can be renamed freely — Steam identifies the game by App ID, not filename.
