---
layout: default
title: Input
nav_order: 6
parent: Pre-release
description: "Keyboard remapping, SDL3 gamepad, Steam Input, hotkeys, and the VDF action-set file."
---

# Input system

NEShim supports three input sources that are combined every frame: keyboard, SDL3 gamepads, and Steam Input controllers. This page covers how each source works, how they interact, and how to configure them.

---

## Overview

Every emulation frame, `InputManager.PollSnapshot()` produces an `InputSnapshot` — an immutable set of NES button names that are currently pressed. The snapshot merges all three sources, with duplicates deduplicated harmlessly by the `ImmutableHashSet` builder:

1. **Steam Input**: reads active Gameplay action names from `SteamInputManager.GetActiveActions()` and maps them to NES button names via a fixed constant table in code (`SteamInputManager.ActionToNesButton`).
2. **SDL3 gamepad**: reads raw gamepad state via `SDL.GetGamepadButton` and `SDL.GetGamepadAxis` for player 0 and resolves each button through each binding's `gamepadButton` field in `config.json`.
3. **Keyboard**: reads pressed keys forwarded as `SDL.Keycode` values from the SDL event loop and resolves them through each binding's `key` field in `config.json`.

Steam actions are **not** stored in `config.json`. The mapping from VDF action name to NES button name is fixed by the VDF file and lives as a compile-time constant — it is not user-configurable. Only keyboard and SDL3 gamepad bindings are stored in config and editable in-game.

Each entry in `inputMappings` maps a NES button name to two source bindings:

```json
"P1 A": {
  "key": "Period",
  "gamepadButton": "A"
}
```

This means:
- An **Xbox, DualShock, Switch Pro, or Steam Deck controller** detected directly by SDL3 fires through the SDL3 gamepad source. SDL3 normalises all supported controller types to a common button layout — face buttons map to positional names (South/A, East/B, West/X, North/Y), and the D-pad and shoulders are consistent across brands.
- A **controller routed through Steam Input** (when Steam Input is active and the controller is configured with native action bindings rather than XInput passthrough) fires through the Steam Input source. The Steam Gameplay action set defines the mapping; `ActionToNesButton` in `SteamInputManager` converts action names to NES buttons each frame.
- A player can use a keyboard and a gamepad simultaneously. Both are always polled.

---

## Keyboard input

Keyboard events are driven by SDL `SDL_EVENT_KEY_DOWN` / `SDL_EVENT_KEY_UP` events forwarded from the SDL event loop in `SDL3WindowHost`:

```
SDL KeyDown/KeyUp events (main thread via SDL3WindowHost)
  → InputManager.OnKeyDown / OnKeyUp
     → _pressedKeys (HashSet<SDL.Keycode>, protected by lock)
          → read on emulation thread in PollSnapshot()
```

### Mapping keyboard keys

Each NES button in `inputMappings` has an optional `key` field. The value is an `SDL.Keycode` enum member name (case-insensitive):

```json
"P1 A": { "key": "Period", "gamepadButton": "A" }
```

Common key names:

| Key | Name |
|---|---|
| Letter keys | `"A"` through `"Z"` |
| Arrow keys | `"Up"`, `"Down"`, `"Left"`, `"Right"` |
| Enter | `"Return"` |
| Right Shift | `"RShift"` |
| Left Shift | `"LShift"` |
| Space | `"Space"` |
| Period | `"Period"` |
| Comma | `"Comma"` |
| Backspace | `"Backspace"` |
| Escape | `"Escape"` |
| F1–F12 | `"F1"` through `"F12"` |

The full list of valid names is the `SDL.Keycode` enum (from `SDL3-CS`). The in-game keyboard rebind screen writes the correct `SDL.Keycode` name for you when you press a key — there is no need to look up names manually. One legacy alias is recognised: `"Enter"` is accepted as a synonym for `"Return"` so existing configs are not broken.

### Binding uniqueness

When you rebind a key, any other action previously bound to that key is automatically cleared to prevent duplicate bindings. This applies to both keyboard and gamepad bindings independently.

---

## SDL3 gamepads

Gamepad input uses the SDL3 gamepad API — `SDL.OpenGamepad` / `SDL.GetGamepadButton` / `SDL.GetGamepadAxis` — via `SDL3GamepadDevice`. This works on Windows and Linux without any platform-specific driver configuration. SDL3 handles controller enumeration, hot-plug reconnection, and button layout normalisation internally.

Player 0 (the first connected controller) is always used. If no controller is connected, `SDL.GetGamepads` returns an empty list and the gamepad source reports no input.

### Button layout normalisation

SDL3 maps all supported controllers to a common positional layout regardless of the physical labels printed on the buttons:

| SDL3 position name | Xbox | PlayStation | Switch | Config string |
|---|---|---|---|---|
| South | A | Cross | B | `"A"` |
| East | B | Circle | A | `"B"` |
| West | X | Square | Y | `"X"` |
| North | Y | Triangle | X | `"Y"` |

The config strings (`"A"`, `"B"`, `"X"`, `"Y"`) refer to SDL3's positional names, which match Xbox labeling. On a PlayStation controller, physical Cross maps to `"A"` in config; on a Switch Pro, physical B maps to `"A"` in config.

### Mapping gamepad buttons

Each NES button in `inputMappings` has an optional `gamepadButton` field. Valid values:

| Value | SDL3 position / function |
|---|---|
| `"A"` | South face button |
| `"B"` | East face button |
| `"X"` | West face button |
| `"Y"` | North face button |
| `"Start"` | Start / Menu button (reserved by default — see below) |
| `"Back"` | Back / Select / Share button |
| `"LeftShoulder"` | Left bumper / L1 |
| `"RightShoulder"` | Right bumper / R1 |
| `"LeftThumb"` | Left stick click / L3 |
| `"RightThumb"` | Right stick click / R3 |
| `"DPadUp"` | D-pad up |
| `"DPadDown"` | D-pad down |
| `"DPadLeft"` | D-pad left |
| `"DPadRight"` | D-pad right |

An optional `gamepadButton2` field accepts the same set of values and is used for the left analog stick aliases (`"AnalogUp"`, `"AnalogDown"`, `"AnalogLeft"`, `"AnalogRight"`) in the default config — this allows both D-pad and analog stick to fire the same NES button without duplicating the binding entry.

**Start is reserved by default.** Regardless of the input mapping, pressing the gamepad Start button always opens or closes the in-game pause menu, and it cannot be bound to a NES button. This prevents the player from softlocking a game that doesn't implement its own pause. Developers who need Start as a NES button can set `overrideStartBindingProtection: true` in `config.json`; see [Developer / diagnostic settings](configuration.md#developer--diagnostic-settings).

### Auto-pause on disconnect

If the SDL3 gamepad or Steam Input controller disconnects while a game is running, NEShim automatically pauses and displays a **Controller Disconnected** overlay. Press any button or key to dismiss the overlay and resume. The pause uses the standard `PauseReasons.Menu` mechanism, so audio is muted and the frozen frame is shown in the background exactly as it is for a normal pause.

Reconnection is detected lazily on the next poll frame: `SDL3GamepadDevice` calls `SDL.GamepadConnected` each frame and re-calls `SDL.GetGamepads` / `SDL.OpenGamepad` if the previous handle is no longer valid.

### Analog stick → D-pad conversion

The left analog stick is automatically converted to directional input using the configured deadzone (`gamepadDeadzone`). The SDL3 axis convention is negated internally so positive Y means up, matching the D-pad expectation. The deadzone is a raw axis value in the range ±32767; the default of 8000 is about 24% deflection.

When both axes exceed the deadzone at the same time (stick pushed diagonally), behaviour is controlled by the `analogStickMode` developer setting:

| `analogStickMode` | Behaviour |
|---|---|
| `"Cardinal"` (default) | The axis with the larger absolute value wins. Only one direction registers, preventing accidental diagonal NES input in games with 4-directional movement. |
| `"Diagonal"` | Both axes register simultaneously. Use this for games with genuine 8-directional movement. |

Menu navigation always uses cardinal mode regardless of this setting — menus are inherently 4-directional.

---

## Steam Input

Steam Input is the recommended path for controllers that benefit from Steam's configurator. It maps physical hardware to abstract game actions defined in a VDF file, and works alongside the SDL3 gamepad source — both are polled every frame.

### How it works

Steam Input maps physical hardware through a layer defined in a VDF (value definition) file. The game declares *abstract actions* (`up`, `a_button`, `menu_confirm`, etc.) and Steam maps the player's physical hardware to those actions. Default mappings ship with the game so players can use supported controllers immediately. Players can override the defaults from the Steam overlay configurator at any time.

Each frame, `SteamInputManager.GetActiveActions()` returns the set of VDF action names currently pressed. `InputManager.PollSnapshot()` applies the constant mapping table (`SteamInputManager.ActionToNesButton`) to convert those action names to NES button names — no config lookup is needed. This table is:

| VDF action | NES button |
|---|---|
| `up` | `P1 Up` |
| `down` | `P1 Down` |
| `left` | `P1 Left` |
| `right` | `P1 Right` |
| `a_button` | `P1 A` |
| `b_button` | `P1 B` |
| `start` | `P1 Start` |
| `select` | `P1 Select` |

This mapping is fixed in code (`SteamInputManager.ActionToNesButton` / `NesButtonToAction`). It is not stored in `config.json` and cannot be changed without modifying both the code and the VDF file.

### Action sets

NEShim defines two action sets in `game_actions_<appid>.vdf`:

#### `Gameplay` set

Active during emulation (when the pause menu is closed).

| Action name | Purpose |
|---|---|
| `up` | NES D-pad Up |
| `down` | NES D-pad Down |
| `left` | NES D-pad Left |
| `right` | NES D-pad Right |
| `a_button` | NES A button |
| `b_button` | NES B button |
| `start` | NES Start |
| `select` | NES Select |

#### `Menu` set

Active when the pre-game main menu or in-game pause menu is open.

| Action name | Purpose |
|---|---|
| `menu_up` | Move cursor up |
| `menu_down` | Move cursor down |
| `menu_left` | Decrease volume (on Sound screen) |
| `menu_right` | Increase volume (on Sound screen) |
| `menu_confirm` | Activate selected item |
| `menu_back` | Go back / cancel |

The action set is switched automatically:
- `SteamInputManager.ActivateGameplaySet()` — called when emulation resumes (menu closed, game started).
- `SteamInputManager.ActivateMenuSet()` — called when a menu opens or the main menu is shown.

### VDF file setup

The action definition file must be present alongside the executable and named `game_actions_<AppID>.vdf`. During development the placeholder file is named `game_actions_0.vdf`.

The file contains a `configurations` block that tells Steam which binding VDF to load for each controller type. This makes the defaults apply automatically in local development without requiring an upload to the Steamworks partner dashboard first.

Steps for production:
1. Rename the file: `game_actions_0.vdf` → `game_actions_<YourAppID>.vdf`.
2. Upload the file via the Steamworks partner dashboard under **Steam Input → Default Configuration**.
3. Upload the controller binding VDF files from `controller_bindings/` as the default configuration for each controller type (see **Default controller bindings** below).

If the VDF file is missing or Steam Input fails to initialise, `SteamInputManager.IsAvailable` will be `false` and the code falls back to the SDL3 gamepad source only.

### Default controller bindings

Default bindings ship in the `controller_bindings/` directory alongside the executable:

| File | Controller type |
|---|---|
| `xbox360.vdf` | Xbox 360 |
| `xboxone.vdf` | Xbox One / Xbox Series X\|S / Xbox One Elite |
| `neptune.vdf` | Steam Deck |
| `ps4.vdf` | PlayStation 4 DualShock 4 |
| `ps5.vdf` | PlayStation 5 DualSense |
| `switch_pro.vdf` | Nintendo Switch Pro Controller |
| `steam_controller.vdf` | Valve Steam Controller |

Each file uses **XInput passthrough** bindings by default: face buttons, D-pad, Start, and Back are forwarded to the game as XInput-compatible signals, which SDL3 picks up as a standard gamepad. The `config.json` `inputMappings` then apply from there. Players can override the defaults at any time from the Steam overlay configurator (Shift+Tab).

> **Note on Switch Pro button labels.** Steam Input normalises button positions across controllers using a positional mapping. The "A" binding in the VDF file maps to the bottom face button on the physical controller, which is **B** on a Switch Pro. The NES-style labels (A = right face, B = bottom face) are correct for gameplay feel.

### Gamepad bindings screen behaviour

The **Gamepad Controls** settings screen behaves differently depending on the active controller type:

**SDL3 / XInput passthrough controllers (Xbox, most gamepads)**

`SteamInputManager.IsUsingNativeActions()` returns false — all input flows through the SDL3 gamepad source.

- Each binding row shows the SDL3 button name from `config.json` (e.g. `DPadUp`, `Y`, `Back`).
- All rows are selectable and editable.
- To rebind: select a row, press the desired physical button. The binding is saved immediately.
- Start is reserved by default — pressing it during rebind shows a toast and cancels the operation. When `overrideStartBindingProtection` is enabled, Start binds normally and only Escape cancels.

**Native Steam controllers (PS4, PS5, Switch Pro, Steam Controller with full action bindings)**

`SteamInputManager.IsUsingNativeActions()` returns true — the Gameplay action set has real digital action bindings.

- Each binding row shows the physical button label from Steam (e.g. "Cross Button", "Triangle Button"), queried live via `GetDigitalActionOrigins` + `GetStringForActionOrigin`. Labels reflect the player's current Steam controller configurator layout.
- All binding rows are shown **read-only** (greyed out). In-game rebinding is not possible for native Steam controllers.
- To remap: open the Steam overlay (Shift+Tab) → Controller Settings, and adjust bindings there. The change is reflected live in the binding labels on next visit to this screen.
- The **Back** row remains active so the player can exit the screen.

---

## Hotkeys

Hotkeys are system-level shortcuts processed by the emulation thread before gameplay input is forwarded to the NES. They use edge-triggered detection (fires once on the frame the key is first pressed, not every frame it is held).

### Keyboard hotkeys

Configured via `hotkeyMappings` in `config.json`. Values are `SDL.Keycode` enum member names, the same as keyboard input bindings.

| Action | Default key | Description |
|---|---|---|
| `OpenMenu` | `Escape` | Open or close the in-game pause menu |
| `SaveActiveSlot` | `F5` | Save to the currently selected slot |
| `LoadActiveSlot` | `F9` | Load from the currently selected slot (only if the slot is non-empty) |
| `SelectSlot1`–`SelectSlot8` | `F1`–`F8`* | Select a save slot (displays a toast) |
| `ToggleWindow` | `F11` | Toggle between fullscreen and windowed mode |

*Slots 1–4 use F1–F4; slot 5 uses F6; slots 6–8 use F7, F8, F12. F5 and F9 are reserved for save/load.

### Gamepad hotkeys

Configured via `gamepadHotkeyMappings` in `config.json`. Values are SDL3 button name strings.

| Action | Default button | Description |
|---|---|---|
| `OpenMenu` | `LeftShoulder` | Open or close the in-game pause menu |

The gamepad Start button also opens/closes the pause menu by default, regardless of this mapping. Set `overrideStartBindingProtection: true` to remove that; the menu will then be accessible only via Escape and this `OpenMenu` hotkey.

---

## Menu navigation

While a menu is open, the emulation loop does not run `RunFrame`. Instead, it polls for menu navigation input at approximately 60 Hz (using `ManualResetEventSlim.Wait(16)`).

Menu navigation combines SDL3 gamepad and Steam Input (edge-triggered):

| Input | Action |
|---|---|
| D-pad Up / Left stick up | Move cursor up |
| D-pad Down / Left stick down | Move cursor down |
| D-pad Left / Left stick left | Decrease volume (on Sound screen) |
| D-pad Right / Left stick right | Increase volume (on Sound screen) |
| A (South) button | Confirm / activate selected item |
| B (East) button or Back button | Go back |

Keyboard navigation uses the arrow keys (Up/Down for cursor movement, Left/Right for volume), Enter/Space/Z to confirm, and Escape to go back.

---

## Input pipeline summary

```
SDL KeyDown/KeyUp events (main thread via SDL3WindowHost)
  ──→ KeyboardInputSource._pressedKeys (lock)
         ↓
Emulation thread: PollSnapshot()
  ├─ Steam Input: SteamInputManager.GetActiveActions()
  │    └─ ImmutableHashSet<string> of VDF action names (empty if unavailable)
  │         └─ SteamInputManager.ActionToNesButton (constant table)
  │              └─ NES button names added directly to builder
  ├─ SDL3 gamepad: SDL3GamepadDevice.GetState(0)
  │    └─ SDL.GetGamepadButton / SDL.GetGamepadAxis (empty if disconnected)
  │         └─ SDL3GamepadSource: button identifiers as strings ("A", "DPadUp", etc.)
  └─ Keyboard: _pressedKeys → SDL.Keycode.ToString() names
       ↓
  InputMappings loop (config.json) — keyboard + SDL3 gamepad only
    per NES button: check binding.GamepadButton ∈ gamepadIdentifiers
                    check binding.Key ∈ keyboardIdentifiers
    + analog stick → D-pad conversion (SDL3GamepadSource)
       ↓  (all sources resolved; duplicates deduplicated)
  InputSnapshot (ImmutableHashSet<string> of NES button names)
       ↓
  NesController.Update(snapshot)
       ↓
  NES.FrameAdvance(controller, ...)
```
