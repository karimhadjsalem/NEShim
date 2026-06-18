---
layout: default
title: Configuration
nav_order: 2
parent: Pre-release
description: "Complete config.json reference — every field with type, default, and examples."
---

# Configuration reference

NEShim is configured entirely through `config.json` placed alongside the executable. If the file does not exist at startup it is created with all defaults. Changes made through the in-game menu are written back to `config.json` on exit.

---

## Core settings

| Field | Type | Default | Description |
|---|---|---|---|
| `romPath` | string | `"game.nes"` | Path to the `.nes` ROM file. Relative paths are resolved from the executable directory. |
| `windowTitle` | string | `"NEShim"` | Title shown in the window title bar and in the Windows taskbar. |
| `windowMode` | string | `"Fullscreen"` | `"Fullscreen"` or `"Windowed"`. Togglable at runtime via F11 or the Settings menu. |

---

## Save settings

| Field | Type | Default | Description |
|---|---|---|---|
| `saveStateDirectory` | string | `"saves"` | Directory where save state files are written. Relative paths resolve from the executable directory. |
| `saveRamPath` | string | `"game.srm"` | Path for battery-backed RAM persistence (used by games like Zelda and Metroid). Written on exit if the emulator reports the save RAM was modified. |
| `activeSlot` | integer | `0` | Index of the currently selected save slot (0–7). Persisted across sessions. |

### Auto-save

NEShim writes `autosave.state` inside `saveStateDirectory` at three points:

- **When the in-game menu opens** — captures the exact state at the moment the player pauses.
- **Every ~5 minutes during active gameplay** — a frame counter fires after approximately 18,000 frames (~5 min at 60 fps).
- **On graceful exit** — written when the window is closed or Exit is chosen from the menu, if the game is running.

No auto-save is written while the pre-game main menu is showing (i.e., before the player has started a game).

The auto-save file is separate from the eight manual slots and cannot be loaded from within the game. It exists as a recovery file for Steam Cloud — if the player's session ends without a manual save, the auto-save gives Steam something to sync so progress is not lost on the next machine.

On a crash or force-quit, the most recent periodic or menu-triggered save remains on disk. At most ~5 minutes of progress is exposed between periodic saves; a clean shutdown always writes a fresh snapshot on exit regardless of when the last periodic save fired.

There are no config fields to enable, disable, or rename the auto-save file. The path is always `<saveStateDirectory>/autosave.state`.

---

## Audio settings

| Field | Type | Default | Description |
|---|---|---|---|
| `audioBufferFrames` | integer | `3` | Size of the audio ring buffer in frames (~16.67 ms each). Increase if you hear crackling; decrease to reduce latency. Range: 1–8 is typical. |
| `audioDevice` | string | `""` | Reserved for future use. Currently the audio system tries WASAPI shared mode, then falls back to WaveOut automatically. |
| `volume` | integer | `100` | Master volume for game audio (0–100). Adjustable in the Sound menu. |
| `audioFilter` | string | `"Default"` | Audio filter applied to the NES audio output. `"Default"` — standard NES filter chain (HP@37Hz → HP@39Hz → LP@14kHz). `"Warm"` — adds a LP@8kHz stage for warmer sound on modern speakers. `"PseudoStereo"` — Haas-effect stereo widening from the mono source. `"WarmStereo"` — PseudoStereo + Warm lowpass combined. `"Compression"` — soft look-ahead compression to even out DPCM channel spikes. `"BassBoost"` — additive low-shelf boost at 150 Hz (+4 dB DC, ~+2 dB at 150 Hz) on top of the standard NES filter, for fuller sound on bass-light speakers. `"Saturation"` — tanh soft-clip applied after the NES filter chain; super-linear below full scale (mild mid-level boost) with smooth limiting at peaks. Unknown values throw a startup error. |
| `mainMenuMusicVolume` | integer | `100` | Volume for main menu music (0–100), independent of the game audio `volume` field. Setting one does not affect the other. |
| `mainMenuMusicEnabled` | boolean | `true` | When `false`, silences the main menu music regardless of `mainMenuMusicPath`. |
| `mainMenuMusicPath` | string | `""` | Path to an audio file (MP3, WAV) played on the pre-game main menu. Looping. Leave empty to disable. |
| ~~`soundScrubberEnabled`~~ | boolean | `false` | **Deprecated.** Use `audioFilter: "Warm"` instead. If `true` and `audioFilter` is still `"Default"`, the config loader promotes it to `"Warm"` automatically. |

---

## Video settings

| Field | Type | Default | Description |
|---|---|---|---|
| `videoFilter` | string | `"PixelPerfect"` | Structural video filter applied to the NES framebuffer before display. `"NearestNeighbour"` / `"PixelPerfect"` — pixel-perfect nearest-neighbour scaling with 8:7 pixel aspect ratio correction (`"NearestNeighbour"` is a legacy alias for `"PixelPerfect"`). `"Bilinear"` — bilinear interpolation (GDI+ and D3D11). `"CrtScanlines"` — alternating scanline darkening shader (D3D11 only). `"CrtPhosphor"` — scanlines plus aperture-grille phosphor mask (D3D11 only). `"NtscComposite"` — NTSC composite simulation shader with chroma smearing and noise (D3D11 only). If a D3D11-only filter is selected but D3D11 is unavailable, NEShim logs a warning, falls back to `PixelPerfect`, and saves the fallback to `config.json`. Unknown values throw a startup error. See [Filters](filters.md). |
| `videoColorFilter` | string | `"None"` | Color-grade effect applied after the structural filter (D3D11 only; stored but inactive in GDI+ mode). `"None"` — no transform. `"Warm"` — slight amber tint with reduced blues. `"Greyscale"` — full desaturation using BT.601 luma coefficients. `"NesColorCorrection"` — small color-correction matrix for more accurate 2C02 → sRGB output. `"Cool"` — blue-green tint approximating the D93 9300K CRT white point. Unknown values throw a startup error. See [Filters](filters.md). |
| `overscanMode` | string | `"Auto"` | Controls how the NES PPU's 240-scanline output is cropped. `"Auto"` — NTSC crop (top and bottom 8 rows hidden, 224 rows displayed); correct default for the vast majority of NES games. `"NTSC"` — same as Auto, explicit. `"None"` — display all 240 rows. |
| ~~`graphicsSmoothingEnabled`~~ | boolean | `false` | **Deprecated.** Use `videoFilter: "Bilinear"` instead. If `true` and `videoFilter` is still `"NearestNeighbour"`, the config loader promotes it to `"Bilinear"` automatically. |
| `mainMenuBackgroundPath` | string | `""` | Path to an image file shown as the background on the pre-game main menu. Relative to exe or absolute. |
| `sidebarLeftPath` | string | `""` | Path to an image drawn in the left letterbox bar during gameplay. Scaled to fill the full bar area (cover, maintaining aspect ratio), centered, with any overflow cropped. Leave empty for black bars. |
| `sidebarRightPath` | string | `""` | Path to an image drawn in the right letterbox bar during gameplay. Same scaling rules as the left bar. |
| `mainMenuPosition` | string | `"BottomCenter"` | Position of the menu panel on the main menu screen. Accepted values: `"BottomCenter"`, `"Center"`, `"BottomLeft"`, `"BottomRight"`, `"TopLeft"`, `"TopCenter"`, `"TopRight"`. |
| `showFps` | boolean | `false` | Displays a live FPS counter in the top-right corner during gameplay. Toggleable in the Video menu. |
| `noLogo` | boolean | `false` | When `true`, skips the logo splash screen shown at startup. |

---

## Input settings

### Keyboard and gamepad button mappings

`inputMappings` is a dictionary mapping NES button names to a keyboard key name and/or a gamepad button name. Both are optional — you can have keyboard-only or gamepad-only bindings.

```json
"inputMappings": {
  "P1 Up":     { "key": "W",          "gamepadButton": "DPadUp" },
  "P1 Down":   { "key": "S",          "gamepadButton": "DPadDown" },
  "P1 Left":   { "key": "A",          "gamepadButton": "DPadLeft" },
  "P1 Right":  { "key": "D",          "gamepadButton": "DPadRight" },
  "P1 A":      { "key": "OemPeriod",  "gamepadButton": "A" },
  "P1 B":      { "key": "OemComma",   "gamepadButton": "B" },
  "P1 Start":  { "key": "Return",     "gamepadButton": "Y" },
  "P1 Select": { "key": "RShiftKey",  "gamepadButton": "Back" }
}
```

**Key names** are values from the `System.Windows.Forms.Keys` enum (e.g. `"W"`, `"Return"`, `"OemPeriod"`, `"Space"`, `"NumPad1"`). The in-game menu's keyboard rebind screen writes these for you.

**Gamepad button names** for XInput are: `A`, `B`, `X`, `Y`, `Start`, `Back`, `LeftShoulder`, `RightShoulder`, `LeftThumb`, `RightThumb`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`. **`Start` is reserved by default** — it always opens/closes the pause menu and cannot be bound to a NES button. Set `overrideStartBindingProtection: true` to allow rebinding it.

When a **Steam Input controller** is connected, the `gamepadButton` fields in this map are ignored for that controller. Input comes from the Steam Input action set instead. See [Input system — Steam Input](input.md#steam-input).

### Gamepad deadzone

| Field | Type | Default | Description |
|---|---|---|---|
| `gamepadDeadzone` | integer | `8000` | Analog stick deadzone threshold for XInput (raw axis value, ±32767 max). Increase if the character drifts without input. |

### Hotkey mappings

`hotkeyMappings` maps action names to keyboard key names. These are not NES button presses — they are system-level shortcuts processed before the emulator sees input.

```json
"hotkeyMappings": {
  "SaveActiveSlot": "F5",
  "LoadActiveSlot": "F9",
  "SelectSlot1":    "F1",
  "SelectSlot2":    "F2",
  "SelectSlot3":    "F3",
  "SelectSlot4":    "F4",
  "SelectSlot5":    "F6",
  "SelectSlot6":    "F7",
  "SelectSlot7":    "F8",
  "SelectSlot8":    "F12",
  "ToggleWindow":   "F11"
}
```

**`OpenMenu` is not in `hotkeyMappings`** — it is always triggered by **Escape** (keyboard), **Start** (gamepad, by default), or the configured `gamepadHotkeyMappings` entry. Escape is always reserved. Start is reserved by default; set `overrideStartBindingProtection: true` to allow rebinding it, which also exposes an **Open Menu** rebind entry in the gamepad bindings screen.

### Gamepad hotkey mappings

`gamepadHotkeyMappings` maps action names to XInput button names for gamepad-triggered system shortcuts.

```json
"gamepadHotkeyMappings": {
  "OpenMenu": "LeftShoulder"
}
```

This is separate from `inputMappings` — hotkeys are edge-triggered system actions; input mappings are held-down NES button presses.

---

## Developer / diagnostic settings

These fields are not exposed in any in-game menu. They are intended for publishers building and tuning a specific release. Set them directly in `config.json`.

| Field | Type | Default | Description |
|---|---|---|---|
| `enableLogging` | boolean | `false` | When `true`, diagnostic output is appended to `neshim.log` in the executable directory. Useful for debugging startup, audio, or Steam handshake issues. **Do not ship with this enabled** — it creates a log file on the player's machine. |
| `forceRenderer` | string | `"auto"` | Forces a specific rendering backend. `"auto"` — try D3D11 first, fall back to GDI+ if init fails (default). `"gdi"` — always use GDI+ (useful when isolating D3D11-specific issues). `"d3d11"` — prefer D3D11, still falls back to GDI+ if D3D11 init throws. Not exposed in any menu. |
| `region` | string | `"Auto"` | NES emulation region. Controls CPU clock rate, PPU scanline timing, APU frame counter, and the VSync rate used by the frame-timing loop. `"Auto"` detects from the ROM's iNES header (correct for most ROMs). `"NTSC"` forces ~60.099 Hz; `"PAL"` forces ~50.007 Hz; `"Dendy"` forces ~49.99 Hz (Russian clone variant). |
| `analogStickMode` | string | `"Cardinal"` | How the left analog stick maps to the NES D-pad when both axes exceed the deadzone simultaneously. `"Cardinal"` (default) — the dominant axis wins; only the axis with the larger absolute value registers. Prevents accidental diagonals in games with 4-directional movement. `"Diagonal"` — both axes register simultaneously, enabling true diagonal input for games with 8-directional movement. |
| `achievementPublicKey` | string | `""` | ECDSA-P256 public key (SubjectPublicKeyInfo DER format, base64-encoded) used to verify achievement signatures at runtime. Used when no key is embedded in the binary at build time (`AchievementSigner.EmbeddedPublicKeyBase64`). When both are absent, no achievements fire. Set to the public half printed by `seal-achievements --gen-keypair`. See [Achievement system — Key management](achievements.md#key-management). |
| `language` | string | `"Auto"` | Menu language to use when Steam is not running. Accepts any Steam language code: `"english"`, `"french"`, `"german"`, `"spanish"`, `"japanese"`, `"korean"`, `"russian"`, `"schinese"`, `"portuguese"`. `"Auto"` falls back to English. **Ignored when Steam is running** — Steam's game language setting always takes precedence. See [Localization](localization.md). |
| `overrideStartBindingProtection` | boolean | `false` | When `true`, the Start button is no longer reserved as the system menu trigger and can be rebound to a NES button via the gamepad rebind screen. The menu remains accessible via Escape and the `gamepadHotkeyMappings["OpenMenu"]` button (Left Bumper by default). An additional **Open Menu** rebind entry appears in the gamepad bindings screen, visually separated from NES button bindings under a "SYSTEM" section label, so the player can reassign that hotkey as well. |

### Steam Deck / Proton

NEShim runs on Steam Deck via Proton with no configuration changes required. The game detects Wine/Proton at startup for diagnostic logging purposes.

---

## Full example `config.json`

```json
{
  "romPath": "mygame.nes",
  "windowTitle": "My Awesome NES Game",
  "windowMode": "Fullscreen",
  "saveStateDirectory": "saves",
  "saveRamPath": "game.srm",
  "activeSlot": 0,
  "audioBufferFrames": 3,
  "audioDevice": "",
  "gamepadDeadzone": 8000,
  "inputMappings": {
    "P1 Up":     { "key": "W",         "gamepadButton": "DPadUp" },
    "P1 Down":   { "key": "S",         "gamepadButton": "DPadDown" },
    "P1 Left":   { "key": "A",         "gamepadButton": "DPadLeft" },
    "P1 Right":  { "key": "D",         "gamepadButton": "DPadRight" },
    "P1 A":      { "key": "OemPeriod", "gamepadButton": "A" },
    "P1 B":      { "key": "OemComma",  "gamepadButton": "B" },
    "P1 Start":  { "key": "Return",    "gamepadButton": "Y" },
    "P1 Select": { "key": "RShiftKey", "gamepadButton": "Back" }
  },
  "gamepadHotkeyMappings": {
    "OpenMenu": "LeftShoulder"
  },
  "hotkeyMappings": {
    "SaveActiveSlot": "F5",
    "LoadActiveSlot": "F9",
    "SelectSlot1":    "F1",
    "SelectSlot2":    "F2",
    "SelectSlot3":    "F3",
    "SelectSlot4":    "F4",
    "SelectSlot5":    "F6",
    "SelectSlot6":    "F7",
    "SelectSlot7":    "F8",
    "SelectSlot8":    "F12",
    "ToggleWindow":   "F11"
  },
  "mainMenuBackgroundPath": "art/menu_bg.png",
  "sidebarLeftPath":  "art/sidebar_left.png",
  "sidebarRightPath": "art/sidebar_right.png",
  "mainMenuMusicPath": "audio/menu_theme.mp3",
  "volume": 80,
  "audioFilter": "Default",
  "mainMenuMusicVolume": 100,
  "mainMenuMusicEnabled": true,
  "videoFilter": "PixelPerfect",
  "videoColorFilter": "None",
  "overscanMode": "Auto",
  "mainMenuPosition": "BottomCenter",
  "showFps": false,

  "_comment_developer_settings": "The fields below are developer-only and not exposed in any menu.",
  "enableLogging": false,
  "forceRenderer": "auto",
  "region": "Auto",
  "analogStickMode": "Cardinal",
  "achievementPublicKey": "",
  "language": "Auto",
  "overrideStartBindingProtection": false
}
```
