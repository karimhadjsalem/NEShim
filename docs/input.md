---
layout: default
title: Input
nav_order: 6
description: "Keyboard remapping, XInput, Steam Input, hotkeys, and the VDF action-set file."
---

# Input system

NEShim supports three input sources that are combined every frame: keyboard, XInput gamepads, and Steam Input controllers. This page covers how each source works, how they interact, and how to configure them.

---

## Overview

Every emulation frame, `InputManager.PollSnapshot()` produces an `InputSnapshot` — a set of NES button names that are currently pressed. The snapshot combines all three sources simultaneously:

1. **Steam Input**: reads action states from the active Steam Input action set. Returns an empty set if Steam Input is unavailable or no controller is connected.
2. **XInput**: reads raw gamepad state from `xinput1_4.dll` for player 0. Returns nothing if no XInput device is present.
3. **Keyboard**: reads the current set of pressed keys.

Both Steam Input and XInput are always polled. If both report input for the same NES button, it is deduplicated harmlessly. This means:
- An **Xbox controller** works via XInput even when Steam Input is disabled in Steam.
- A **PS4, PS5, or Switch Pro controller** requires Steam Input (since these are not natively XInput devices). When Steam Input is enabled and default bindings are configured, these controllers work without further setup.
- A player can use a keyboard and a gamepad simultaneously.

All three sources map to the same NES button names (`P1 Up`, `P1 Down`, `P1 Left`, `P1 Right`, `P1 A`, `P1 B`, `P1 Start`, `P1 Select`) defined in `config.json`'s `inputMappings`.

---

## Keyboard input

Keyboard input is driven by Windows Forms `KeyDown` / `KeyUp` events wired in `MainForm`:

```
WinForms KeyDown/KeyUp (UI thread)
  → InputManager.OnKeyDown / OnKeyUp
     → _pressedKeys (HashSet, protected by lock)
          → read on emulation thread in PollSnapshot()
```

### Mapping keyboard keys

Each NES button in `inputMappings` has an optional `key` field. The value is a `System.Windows.Forms.Keys` enum member name (case-insensitive):

```json
"P1 A": { "key": "OemPeriod", "gamepadButton": "A" }
```

Common key names:

| Key | Name |
|---|---|
| Letter keys | `"A"` through `"Z"` |
| Number row | `"D0"` through `"D9"` |
| Numpad | `"NumPad0"` through `"NumPad9"` |
| Arrow keys | `"Up"`, `"Down"`, `"Left"`, `"Right"` |
| Enter | `"Return"` |
| Shift (right) | `"RShiftKey"` |
| Shift (left) | `"LShiftKey"` |
| Space | `"Space"` |
| Period | `"OemPeriod"` |
| Comma | `"OemComma"` |
| Backspace | `"Back"` |
| Escape | `"Escape"` |
| F1–F12 | `"F1"` through `"F12"` |

The full list is the `System.Windows.Forms.Keys` enum. The in-game keyboard rebind screen writes the correct name for you when you press a key.

### Binding uniqueness

When you rebind a key, any other action previously bound to that key is automatically cleared to prevent duplicate bindings. This applies to both keyboard and gamepad bindings independently.

---

## XInput gamepads

XInput support uses a direct P/Invoke to `xinput1_4.dll` (present on Windows 8+). Player 0 (the first connected controller) is always used.

### Mapping XInput buttons

Each NES button in `inputMappings` has an optional `gamepadButton` field. Valid values:

| Value | Physical button |
|---|---|
| `"A"` | A button |
| `"B"` | B button |
| `"X"` | X button |
| `"Y"` | Y button |
| `"Start"` | Start button (reserved — see below) |
| `"Back"` | Back/Select button |
| `"LeftShoulder"` | Left bumper (LB) |
| `"RightShoulder"` | Right bumper (RB) |
| `"LeftThumb"` | Left stick click |
| `"RightThumb"` | Right stick click |
| `"DPadUp"` | D-pad up |
| `"DPadDown"` | D-pad down |
| `"DPadLeft"` | D-pad left |
| `"DPadRight"` | D-pad right |

**Start is reserved.** Regardless of the input mapping, pressing the gamepad Start button always opens or closes the in-game pause menu. It cannot be bound to a NES button. This prevents the player from softlocking a game that doesn't implement its own pause.

### Analog stick → D-pad conversion

The left analog stick is automatically converted to directional input using the configured deadzone (`gamepadDeadzone`). The conversion runs even if D-pad buttons are already mapped. The deadzone is a raw axis value in the range ±32767; the default of 8000 is about 24% deflection.

When both axes exceed the deadzone at the same time (stick pushed diagonally), behaviour is controlled by the `analogStickMode` developer setting:

| `analogStickMode` | Behaviour |
|---|---|
| `"Cardinal"` (default) | The axis with the larger absolute value wins. Only one direction registers, preventing accidental diagonal NES input in games with 4-directional movement. |
| `"Diagonal"` | Both axes register simultaneously. Use this for games with genuine 8-directional movement. |

Menu navigation always uses cardinal mode regardless of this setting — menus are inherently 4-directional.

---

## Steam Input

Steam Input is the recommended path for non-Xbox controllers. It maps physical hardware to abstract game actions, enabling PS4, PS5, Switch Pro, Steam Controller, and other controllers to work without XInput.

### How it works

Steam Input maps physical hardware through a layer defined in a VDF (value definition) file. The game declares *abstract actions* (`up`, `a_button`, `menu_confirm`, etc.) and Steam maps the player's physical hardware to those actions. Default mappings ship with the game so players can use supported controllers immediately. Players can override the defaults from the Steam overlay configurator at any time.

Steam Input and XInput are both always active. They produce independent sets of pressed buttons that are merged each frame. This avoids double-input problems — button names are deduplicated in the merge — and means Xbox controllers continue to work even if Steam Input is also reporting them.

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

The VDF file must be present alongside the executable and named `game_actions_<AppID>.vdf`. During development the placeholder file is named `game_actions_0.vdf`.

Steps for production:
1. Rename the file: `game_actions_0.vdf` → `game_actions_<YourAppID>.vdf`.
2. Upload the file via the Steamworks partner dashboard under **Steam Input → Default Configuration**.
3. Upload the controller binding VDF files from `controller_bindings/` as the default configuration for each controller type (see **Default controller bindings** below).

If the VDF file is missing or Steam Input fails to initialise, `SteamInputManager.IsAvailable` will be `false` and the code falls back to XInput.

### Default controller bindings

Default bindings ship in the `controller_bindings/` directory alongside the executable:

| File | Controller type |
|---|---|
| `xbox360.vdf` | Xbox 360 |
| `xboxone.vdf` | Xbox One / Xbox Series X\|S |
| `ps4.vdf` | PlayStation 4 DualShock 4 |
| `ps5.vdf` | PlayStation 5 DualSense |
| `switch_pro.vdf` | Nintendo Switch Pro Controller |
| `steam_controller.vdf` | Valve Steam Controller |

Each file maps D-pad and face buttons to the NES actions (`up`, `down`, `left`, `right`, `a_button`, `b_button`, `start`, `select`) in the `Gameplay` action set, and D-pad/confirm/back to the `Menu` action set.

Upload each file to the Steamworks partner dashboard as the **Default Configuration** for its controller type. Players can override these defaults at any time from the Steam overlay configurator (Shift+Tab).

> **Note on Switch Pro button labels.** Steam Input normalises button positions across controllers using a positional mapping. The "A" button in the binding file maps to the bottom face button on the physical controller, which is **B** on a Switch Pro. The NES-style labels (A = right face, B = bottom face) are correct for gameplay feel.

### Gamepad bindings screen behaviour

When Steam Input has an active controller connected, the in-game **Gamepad Controls** settings screen shows a notice instead of the editable XInput binding list:

> *Managed by Steam Input*  
> *Configure via Steam overlay (Shift+Tab)*

This prevents confusion: the XInput button names shown in the binding list would not match the physical buttons on a Steam Input-managed controller. The player configures their controller through the Steam overlay's controller configurator instead.

---

## Hotkeys

Hotkeys are system-level shortcuts processed by the emulation thread before gameplay input is forwarded to the NES. They use edge-triggered detection (fires once on the frame the key is first pressed, not every frame it is held).

### Keyboard hotkeys

Configured via `hotkeyMappings` in `config.json`:

| Action | Default key | Description |
|---|---|---|
| `OpenMenu` | `Escape` | Open or close the in-game pause menu |
| `SaveActiveSlot` | `F5` | Save to the currently selected slot |
| `LoadActiveSlot` | `F9` | Load from the currently selected slot (only if the slot is non-empty) |
| `SelectSlot1`–`SelectSlot8` | `F1`–`F8`* | Select a save slot (displays a toast) |
| `ToggleWindow` | `F11` | Toggle between fullscreen and windowed mode |

*Slots 1–4 use F1–F4; slot 5 uses F6; slots 6–8 use F7, F8, F12. F5 and F9 are reserved for save/load.

### Gamepad hotkeys

Configured via `gamepadHotkeyMappings` in `config.json`:

| Action | Default button | Description |
|---|---|---|
| `OpenMenu` | `LeftShoulder` | Open or close the in-game pause menu |

The gamepad Start button always opens/closes the pause menu regardless of this mapping.

---

## Menu navigation

While a menu is open, the emulation loop does not run `RunFrame`. Instead, it polls for menu navigation input at approximately 60 Hz (using `ManualResetEventSlim.Wait(16)`).

Menu navigation combines XInput and Steam Input (edge-triggered):

| Input | Action |
|---|---|
| D-pad Up / Left stick up | Move cursor up |
| D-pad Down / Left stick down | Move cursor down |
| D-pad Left / Left stick left | Decrease volume (on Sound screen) |
| D-pad Right / Left stick right | Increase volume (on Sound screen) |
| A button | Confirm / activate selected item |
| B button or Back button | Go back |

Keyboard navigation uses the arrow keys (Up/Down for cursor movement, Left/Right for volume), Enter/Space/Z to confirm, and Escape to go back.

Mouse hover and click are also supported — hovering highlights items; clicking activates them.

---

## Input pipeline summary

```
Keyboard events (UI thread)
  ──→ InputManager._pressedKeys (lock)
         ↓
Emulation thread: PollSnapshot()
  ├─ Steam Input: SteamInputManager.GetActiveGameplayButtons()
  │    └─ ImmutableHashSet<string> of NES button names (empty if unavailable)
  ├─ XInput: XInputHelper.GetState(0)
  │    └─ Digital buttons + analog stick → D-pad (empty if disconnected)
  └─ Keyboard: _pressedKeys → Keys enum → inputMappings lookup
       ↓  (all three merged; duplicates deduplicated)
  InputSnapshot (ImmutableHashSet<string>)
       ↓
  NesController.Update(snapshot)
       ↓
  NES.FrameAdvance(controller, ...)
```
