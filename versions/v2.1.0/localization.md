---
layout: default
title: Localization
nav_order: 7
parent: v2.1.0
description: "Language support, in-game language switcher, custom language files, Steam/OS language detection, and CJK font fallback."
---

# Localization

NEShim supports multiple UI languages through JSON language files shipped alongside the executable. Players can also change the language at any time from the in-game Settings screen.

---

## Selecting a language in-game

Open the in-game menu → **Settings → Language**. The Language screen lists all supported languages by their native name with a flag icon:

- **Auto** — lets the app choose automatically (see [Auto-detection order](#auto-detection-order) below)
- **English**, **Français**, **Deutsch**, **Español**, **Español (Latinoamérica)**, **日本語**, **한국어**, **Русский**, **中文（简体）**, **Português**

Selecting a language writes it to `config.json` immediately and reloads all menu text without restarting. A `✓` appears next to the active choice. Selecting **Auto** clears the override and restores automatic detection.

---

## Auto-detection order

When `config.json` contains `"language": "Auto"` (or no language field at all), NEShim resolves the language at startup in this order:

1. **Steam game language** — if Steam is running, `SteamApps.GetCurrentGameLanguage()` is checked first. This is the language set in the game's Properties dialog in Steam, not the Steam UI language. The raw Steam code is normalized to NEShim's internal code before use: `koreana` → `korean`, `brazilian` → `portuguese`. Steam codes for unsupported languages (e.g. `tchinese`, `arabic`) return no match and fall through to step 2.
2. **OS UI culture** — if Steam is unavailable or returned no match, `CultureInfo.CurrentUICulture` is checked. Full culture names take priority over two-letter codes so that LatAm locales resolve correctly (`es-MX` → `latam`, `es-ES` → `spanish`). Simplified Chinese matches on explicit culture names (`zh-CN`, `zh-Hans`, etc.); Traditional Chinese (`zh-TW`, `zh-Hant`) does not match any supported language and falls through to step 3.
3. **English** — the built-in default, used when no resolver returns a match.

Every resolver decision is written to the diagnostic log so you can trace exactly which path was taken.

> **Explicit selection overrides Steam.** If the user picks a language in the Language screen (or you pre-set `"language"` in `config.json` to a specific code), that value takes priority over Steam — even when Steam is running. Auto mode is the only mode where Steam is consulted.

NEShim then loads `lang/<language>.json` from the directory alongside the executable. If the file does not exist it falls back to `lang/english.json`. If that is also missing it falls back to built-in English defaults with no file I/O.

---

## Built-in languages

| Code (lang file) | Native name | Steam API code | Culture matching |
|---|---|---|---|
| `english` | English | `english` | `en` |
| `french` | Français | `french` | `fr` |
| `german` | Deutsch | `german` | `de` |
| `spanish` | Español | `spanish` | `es` (fallback for unlisted Spanish locales) |
| `latam` | Español (Latinoamérica) | `latam` | `es-MX`, `es-AR`, `es-CO`, `es-CL`, `es-PE`, `es-VE`, `es-US`, `es-419` |
| `japanese` | 日本語 | `japanese` | `ja` |
| `korean` | 한국어 | `koreana` ¹ | `ko` |
| `russian` | Русский | `russian` | `ru` |
| `schinese` | 中文（简体） | `schinese` | `zh-CN`, `zh-SG`, `zh-Hans`, `zh-Hans-CN`, `zh-Hans-SG` ² |
| `portuguese` | Português | `portuguese`, `brazilian` ³ | `pt` |

¹ Steam's API returns `koreana` for Korean (a legacy quirk). NEShim maps this automatically to the `korean` lang file — you do not need a `koreana.json`.

² Traditional Chinese (`zh-TW`, `zh-Hant`, `zh-HK`) is not a supported language and falls back to English. The bare `zh` two-letter code is intentionally not used because `zh-Hant` shares the same `TwoLetterISOLanguageName` value and would incorrectly match Simplified Chinese.

³ Steam's `brazilian` code (Brazilian Portuguese) is mapped to `portuguese` since NEShim ships one Portuguese locale. Brazilian Steam users receive European Portuguese text.

> **Spanish vs. Latin American Spanish:** Steam's `latam` language code targets Latin American Spanish separately from `spanish` (Spain). Culture-based auto-detection uses full locale names first: `es-MX`, `es-AR`, and other listed locales resolve to `latam`; `es-ES` (and any unlisted `es-*` locale) falls back to `spanish`.

---

## Config reference

```json
{
  "language": "Auto"
}
```

| Value | Behaviour |
|---|---|
| `"Auto"` (default) | Resolve automatically: Steam → OS culture → English |
| Any Steam language code | Use that language; overrides Steam even when Steam is running |

This field is written automatically when the player selects a language from the in-game Language screen.

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

NEShim will load `lang/french.json` and render all menu text in French. Because an explicit language overrides Steam, this works regardless of whether Steam is running.

To test Auto mode with a specific OS culture, set `"language": "Auto"`, temporarily change Windows' display language in System Settings, and relaunch. The diagnostic log will show which resolver fired and which language was chosen.

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

The `game_actions_0.vdf` file (renamed to `game_actions_<AppID>.vdf` before release) contains a `"localization"` block with translated action names for each language. These names appear in the Steam overlay's controller binding UI. The built-in language files already cover all ten shipped languages. When adding a custom language, add a matching block in the VDF using the same Steam language code as a key.

Upload the VDF via the Steamworks dashboard under **Steam Input → Default Configuration**.

### 5. Achievement names

Achievement unlock pop-ups display the achievement's display name as returned by Steam (`SteamUserStats.GetAchievementDisplayAttribute`). Steam returns this name in the current game language automatically — no code changes are needed. To localise achievement names, add translated names and descriptions for each language in the Steamworks partner dashboard under **Achievements → Edit Achievements**. If a translation is missing for the active language, Steam falls back to the English name.

### 6. Game language vs. Steam UI language

The `SteamApps.GetCurrentGameLanguage()` API returns the **game's** configured language — the one set in Steam's game properties dialog (right-click the game → Properties → Language tab). This is independent of the Steam UI language. Users can set the game language separately from their Steam UI language. NEShim reads the game language, not the Steam UI language.
