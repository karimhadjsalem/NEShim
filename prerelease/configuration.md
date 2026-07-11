---
layout: default
title: Configuration
nav_order: 2
parent: Pre-release
description: "Complete config.json reference — every field with type, default, and examples."
---

# Configuration reference

NEShim uses two configuration files:

**`config.json`**, placed alongside the executable, is the publisher configuration layer. It ships in your Steam depot and contains game-specific settings (ROM path, window title, achievement key, artwork paths) plus initial defaults for all player-facing settings. Steam updates may overwrite this file — that is intentional and safe.

**`user.json`**, at `%APPDATA%\<WindowTitle>\user.json` on Windows (or the equivalent Proton Wine prefix path on Steam Deck), stores player preferences written by the in-game menu. Steam never touches this file — it lives outside the install tree. On first launch, it is automatically bootstrapped from the user-settable values in `config.json`, so the player's starting preferences match whatever defaults you shipped.

At startup, both files are merged: `config.json` is loaded first, then `user.json` is overlaid on top — player overrides win. In-game menu changes write only to `user.json`; `config.json` is never modified at runtime. If `config.json` is not found at startup, it is created with all defaults.

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
| `saveRamPath` | string | `"game.srm"` | Path for battery-backed RAM persistence (used by games like Zelda and Metroid). Written on exit only after the player has started a game session. |
| `activeSlot` | integer | `0` | Index of the currently selected save slot (0–7). User-settable — persisted in `user.json` across sessions. |

### Auto-save

NEShim writes `autosave.state` inside `saveStateDirectory` at three points:

- **When the in-game menu opens** — captures the exact state at the moment the player pauses.
- **Every ~5 minutes during active gameplay** — a frame counter fires after approximately 18,000 frames (~5 min at 60 fps).
- **On graceful exit** — written when the window is closed or Exit is chosen from the menu, if the game is running.

No auto-save is written before the player has started a game session. Exiting from the pre-game main menu without ever choosing New Game or Resume produces no auto-save file.

The auto-save file is separate from the eight manual slots and cannot be loaded from within the game. It exists as a recovery file for Steam Cloud — if the player's session ends without a manual save, the auto-save gives Steam something to sync so progress is not lost on the next machine.

On a crash or force-quit, the most recent periodic or menu-triggered save remains on disk. At most ~5 minutes of progress is exposed between periodic saves; a clean shutdown always writes a fresh snapshot on exit regardless of when the last periodic save fired.

There are no config fields to enable, disable, or rename the auto-save file. The path is always `<saveStateDirectory>/autosave.state`.

---

## Audio settings

| Field | Type | Default | Description |
|---|---|---|---|
| `audioBufferFrames` | integer | `3` | Size of the audio ring buffer in frames (~16.67 ms each). Increase if you hear crackling; decrease to reduce latency. Range: 1–8 is typical. |
| `audioDevice` | string | `""` | Accepted but not currently used — the audio system always opens the SDL3 default playback device (`SDL.AudioDeviceDefaultPlayback`). The field is retained for future device-selection support. |
| `volume` | integer | `100` | Master volume for game audio (0–100). Adjustable in the Sound menu. |
| `audioFilter` | string | `"Default"` (`"Saturation"` on Steam Deck first run) | Audio filter applied to the NES audio output. `"Default"` — standard NES filter chain (HP@37Hz → HP@39Hz → LP@14kHz). `"Warm"` — adds a LP@8kHz stage for warmer sound on modern speakers. `"PseudoStereo"` — Haas-effect stereo widening from the mono source. `"WarmStereo"` — PseudoStereo + Warm lowpass combined. `"Compression"` — soft look-ahead compression to even out DPCM channel spikes. `"BassBoost"` — additive low-shelf boost at 150 Hz (+4 dB DC, ~+2 dB at 150 Hz) on top of the standard NES filter, for fuller sound on bass-light speakers. `"Saturation"` — tanh soft-clip applied after the NES filter chain; super-linear below full scale (mild mid-level boost) with smooth limiting at peaks; **recommended for Steam Deck speakers**. `"DmcStabilizer"` — slew-rate limiter applied before the standard NES filter chain that softens large DMC sample-to-sample amplitude jumps, reducing audible pops and clicks from DPCM samples. Unknown values throw a startup error. |
| `mainMenuMusicVolume` | integer | `100` | Volume for main menu music (0–100), independent of the game audio `volume` field. Setting one does not affect the other. |
| `mainMenuMusicEnabled` | boolean | `true` | When `false`, silences the main menu music regardless of `mainMenuMusicPath`. |
| `mainMenuMusicPath` | string | `""` | Path to an audio file (MP3, WAV) played on the pre-game main menu. Looping. Leave empty to disable. |
| ~~`soundScrubberEnabled`~~ | boolean | `false` | **Deprecated.** Use `audioFilter: "Warm"` instead. If `true` and `audioFilter` is still `"Default"`, the config loader promotes it to `"Warm"` automatically. |
| `audioEqBass` | integer | `0` | Bass EQ gain in dB (−12 to +12). Center frequency 100 Hz. 0 = no processing. |
| `audioEqMid` | integer | `0` | Mid EQ gain in dB (−12 to +12). Center frequency 1 kHz. 0 = no processing. |
| `audioEqTreble` | integer | `0` | Treble EQ gain in dB (−12 to +12). Center frequency 8 kHz. 0 = no processing. |

---

## Video settings

| Field | Type | Default | Description |
|---|---|---|---|
| `videoFilter` | string | `"PixelPerfect"` | Structural video filter applied to the NES framebuffer before display. `"NearestNeighbour"` / `"PixelPerfect"` — pixel-perfect nearest-neighbour scaling with 8:7 pixel aspect ratio correction (`"NearestNeighbour"` is a legacy alias for `"PixelPerfect"`). `"Bilinear"` — smooth pixel upscaling via a Jinc2 windowed-sinc filter (displayed as "Smooth" in the menu). `"CrtScanlines"` — alternating scanline darkening shader (shader renderer — D3D11 and SDL_GPU/Vulkan). `"CrtPhosphor"` — scanlines plus aperture-grille phosphor mask (shader renderer — D3D11 and SDL_GPU/Vulkan). `"NtscComposite"` — NTSC composite simulation shader with chroma smearing and noise (shader renderer — D3D11 and SDL_GPU/Vulkan). `"CrtScreen"` — barrel distortion, chromatic aberration, and vignette simulating a curved CRT screen (shader renderer — D3D11 and SDL_GPU/Vulkan). `"Xbr"` — Sharp Pixel: Scale2x/EPX edge-preserving upscaler that sharpens pixel-art edges at sub-pixel precision without blurring flat regions (shader renderer — D3D11 and SDL_GPU/Vulkan). Unknown values throw a startup error. See [Filters](filters.md). |
| `videoFilterOverlay` | string | `"None"` | Second-pass overlay filter rendered on top of the primary `videoFilter` using an intermediate render target (D3D11 only). Eligible values: `"None"` — no overlay (single-pass, zero overhead). `"CrtScanlines"`, `"CrtPhosphor"`, `"CrtScreen"` — composable effects that work on an already-scaled frame. Setting primary and overlay to the same filter is rejected by the menu. See [Filters](filters.md). |
| `videoColorFilter` | string | `"None"` | Color-grade effect applied after the structural filter (D3D11 and SDL_GPU/Vulkan). `"None"` — no transform. `"Warm"` — slight amber tint with reduced blues. `"Greyscale"` — full desaturation using BT.601 luma coefficients. `"NesColorCorrection"` — small color-correction matrix for more accurate 2C02 → sRGB output. `"Cool"` — blue-green tint approximating the D93 9300K CRT white point. `"PhosphorAmber"` — amber phosphor tint. `"PhosphorGreen"` — green phosphor tint. Unknown values throw a startup error. See [Filters](filters.md). |
| `videoMotionEffect` | string | `"None"` | Per-frame motion effect applied during emulation (D3D11 and SDL_GPU/Vulkan; PhosphorPersistence is D3D11 only). `"None"` — no effect. `"CrtJitter"` — subtle frame-to-frame horizontal position jitter simulating CRT convergence instability. `"ScanlineBob"` — alternates the vertical offset of odd/even frames to simulate interlaced scanline bobbing. `"MagneticDistortion"` — per-pixel sine-wave UV warp simulating magnetic interference on a CRT; runs as a dedicated pixel shader pass using an intermediate render target. `"PhosphorPersistence"` — temporally accumulates frames with 65% per-frame retention to simulate CRT phosphor persistence, producing an after-image trail that fades over ~10–15 frames; runs as a shader pass with a ping-pong temporal buffer. D3D11 only — selecting this on SDL_GPU/Vulkan silently demotes to None. Unknown values throw a startup error. |
| `overscanMode` | string | `"Normal"` | Controls how the NES PPU's 240-scanline output is cropped. `"Normal"` — display all 240 rows (default). `"Overscan"` — NTSC crop (top and bottom 8 rows hidden, 224 rows displayed); matches original NTSC TV output. `"Underscan"` — display all 240 rows but scale the image to 88% of the window, leaving a uniform black border on all sides. Legacy values `"Auto"` and `"NTSC"` map to `"Overscan"`; `"None"` maps to `"Normal"`. |
| `videoBrightness` | integer | `0` | Additive brightness post-process applied after all structural filter passes (D3D11 and SDL_GPU/Vulkan). Range: −100 to +100. 0 = neutral. Configurable via Settings → Video → Picture. |
| `videoContrast` | integer | `0` | Contrast post-process scaling RGB around mid-grey (D3D11 and SDL_GPU/Vulkan). Range: −100 to +100. 0 = neutral (1× scale). Configurable via Settings → Video → Picture. |
| `videoSaturation` | integer | `0` | Saturation post-process blending between greyscale (−100) and boosted colour (+100) (D3D11 and SDL_GPU/Vulkan). Range: −100 to +100. 0 = neutral. Configurable via Settings → Video → Picture. |
| `videoHue` | integer | `0` | Hue rotation applied to all colours using Rodrigues' rotation around the grey axis (D3D11 and SDL_GPU/Vulkan). Range: −100 to +100 (−100 = −π rad ≈ full complementary inversion; +100 = +π rad). 0 = neutral. Configurable via Settings → Video → Picture. |
| `videoPreset` | string | `"None"` | Name of the last-applied video preset. `"None"` — no preset active. `"LivingRoom"`, `"Arcade"`, `"Sharp"`, `"Phosphor"` — built-in presets. Written by the Presets sub-menu (hidden from the in-game menu when D3D11 is not active); cleared to `"None"` automatically whenever any individual video setting is changed manually. On SDL_GPU/Vulkan, the Video Overlay and Screen Glow components of a preset are not applied (see [Filters — Video Presets](filters.md#video-presets)); all other preset fields take effect. |
| ~~`graphicsSmoothingEnabled`~~ | boolean | `false` | **Deprecated.** Use `videoFilter: "Bilinear"` instead. If `true` and `videoFilter` is still `"NearestNeighbour"`, the config loader promotes it to `"Bilinear"` automatically. |
| `mainMenuBackgroundPath` | string | `""` | Path to an image file shown as the background on the pre-game main menu. Relative to exe or absolute. The image is stretched to fill the entire window — design at your target resolution to avoid aspect-ratio distortion. **1920×1080** for 16:9 fullscreen; **1280×800** for Steam Deck fullscreen. See [Main menu background sizing](#main-menu-background-sizing) below. |
| `sidebarLeftPath` | string | `""` | Path to an image drawn in the left letterbox bar during gameplay. Scaled to fill the full bar area (cover, maintaining aspect ratio), centered, with any overflow cropped. Leave empty for black bars. See [Sidebar image sizing](#sidebar-image-sizing) below. |
| `sidebarRightPath` | string | `""` | Path to an image drawn in the right letterbox bar during gameplay. Same scaling rules as the left bar. See [Sidebar image sizing](#sidebar-image-sizing) below. |
| `mainMenuPosition` | string | `"BottomCenter"` | Position of the menu panel on the main menu screen. Accepted values: `"BottomCenter"`, `"Center"`, `"BottomLeft"`, `"BottomRight"`, `"TopLeft"`, `"TopCenter"`, `"TopRight"`. |
| `showFps` | boolean | `false` | Displays a live FPS counter in the top-right corner during gameplay. Toggleable in the Video menu. |
| `noLogo` | boolean | `false` | When `true`, skips the logo splash screen shown at startup. |

### Main menu background sizing

The background image is stretched to fill the entire window with no cropping. If the source image has a different aspect ratio than the window, it will be visibly distorted. Design at the aspect ratio your players will most commonly use:

| Target | Recommended canvas |
|---|---|
| 16:9 fullscreen (most PC monitors) | **1920×1080 px** |
| 16:10 fullscreen (Steam Deck) | **1280×800 px** |
| Both | Provide a 1920×1080 image — the minor vertical compression on Steam Deck (~11%) is usually imperceptible for background art |

### Sidebar image sizing

Each sidebar bar spans the full window height and is a narrow portrait strip flanking the NES frame. Its pixel width is `(window_width − NES_frame_width) ÷ 2`. The NES frame's display width depends on the active filter's pixel aspect ratio (8:7 for Pixel Perfect and the CRT/NTSC/Sharp filters) and the number of visible scanlines set by `overscanMode`.

With the default **Pixel Perfect (8:7 PAR)** filter on a **16:9 display**:

| Resolution | `overscanMode: "Normal"` (240 rows) | `overscanMode: "Overscan"` (224 rows) |
|---|---|---|
| 1280×720 (720p) | ~201×720 px &nbsp; (1:3.6 portrait) | ~170×720 px &nbsp; (1:4.2 portrait) |
| 1920×1080 (1080p) | ~302×1080 px &nbsp; (1:3.6 portrait) | ~254×1080 px &nbsp; (1:4.3 portrait) |
| 2560×1440 (1440p) | ~402×1440 px &nbsp; (1:3.6 portrait) | ~339×1440 px &nbsp; (1:4.2 portrait) |
| 1280×800 (Steam Deck, 16:10) | ~152×800 px &nbsp; (1:5.3 portrait) | ~118×800 px &nbsp; (1:6.8 portrait) |

The aspect ratio is constant across resolutions for a given configuration — only the pixel count scales.

**Design recommendations:**

- Images are cover-scaled (scale uniformly until both dimensions are filled, then crop overflow), so the source pixel count does not need to match the runtime bar exactly — design at the correct aspect ratio and any convenient canvas size.
- **For 16:9 PC displays with Normal overscan (the default):** target **1:3.6 portrait**. A canvas of **200×720 px**, **302×1080 px**, or **402×1440 px** fills the bar with no cropping.
- **On Steam Deck (16:10):** bars are narrower relative to their height (~1:5.3 portrait), so 16:9-designed art will have some left and right content cropped at runtime. Keep key visual elements near the centre column of your sidebar art.
- Sidebar art is always displayed at its original colours — structural filters and Color Effects do not apply to sidebar images.

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
  "P1 A":      { "key": "Period",  "gamepadButton": "A" },
  "P1 B":      { "key": "Comma",   "gamepadButton": "B" },
  "P1 Start":  { "key": "Return",  "gamepadButton": "Y" },
  "P1 Select": { "key": "RShift",  "gamepadButton": "Back" }
}
```

**Key names** are `SDL.Keycode` enum member names (e.g. `"W"`, `"Return"`, `"Period"`, `"Space"`, `"Kp1"`). The in-game menu's keyboard rebind screen writes these for you — there is no need to look up names manually. Common names: letter keys `"A"`–`"Z"`; arrow keys `"Up"`, `"Down"`, `"Left"`, `"Right"`; `"Return"`, `"Escape"`, `"Space"`, `"Backspace"`, `"LShift"`, `"RShift"`, `"Period"`, `"Comma"`, `"F1"`–`"F12"`, digit row `"Alpha0"`–`"Alpha9"`, numpad `"Kp0"`–`"Kp9"`. Note: these names differ from the old `System.Windows.Forms.Keys` names — `"OemPeriod"`, `"OemComma"`, `"RShiftKey"` are no longer valid; use `"Period"`, `"Comma"`, `"RShift"`.

**Gamepad button names** for SDL3 are: `A`, `B`, `X`, `Y`, `Start`, `Back`, `LeftShoulder`, `RightShoulder`, `LeftThumb`, `RightThumb`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`. **`Start` is reserved by default** — it always opens/closes the pause menu and cannot be bound to a NES button. Set `overrideStartBindingProtection: true` to allow rebinding it.

When a **Steam Input controller** is connected, the `gamepadButton` fields in this map are ignored for that controller. Input comes from the Steam Input action set instead. See [Input system — Steam Input](input.md#steam-input).

### Gamepad deadzone

| Field | Type | Default | Description |
|---|---|---|---|
| `gamepadDeadzone` | integer | `8000` | Analog stick deadzone threshold for SDL3 gamepad (raw axis value, ±32767 max). Increase if the character drifts without input. |

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

`gamepadHotkeyMappings` maps action names to SDL3 button names for gamepad-triggered system shortcuts.

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
| `region` | string | `"Auto"` | NES emulation region. Controls CPU clock rate, PPU scanline timing, APU frame counter, and the VSync rate used by the frame-timing loop. `"Auto"` detects from the ROM's iNES header (correct for most ROMs). `"NTSC"` forces ~60.099 Hz; `"PAL"` forces ~50.007 Hz; `"Dendy"` forces ~49.99 Hz (Russian clone variant). |
| `analogStickMode` | string | `"Cardinal"` | How the left analog stick maps to the NES D-pad when both axes exceed the deadzone simultaneously. `"Cardinal"` (default) — the dominant axis wins; only the axis with the larger absolute value registers. Prevents accidental diagonals in games with 4-directional movement. `"Diagonal"` — both axes register simultaneously, enabling true diagonal input for games with 8-directional movement. |
| `achievementPublicKey` | string | `""` | ECDSA-P256 public key (SubjectPublicKeyInfo DER format, base64-encoded) used to verify achievement signatures at runtime. Used when no key is embedded in the binary at build time (`AchievementSigner.EmbeddedPublicKeyBase64`). When both are absent, no achievements fire. Set to the public half printed by `seal-achievements --gen-keypair`. See [Achievement system — Key management](achievements.md#key-management). |
| `language` | string | `"Auto"` | Active menu language. Accepts any Steam language code: `"english"`, `"french"`, `"german"`, `"spanish"`, `"latam"`, `"japanese"`, `"korean"`, `"russian"`, `"schinese"`, `"portuguese"`, or `"Auto"`. When `"Auto"`, the language is resolved at startup in order: Steam game language → OS UI culture (`CultureInfo.CurrentUICulture`) → English. **Any explicit value overrides Steam** — even when Steam is running, an explicit language setting wins. This field is written automatically to `user.json` when the user picks a language in **Settings → Language**; set it manually to pre-configure the language for a game build. See [Localization](localization.md). |
| `overrideStartBindingProtection` | boolean | `false` | When `true`, the Start button is no longer reserved as the system menu trigger and can be rebound to a NES button via the gamepad rebind screen. The menu remains accessible via Escape and the `gamepadHotkeyMappings["OpenMenu"]` button (Left Bumper by default). An additional **Open Menu** rebind entry appears in the gamepad bindings screen, visually separated from NES button bindings under a "SYSTEM" section label, so the player can reassign that hotkey as well. |

### Steam Deck / Proton

NEShim runs on Steam Deck via Proton with no configuration changes required. The game detects Wine/Proton at startup for diagnostic logging purposes.

---

## Full example `config.json`

This is a complete publisher configuration template. All fields are optional — omit any you want to leave at the default. User-settable fields (volume, video/audio filters, language, input bindings, etc.) listed here become the player's starting defaults: on first launch they are copied to `user.json` in AppData, after which the player's in-game changes are stored there and this file is no longer consulted for those fields.

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
    "P1 A":      { "key": "Period", "gamepadButton": "A" },
    "P1 B":      { "key": "Comma",  "gamepadButton": "B" },
    "P1 Start":  { "key": "Return", "gamepadButton": "Y" },
    "P1 Select": { "key": "RShift", "gamepadButton": "Back" }
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
  "audioEqBass": 0,
  "audioEqMid": 0,
  "audioEqTreble": 0,
  "mainMenuMusicVolume": 100,
  "mainMenuMusicEnabled": true,
  "videoFilter": "PixelPerfect",
  "videoFilterOverlay": "None",
  "videoColorFilter": "None",
  "videoMotionEffect": "None",
  "videoBrightness": 0,
  "videoContrast": 0,
  "videoSaturation": 0,
  "videoHue": 0,
  "videoPreset": "None",
  "overscanMode": "Normal",
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
