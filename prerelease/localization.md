---
layout: default
title: Localization
nav_order: 6
parent: Pre-release
description: "Language support, custom language files, Steam language detection, and CJK font fallback."
permalink: /prerelease/localization/
---

# Localization

NEShim supports multiple UI languages through JSON language files shipped alongside the executable. The active language is detected from Steam at startup; the `language` config field provides a fallback for non-Steam publishing scenarios.

---

## Overview

At startup, NEShim resolves the active language in this order:

1. **Steam** — if Steam is running, `SteamApps.GetCurrentGameLanguage()` returns the current game language (e.g. `"french"`). This takes precedence over everything else.
2. **`config.json` `language` field** — used only when Steam is not available and the field is not `"Auto"`.
3. **English** — the built-in default, used when neither source provides a value.

NEShim then loads `lang/<language>.json` from the directory alongside the executable. If the file does not exist, it falls back to `lang/english.json`. If that is also missing, it falls back to built-in English defaults with no file I/O.

---

## Built-in languages

| Steam language code | Language |
|---|---|
| `english` | English |
| `french` | French |
| `german` | German |
| `spanish` | Spanish |
| `japanese` | Japanese |
| `korean` | Korean |
| `russian` | Russian |
| `schinese` | Simplified Chinese |
| `portuguese` | Brazilian Portuguese |

---

## Config reference

```json
{
  "language": "Auto"
}
```

| Value | Behaviour |
|---|---|
| `"Auto"` (default) | Use English when Steam is not running |
| Any Steam language code | Use that language when Steam is not running |

> **Note:** This field is ignored when Steam is running. Steam's game language setting always takes precedence.

---

## Language file format

Each language file is a JSON object with camelCase keys corresponding to `LocalizationData` properties. Unrecognised keys are silently ignored. Missing keys fall back to their English defaults — a partially-translated file works fine.

**Example `lang/english.json` (abridged):**

```json
{
  "fontFamily": "Segoe UI",
  "back": "← Back",
  "settingsTitle": "SETTINGS",
  "videoTitle": "VIDEO",
  "soundTitle": "SOUND",
  "settingsKeyboard": "Keyboard Controls",
  "settingsGamepad": "Gamepad Controls",
  "settingsVideo": "Video",
  "settingsSound": "Sound",
  "videoWindowFullscreen": "Window Mode: Fullscreen",
  "videoWindowWindowed": "Window Mode: Windowed",
  "videoGraphicsSmooth": "Graphics: Smooth",
  "videoGraphicsOriginal": "Graphics: Original",
  "videoFpsOn": "FPS Overlay: On",
  "videoFpsOff": "FPS Overlay: Off",
  "soundVolume": "◀  Volume: {0}  ▶",
  "soundScrubberOn": "Sound Scrubber: On",
  "soundScrubberOff": "Sound Scrubber: Off",
  "soundMusicOn": "Menu Music: On",
  "soundMusicOff": "Menu Music: Off",
  "pressKeyTitle": "PRESS KEY FOR  {0}",
  "pressButtonTitle": "PRESS BUTTON FOR  {0}",
  "bindUp": "Up",
  "bindDown": "Down",
  "bindLeft": "Left",
  "bindRight": "Right",
  "bindA": "A",
  "bindB": "B",
  "bindStart": "Start",
  "bindSelect": "Select",
  "slotLabel": "Slot {0}",
  "slotNoSave": "  (no save)",
  "slotActive": "  ◀ active",
  "slotAutoSave": "Auto Save",
  "mainMenuTitle": "MAIN MENU",
  "mainMenuLoadTitle": "LOAD GAME",
  "mainMenuNewGame": "New Game",
  "mainMenuResumeGame": "Resume Game",
  "mainMenuSettings": "Settings",
  "mainMenuExit": "Exit",
  "mainMenuRebindPressKey": "Press any key  •  Esc to cancel",
  "mainMenuRebindPressButton": "Press any controller button  •  Start to cancel",
  "inGamePausedTitle": "PAUSED",
  "inGameSelectSlotTitle": "SELECT SLOT  (active: {0})",
  "inGameLoadTitle": "LOAD GAME?",
  "inGameReturnTitle": "RETURN TO MAIN MENU?",
  "inGameExitTitle": "EXIT TO DESKTOP?",
  "inGameResume": "Resume",
  "inGameResetGame": "Reset Game",
  "inGameSelectSaveSlot": "Select Save Slot",
  "inGameSaveGame": "Save Game",
  "inGameLoadGame": "Load Game",
  "inGameSettings": "Settings",
  "inGameReturnToMain": "Return to Main Menu",
  "inGameExit": "Exit",
  "inGameConfirmYesLoad": "Yes, load game",
  "inGameConfirmNoStay": "No, stay in game",
  "inGameConfirmYesReturn": "Yes, return to main menu",
  "inGameConfirmYesExit": "Yes, exit to desktop",
  "inGameConfirmWarning": "Unsaved progress will be lost.",
  "inGameRebindPressKey": "Press any key to bind\n(Esc to cancel)",
  "inGameRebindPressButton": "Press any controller button\n(Start to cancel)",
  "inGameRebindStartReserved": "Start is reserved for the menu"
}
```

Format strings (e.g. `"Slot {0}"`, `"◀  Volume: {0}  ▶"`) must keep the `{0}` placeholder — it is substituted at runtime with the slot number or volume level.

The window title is **not** localized. Set `windowTitle` in `config.json` independently.

---

## Adding a custom language

1. Create `lang/<code>.json` alongside the executable, where `<code>` is a Steam language code.
2. Include only the keys you want to translate. Any key absent from the file uses its English default.
3. Set `fontFamily` to a font that covers the target script (see [Font support](#font-support) below).
4. Set `"language": "<code>"` in `config.json` if you want it active without Steam, or rely on Steam's language setting.

Any Steam language code is valid as a file name — NEShim will load it when Steam reports that language.

---

## Testing localization locally

You do not need a live Steam session to test a specific language. Set the `language` field in `config.json` and launch the executable directly:

```json
{
  "language": "french"
}
```

NEShim will load `lang/french.json` and render all menu text in French. Steam is not involved when the exe is launched outside Steam, so the config value takes effect.

To verify a CJK language, launch with `"language": "japanese"` (or `"korean"`, `"schinese"`) and confirm that menu text renders legibly. If the specified `fontFamily` is not installed, Windows substitutes a fallback — check that glyphs are not rendered as blank boxes.

To confirm the fallback chain works, temporarily rename or remove a language file. With `"language": "french"` and no `french.json`, the game should fall back to `english.json`. Remove `english.json` as well and the game should fall back to built-in English defaults.

---

## Font support

The `fontFamily` key in each language file controls the GDI+ font used to render all menu text. GDI+ resolves glyphs through Windows font fallback, so Latin, Cyrillic, and Greek scripts render correctly with `"Segoe UI"` (available on all supported Windows versions).

CJK languages ship with platform-specific font families:

| Language | `fontFamily` |
|---|---|
| Japanese | `"Yu Gothic UI"` |
| Korean | `"Malgun Gothic"` |
| Simplified Chinese | `"Microsoft YaHei UI"` |

If the specified font is not installed on the system, Windows will silently substitute a fallback that covers the required glyphs.

When adding a new CJK language, set `fontFamily` to the appropriate platform font for that script. For Traditional Chinese, `"Microsoft JhengHei UI"` is the recommended value.

---

## RTL note

Arabic and Hebrew characters render correctly through GDI+'s Unicode bidirectional algorithm without any code changes — text flows right-to-left within drawn strings automatically.

Full layout mirroring (right-aligned panels, reversed navigation order) requires significant renderer restructuring and is not currently implemented. If you add an RTL language, menu text will display correctly but panel alignment and tab order will remain left-to-right.

---

## Steam store localization

Publishing a localized game on Steam involves several steps beyond the language files in your executable.

### 1. Store page descriptions

In the Steamworks partner dashboard, navigate to **Store Presence → Edit Store Page** and add localized descriptions for each supported language. Steam displays these on the store page in the user's language.

### 2. Screenshots and capsule art

Under **Store Presence → Graphical Assets**, upload language-specific screenshots showing localized UI if the game text appears in screenshots. Steam selects assets by the user's store language.

### 3. Supported languages list

In the dashboard under **Store Presence → Edit Store Page → Basic Info**, check each language your game supports. This list controls what appears on the store page under "Languages" and determines which language the Steam client offers to users.

### 4. Steam Input VDF localization

The `game_actions_0.vdf` file (renamed to `game_actions_<AppID>.vdf` before release) contains a `"localization"` block with translated action names for each language. These names appear in the Steam overlay's controller binding UI. The built-in language files already cover all nine shipped languages. When adding a custom language, add a matching block in the VDF using the same Steam language code as a key.

Upload the VDF via the Steamworks dashboard under **Steam Input → Default Configuration**.

### 5. Achievement names

Achievement unlock pop-ups display the achievement's display name as returned by Steam (`SteamUserStats.GetAchievementDisplayAttribute`). Steam returns this name in the current game language automatically — no code changes are needed. To localise achievement names, add translated names and descriptions for each language in the Steamworks partner dashboard under **Achievements → Edit Achievements**. If a translation is missing for the active language, Steam falls back to the English name.

### 6. Game language vs. Steam UI language

The `SteamApps.GetCurrentGameLanguage()` API returns the **game's** configured language — the one set in Steam's game properties dialog (right-click the game → Properties → Language tab). This is independent of the Steam UI language. Users can set the game language separately from their Steam UI language. NEShim reads the game language, not the Steam UI language.
