---
layout: default
title: Input
nav_order: 6
parent: v3.0.1
description: "Keyboard remapping, SDL3 gamepad, Steam Input, hotkeys, and the VDF action-set file."
---

# Input system

NEShim supports three input sources that are combined every frame: keyboard, SDL3 gamepads, and Steam Input controllers. This page covers how each source works, how they interact, and how to configure them.

---

## Overview

Every emulation frame, `InputManager.PollSnapshot()` produces an `InputSnapshot` — an immutable set of NES button names that are currently pressed. The snapshot merges every source for every active player (see [Local multiplayer](#local-multiplayer) below — everything on this page describes player 1's path, which is unchanged from single-player and is exactly what runs when `playerCount` is left at its default of 1), with duplicates deduplicated harmlessly by the `ImmutableHashSet` builder:

1. **Steam Input**: reads active Gameplay action names from `SteamInputManager.GetActiveActions()` and maps them to NES button names via a fixed translation table in code (`SteamInputManager.NesButtonFor`).
2. **SDL3 gamepad**: reads raw gamepad state via `SDL.GetGamepadButton` and `SDL.GetGamepadAxis` for the player's assigned controller and resolves each button through each binding's `gamepadButton` field in `config.json`.
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
- An **Xbox, DualShock, Switch Pro, or Steam Deck controller** detected directly by SDL3 fires through the SDL3 gamepad source. SDL3 normalises all supported controller types to a common button layout — face buttons map to positional names (South/A, East/B, West/X, North/Y), and the D-pad and shoulders are consistent across brands. This is the path used for **every** controller in the overwhelming majority of setups, including Steam Deck's built-in controller and a Steam Controller used with its regular buttons — all fully rebindable in-game.
- A **controller with a physical trackpad or gyro/motion input bound to an action** — rare, and only possible if the player has deliberately assigned one from the Steam overlay configurator — fires through the Steam Input source instead. The Steam Gameplay action set defines the mapping; `ActionToNesButton` in `SteamInputManager` converts action names to NES buttons each frame. See [Native mode: trackpad and gyro only](#native-mode-trackpad-and-gyro-only) below.
- Independently of which of the above two is active, every gamepad binding row in the settings UI shows a glyph icon matching the player's actual hardware — a DualSense pad shows Square/Cross/Circle/Triangle, an Xbox pad shows its own face-button icons, and so on — rather than a generic Xbox-style label. This is purely a display concern layered on top of the SDL3 identifiers above; it has no effect on how input is read. See [Controller button glyphs](#controller-button-glyphs).
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

In single-player (the default, `playerCount: 1`), the first connected controller is always used. With local multiplayer enabled, `SDL3GamepadDevice` opens up to 4 controllers simultaneously — one per player, in `SDL.GetGamepads()`'s enumeration order — see [Local multiplayer](#local-multiplayer) below. If no controller is connected for a given player slot, that player's gamepad source reports no input.

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

### D-Pad / analog stick linkage

By default (`gamepadDeadzone`'s neighbor, `gamepadDpadStickInterchangeable: true`), a binding assigned to a D-pad direction also fires from the left analog stick's matching direction, and vice versa — the two are treated as the same physical input regardless of which one a binding actually names. This exists specifically so that rebinding one half of the pair from the **Gamepad Controls** settings screen can't silently orphan the other half: without it, rebinding only `P1 Up`'s D-pad slot would leave the analog stick no longer moving that direction (or vice versa), which is rarely what a player rebinding a single row intends. Left stick only — there is no right-stick equivalent to fold in. A **Settings** screen toggle (`D-Pad / Analog Stick: Linked` / `...: Separate`), present in both the main menu and the in-game pause menu, lets players who genuinely want independent D-pad and analog-stick bindings turn it off.

---

## Steam Input

Steam Input is the recommended path for controllers that benefit from Steam's configurator. It maps physical hardware to abstract game actions defined in a VDF file, and works alongside the SDL3 gamepad source — both are polled every frame.

### How it works

Steam Input maps physical hardware through a layer defined in a VDF (value definition) file. The game declares *abstract actions* (`up`, `a_button`, `menu_confirm`, etc.) and Steam maps the player's physical hardware to those actions. Default mappings ship with the game so players can use supported controllers immediately. Players can override the defaults from the Steam overlay configurator at any time.

Each frame, `SteamInputManager.GetActiveActions()` returns the set of VDF action names currently pressed. `InputManager.PollSnapshot()` applies a fixed translation (`SteamInputManager.NesButtonFor`) to convert those action names to NES button names — no config lookup is needed. For player 1, the mapping is:

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

This mapping is fixed in code (`SteamInputManager.NesButtonFor` / `ActionFor`). It is not stored in `config.json` and cannot be changed without modifying both the code and the VDF file. With local multiplayer enabled, the same 8 action names serve every player — `NesButtonFor(action, player)` just targets a different player's NES button (`up` for player 2 → `P2 Up`) while reading a different physical controller's Steam Input handle. **No VDF changes are needed for multiplayer** — see [Local multiplayer](#local-multiplayer) below.

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

### Native mode: trackpad and gyro only

`SteamInputManager.IsUsingNativeActions()` does **not** mean "any Steam controller with action bindings" — it means specifically that **the player has assigned a physical trackpad or gyro/motion input to one of NEShim's Gameplay or Menu actions**, via the Steam overlay configurator. It checks every origin bound to every Gameplay and Menu action (`GetDigitalActionOrigins`) for the substrings `LeftPad`, `RightPad`, `CenterPad`, or `Gyro`.

This is the one case SDL3's flat gamepad model genuinely can't represent — a trackpad click/swipe or a motion sensor has no XInput-style button equivalent. Every other case — regular face buttons, D-pad, shoulders, sticks-as-D-pad, on any controller including Steam Deck's built-in pad or a Steam Controller — returns `false`, and that controller behaves exactly like a plain SDL3 gamepad: full in-game rebinding, no lockout.

In practice this rarely triggers: NEShim's `game_actions_0.vdf` declares every action as a **digital Button action**, and Valve's action-manifest rules only allow a trackpad to drive a digital action in click/touch mode — true continuous gyro aiming can't be bound to any current NES action at all. The default `controller_bindings/*.vdf` presets never bind trackpad or gyro to anything, so out of the box every supported controller — including PS4/PS5/Switch Pro/Steam Controller/Steam Deck — is in the fully-rebindable SDL3 path from first launch.

### Gamepad bindings screen behaviour

The **Gamepad Controls** settings screen behaves the same way for virtually every controller — Xbox, PlayStation, Switch Pro, Steam Deck's built-in controller, and a Steam Controller used with its regular buttons:

- Each binding row shows a **glyph icon** matching the player's actual hardware when one is available (see [Controller button glyphs](#controller-button-glyphs) below), falling back to localized text (e.g. `DPadUp`, `Y`, `Back`) when no glyph can be resolved.
- All rows are selectable and editable.
- To rebind: select a row, press the desired physical button. The binding is saved immediately.
- Start is reserved by default — pressing it during rebind shows a toast and cancels the operation. When `overrideStartBindingProtection` is enabled, Start binds normally and only Escape cancels.

**Native mode (trackpad/gyro bound — rare)**

When `SteamInputManager.IsUsingNativeActions()` is true (see above):

- Each binding row shows the physical button label from Steam (e.g. "Cross Button", "Left Pad Click"), queried live via `GetDigitalActionOrigins` + `GetStringForActionOrigin`. This is a text label from Steam, not a glyph icon, and reflects the player's current Steam controller configurator layout.
- All binding rows are shown **read-only** (greyed out). In-game rebinding is not possible while a trackpad/gyro binding is active.
- To remap: open the Steam overlay (Shift+Tab) → Controller Settings, and adjust bindings there. The change is reflected live in the binding labels on next visit to this screen. Removing the trackpad/gyro binding (back to regular buttons only) returns the controller to the normal, fully-rebindable behaviour above.
- The **Back** row remains active so the player can exit the screen.

### Controller button glyphs

Every binding row's value column shows the physical button glyph for the player's actual connected hardware — a DualSense pad shows Square/Cross/Circle/Triangle, an Xbox pad shows its own face-button icons, a Switch Pro pad shows Nintendo-style icons — regardless of controller brand, and independently of whether that controller is in native mode or the normal SDL3 path.

Resolution is a three-tier fallback chain, tried in order for each bound identifier (e.g. `"A"`, `"DPadUp"`):

1. **Steam Input glyph** — translates the SDL-style identifier to an `EXboxOrigin`, then calls `ISteamInput::GetActionOriginFromXboxOrigin` to find the equivalent origin on the player's actual connected controller, then `GetGlyphPNGForActionOrigin` to fetch Valve's own icon for it. This works for any SDL-recognised controller Steam has glyph art for, and — critically — requires **no VDF/action-manifest binding** on the identifier being looked up. It answers "what does this physical input look like on this hardware," which is a separate question from "is this input routed through the Steam action-set layer," so it applies equally whether the controller is in native mode or the normal SDL3 path.
2. **Bundled per-brand glyph** — used only when Steam Input has no answer (typically: Steam isn't running at all, a dev/local-testing case since the shipped game always relaunches itself through Steam). Reads the controller type via SDL3 and loads a matching icon from a small set of glyphs embedded in the binary, covering Xbox, PlayStation, and Switch Pro / Joy-Con families.
3. **Localized text** — the existing SDL3 button name, localized (e.g. `DPadUp` → the current language's translated label). Always produces a result, so the chain never fails to show something.

Resolved glyphs are cached per bound identifier and invalidated automatically when a controller connects or disconnects, or when the language changes — nothing is re-resolved every frame.

**Publisher note:** the bundled per-brand glyphs (tier 2 above) that ship with NEShim are simple placeholder icons, not licensed Xbox/PlayStation/Switch artwork. Replace them with your own licensed or original glyph set before a commercial release if you expect players to regularly run the game with Steam unavailable — with Steam running (the normal case for a Steam release), tier 1's Valve-supplied glyphs are used instead and this doesn't apply.

---

## Local multiplayer

NEShim supports up to 4 local players, opt-in via the publisher-only `playerCount` field in `config.json` (default `1` — everything above this section describes exactly what runs at the default). See [Configuration — Local multiplayer](configuration.md#local-multiplayer) for the field reference.

### Controller-port wiring

The vendored BizHawk NES core already fully emulates the real **Four Score** / **Famicom 4-Player Adapter** hardware — no core changes were needed to support 3–4 players. `playerCount` selects which virtual ports are plugged in:

| `playerCount` | Left port | Right port | Players wired |
|---|---|---|---|
| 1 (default) | Standard controller | Unplugged | P1 only — byte-identical to every pre-multiplayer NEShim build |
| 2 | Standard controller | Standard controller | P1, P2 |
| 3 | Four Score | Four Score | P1, P2, P3 (a harmless unused P4 slot exists at the hardware level but is never fed input) |
| 4 | Four Score | Four Score | P1, P2, P3, P4 |

**Why 3 players still wires Four Score into both ports.** A real Four Score is one device spanning both controller ports at once — each port emits its own half of a signature that a Four-Score-aware game checks *on both ports together* before it will read the extra "chained" data multiplexed onto the same wire (this chained data is where player 2's input lives, alongside player 1's, on the port). Plugging a standard controller into the right port — a smaller-looking, seemingly tidier wiring for exactly 3 players — supplies no signature at all, so the game's Four Score detection fails and it never reads the chained data, silently dropping player 2 even though NEShim's own input mapping for player 2 is otherwise perfectly correct. Both ports must be Four Score for 3 players to actually work, exactly as for 4.

**Save-state warning:** NEShim's save-state format is a positional binary stream, not a keyed format — it records exactly the bytes each plugged controller port writes, in port order. Changing `playerCount` on an already-shipped game changes what's plugged into each port, which desyncs the read of any save state captured under the old wiring (the load fails safely — caught and logged, not a crash — but the save is unrecoverable). Treat `playerCount` as fixed once a game ships, exactly like the ROM file itself.

### Per-player input sources

Every input source described above — SDL3 gamepad, Steam Input, and (implicitly) keyboard — fans out per player:

- **SDL3 gamepad**: `SDL3GamepadDevice` opens up to 4 physical controllers simultaneously, one per player, in `SDL.GetGamepads()`'s connection-enumeration order. Each player's raw button/axis identifiers are matched only against that player's own `"P{n} …"` entries in `inputMappings` — this matters because two different physical gamepads legitimately report the same raw identifier (both might send `"DPadUp"` for their own D-pad), and without per-player scoping one player's press would incorrectly also trigger another player's identically-bound action.
- **Steam Input**: the same 8 VDF actions (`up`, `down`, …, `select`) serve every player — see the [Steam Input](#steam-input) section above. No VDF changes needed for multiplayer.
- **Keyboard**: a single shared keyboard naturally serves every player through the same flat `inputMappings` dictionary — `"P2 Up"` and `"P1 Up"` can be bound to different keys with no special handling, since keyboard input has no "which physical device" ambiguity the way two gamepads do.

**Hotkeys, menu navigation, and the auto-pause-on-disconnect screen stay player-1-only, deliberately** — even in a 4-player session, only player 1's gamepad/keyboard can open the pause menu, navigate menus, trigger save/load hotkeys, or dismiss the controller-disconnect screen. These are treated as session-level concerns, not per-player gameplay concerns. The one exception: **rebinding** a player 2–4 gamepad control reads that player's own physical controller during the capture, not player 1's — rebinding wouldn't make sense any other way.

### Controller button glyphs, per player

Each player's binding rows resolve glyphs independently and cache them separately (see [Controller button glyphs](#controller-button-glyphs) above for the resolution chain) — if player 2's gamepad is a different brand than player 1's, player 2's binding screen always shows player 2's own hardware's icons. A cache shared across players would risk showing the wrong brand's glyph for an identically-named button once two different-brand controllers were connected at once.

### Player Controls menu

With `playerCount` above 1, **Settings** replaces its single-player "Gamepad Controls"/"Keyboard Controls" rows with one **Player Controls** entry leading to a dedicated submenu listing every player's bindings together:

- A **Gamepad Controls** row for every player, 1 through `playerCount`, listed first.
- A divider line.
- A **Keyboard Controls** row per player, listed below the divider.

Player 1's entries appear here too, alongside players 2–4 — this is now the *only* way to reach player 1's binding screens once local multiplayer is on, since Settings' own direct rows are gone (showing player 1 in both places at once would just be a duplicate entry). With `playerCount` back at `1`, Settings reverts to its original two direct rows and this submenu doesn't exist.

By default, only **player 1's** keyboard row is shown in this submenu — players 2–4's keyboard rows are hidden. This is controlled by the developer setting `hideKeyboardControlsForExtraPlayers` (default `true` — see [Configuration — Developer / diagnostic settings](configuration.md#developer--diagnostic-settings)): a shared local keyboard rarely serves more than one player at a time, so most releases won't want to clutter this screen with keyboard rows for players who will only ever use a gamepad. Set it to `false` to show every player's keyboard row.

Each per-player binding screen behaves exactly like the single-player **Gamepad Controls** / **Keyboard Controls** screens described earlier on this page — same rebind flow, same Start-button reservation rules, same Steam Input native-mode read-only behaviour when applicable — just scoped to that player's own `"P{n} …"` config keys and physical controller.

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

Menu navigation combines SDL3 gamepad and Steam Input (edge-triggered). In local multiplayer, only **player 1's** gamepad/keyboard drives menu navigation — see [Local multiplayer](#local-multiplayer) above.

| Input | Action |
|---|---|
| D-pad Up / Left stick up | Move cursor up |
| D-pad Down / Left stick down | Move cursor down |
| D-pad Left / Left stick left | Decrease volume (on Sound screen) |
| D-pad Right / Left stick right | Increase volume (on Sound screen) |
| A (South) button | Confirm / activate selected item |
| B (East) button or Back button | Go back |

Keyboard navigation uses the arrow keys (Up/Down for cursor movement, Left/Right for volume), Enter/Space/Z to confirm, and Escape to go back.

### Held-repeat on sliders

Holding Left or Right on a slider row — Volume, any of the 3 Audio EQ bands, or any of the 4 Picture Adjustment sliders (Brightness/Contrast/Saturation/Hue) — auto-repeats instead of requiring repeated individual presses, on keyboard, SDL3 gamepad, and Steam Input alike. Timing: a 400 ms initial delay before the first repeat, then repeats every 80 ms (~12 Hz); after 1 second of continuous hold, the rate accelerates to every 30 ms (~33 Hz) for fast large-range adjustment. Releasing the direction (or switching to the other direction) resets the timer. This only applies while paused in a menu — it has no effect on gameplay input.

---

## Input pipeline summary

The pipeline below is player 1's — with `playerCount` above 1, the Steam Input and SDL3 gamepad legs are each replicated once per additional player (its own `SDL3GamepadDevice` slot, its own `SteamInputManager` controller handle, its own `"P{n} …"` scoped `inputMappings` lookup), all merged into the same single `InputSnapshot` for that frame; the keyboard leg is shared across all players (see [Local multiplayer](#local-multiplayer) above).

```
SDL KeyDown/KeyUp events (main thread via SDL3WindowHost)
  ──→ KeyboardInputSource._pressedKeys (lock)
         ↓
Emulation thread: PollSnapshot()
  ├─ Steam Input: SteamInputManager.GetActiveActions()
  │    └─ ImmutableHashSet<string> of VDF action names (empty if unavailable)
  │         └─ SteamInputManager.NesButtonFor (per-player translation)
  │              └─ NES button names added directly to builder
  ├─ SDL3 gamepad: SDL3GamepadDevice.GetState(playerIndex)
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
