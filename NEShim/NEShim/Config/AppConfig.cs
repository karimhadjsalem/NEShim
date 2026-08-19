using System.Collections.Generic;

namespace NEShim.Config;

public sealed class AppConfig
{
    // ── Files & paths ─────────────────────────────────────────────────────────

    public string RomPath             { get; set; } = "game.nes";
    public string SaveStateDirectory  { get; set; } = "saves";
    public string SaveRamPath         { get; set; } = "game.srm";

    // Path to the image shown on the main (pre-game) menu. Relative to exe or absolute.
    public string MainMenuBackgroundPath { get; set; } = "";

    // Paths to images drawn in the left and right letterbox bars during gameplay.
    // Relative to exe or absolute. Leave empty to show plain black bars.
    public string SidebarLeftPath  { get; set; } = "";
    public string SidebarRightPath { get; set; } = "";

    // Path to an audio file (MP3 recommended) played on the pre-game main menu.
    // Relative to exe or absolute. Leave empty to disable.
    public string MainMenuMusicPath { get; set; } = "";

    // Background shown behind the multi-game carousel (games/multigame.json shell config
    // only — ignored in a per-game config.json). Static image or animated GIF; relative to
    // the games/ folder or absolute. Leave empty for a plain fill.
    public string CarouselBackgroundPath { get; set; } = "";

    // games/multigame.json shell config only — ignored in a per-game config.json. Trusted
    // gameId -> expected SteamDlcAppId mapping for games that must be sold as separate DLC.
    // Each game's own config.json (which lives in that game's own DLC depot/folder) is
    // trivially player-editable at any time — Steam's file-integrity verification is a manual,
    // player-triggered check, not a continuous runtime guarantee, so games/multigame.json is
    // realistically just as locally-editable. Without a compiled-in verifying key (see
    // DlcMapSigner.EmbeddedPublicKeyBase64 — deliberately NOT a config field; a config-driven
    // key could just be swapped out alongside a forged map, defeating the whole point) and
    // GameDlcAppIdsSignature below, this map is only a soft cross-check: GameScanner rejects a
    // game whose own claimed SteamDlcAppId mismatches an entry present here, but a gameId
    // absent from the map is trusted as declared by its own config.json — closing the "edit one
    // field" bypass but not one that also edits/removes the corresponding map entry. Sign the
    // map (see below) for a real, tamper-proof guarantee instead.
    public Dictionary<string, uint> GameDlcAppIds { get; set; } = new();

    // games/multigame.json shell config only. ECDSA-P256 signature (base64) over GameDlcAppIds
    // — see DlcMapSigner (NEShim.Signing project, NEShim.Achievements namespace), sealed with the pub-utils tool
    // (--seal-dlc-map). Verified against DlcMapSigner.EmbeddedPublicKeyBase64, a compile-time
    // constant — NOT read from config, so a copied/tampered install can't just supply its own
    // matching keypair. Setting this signature without also compiling in the matching public
    // key has no effect (signing is opted into by the presence of the EMBEDDED key, not this
    // field). GameScanner fails CLOSED (every game rejected, not silently trusted) if the
    // embedded key is set but this signature is missing or doesn't verify — a corrupted/missing
    // signature must never fall back to the weaker unsigned behavior; that would make enabling
    // this feature at all a net loss the moment the signature gets mishandled.
    public string GameDlcAppIdsSignature { get; set; } = "";

    // ── Window & display ──────────────────────────────────────────────────────

    public string WindowTitle { get; set; } = "NEShim";

    // "Fullscreen" or "Windowed"
    public string WindowMode { get; set; } = "Fullscreen";

    // Position of the main menu panel: "BottomCenter", "Center", "BottomLeft",
    // "BottomRight", "TopLeft", "TopCenter", "TopRight"
    public string MainMenuPosition { get; set; } = "BottomCenter";

    // When true, displays a live FPS counter in the top-right corner during gameplay.
    public bool ShowFps { get; set; } = false;

    // Structural video filter applied to the NES framebuffer before display.
    // All filters ("PixelPerfect", "Bilinear", "CrtScanlines", "CrtPhosphor", "NtscComposite",
    // "CrtScreen", "Xbr") work on both D3D11 (DXBC) and SDL_GPU (SPIR-V).
    // If a filter is set but no GPU renderer is available, NEShim logs a warning and falls back to "PixelPerfect".
    // "NearestNeighbour" is a deprecated alias — migrated to "PixelPerfect" at load time.
    public string VideoFilter { get; set; } = "PixelPerfect";

    // Controls how the NES image is displayed relative to the window edges.
    // "Overscan"  — crop 8 rows top and bottom (224 rows visible); matches original NTSC TV output.
    // "Normal"    — display all 240 rows.
    // "Underscan" — display all 240 rows but scale the image to 88% of the window, with a
    //               uniform black border on all sides (simulates an underscanned CRT monitor).
    // Legacy values "NTSC" and "Auto" map to "Overscan"; "None" maps to "Normal".
    public string OverscanMode { get; set; } = "Normal";

    // Per-frame motion effect applied to the NES frame quad.
    // "None", "CrtJitter", "ScanlineBob", "MagneticDistortion" — available on D3D11 and SDL_GPU.
    // "PhosphorPersistence" — D3D11 only; demotes to None on SDL_GPU (requires ping-pong temporal buffer).
    public string VideoMotionEffect { get; set; } = "None";

    // Second-pass overlay filter stacked on top of VideoFilter (D3D11 only).
    // Only "CrtScanlines", "CrtPhosphor", and "CrtScreen" are valid overlay values.
    // "None" disables the overlay (single-pass pipeline).
    // SetOverlayFilter is a no-op on SDL_GPU; this field is stored but has no visual effect.
    public string VideoFilterOverlay { get; set; } = "None";

    // Colour grade applied on top of the structural video filter.
    // Available on both D3D11 and SDL_GPU/Vulkan.
    // "None"               — no color adjustment.
    // "Warm"               — slight amber tint mimicking an aged CRT phosphor.
    // "Greyscale"          — convert to greyscale using BT.601 luma weights.
    // "NesColorCorrection" — approximate 2C02 composite → sRGB colour correction.
    // "Cool"               — blue-green tint approximating the D93 9300K CRT white point.
    public string VideoColorFilter { get; set; } = "None";

    // Deprecated — use VideoFilter: "Bilinear" instead.
    // If true and VideoFilter is "NearestNeighbour", the loader promotes to "Bilinear".
    public bool GraphicsSmoothingEnabled { get; set; } = false;

    // ── Audio ─────────────────────────────────────────────────────────────────

    // Output device friendly name. Empty string selects the system default.
    public string AudioDevice      { get; set; } = "";
    public int    AudioBufferFrames { get; set; } = 3;

    // Master volume for game audio (0–100).
    public int Volume { get; set; } = 100;

    // Audio filter applied to the NES audio output.
    // "Default"      — standard NES hardware filter chain (HP@37Hz → HP@39Hz → LP@14kHz).
    // "Warm"         — raised HP cutoffs + LP@8kHz for warmer sound on modern speakers.
    // "PseudoStereo" — Haas-effect stereo widening from the mono source.
    // "WarmStereo"   — PseudoStereo + Warm lowpass combined.
    // "Compression"  — soft look-ahead compression to even out DPCM channel spikes.
    // "BassBoost"    — additive low-shelf boost at 150 Hz (+4 dB at DC, ~+2 dB at 150 Hz).
    // "Saturation"   — tanh soft-clip after the standard chain; mild boost below full scale.
    public string AudioFilter { get; set; } = "Default";

    // Deprecated — use AudioFilter: "Warm" instead.
    // If true and AudioFilter is still "Default", the loader promotes to "Warm".
    public bool SoundScrubberEnabled { get; set; } = false;

    // 3-band peaking EQ gains in dB (-12 to +12). 0 = neutral (no processing).
    public int AudioEqBass   { get; set; } = 0;
    public int AudioEqMid    { get; set; } = 0;
    public int AudioEqTreble { get; set; } = 0;

    // Brightness/Contrast/Saturation/Hue picture adjustments (-100..100; 0 = neutral).
    // Applied as a post-process pass after all structural, overlay, and motion effect passes.
    // Available on both D3D11 and SDL_GPU. When all four are 0 the pass is skipped entirely.
    public int VideoBrightness { get; set; } = 0;
    public int VideoContrast   { get; set; } = 0;
    public int VideoSaturation { get; set; } = 0;
    public int VideoHue        { get; set; } = 0;

    // Name of the last-applied video preset ("None", "NoFilters", "LivingRoom", "Arcade", "Sharp", "Phosphor").
    // "None" means no preset is tracked as active (current settings may be hand-tuned); "NoFilters"
    // is a real preset that resets every video setting to its engine default. Cleared to "None"
    // whenever any individual video setting is changed manually.
    public string VideoPreset { get; set; } = "None";

    // When false, main menu music is silenced regardless of MainMenuMusicPath.
    public bool MainMenuMusicEnabled { get; set; } = true;

    // Volume for main menu music, independent of the game audio Volume field. Range 0–100.
    public int MainMenuMusicVolume { get; set; } = 100;

    // ── Input ─────────────────────────────────────────────────────────────────

    // Number of local players (1-4), publisher-only — never overridable via user.json.
    // Clamped to [1,4] at load time (see ConfigLoader). Drives NES controller-port wiring
    // (NesPortSelector.ForPlayerCount) and which "Player N" gamepad/keyboard binding menu
    // entries appear. Default 1 keeps every currently-shipped game byte-identical to today's
    // single-player behavior unless a publisher explicitly opts in.
    //
    // Treat this as fixed once a game has shipped, exactly like the ROM itself: NEShim's save
    // states are a positional binary stream, not a keyed format, so changing the controller-port
    // wiring underneath an already-shipped game desyncs the read of any existing save state
    // (caught/logged as a load failure, not a crash — but the save is effectively lost).
    //
    // Multiplayer is gamepad-first out of the box: the default InputMappings below only seed
    // gamepad bindings for P2-P4, not keyboard ones — a single shared keyboard can't serve up to
    // 4 simultaneous players without a publisher-chosen, non-conflicting key layout, and guessing
    // one risks silently colliding with P1's own default keys. Publishers wanting local
    // keyboard-based co-op must hand-author P2-P4 Key bindings in config.json.
    public int PlayerCount { get; set; } = 1;

    public int GamepadDeadzone { get; set; } = 8000;

    // When true, a binding assigned to a D-pad direction also fires from the left analog
    // stick's matching direction, and vice versa — regardless of which slot (GamepadButton /
    // GamepadButton2) a rebind happens to leave holding which identifier (see
    // SDL3GamepadSource.GetActiveIdentifiers). Left stick only — there is no right-stick
    // equivalent identifier to pair against. Default on: most players expect both to just work
    // without needing to manually re-pair bindings after a rebind orphans the default pairing.
    public bool GamepadDpadStickInterchangeable { get; set; } = true;

    // Persists the last-used save slot index (0–7) across sessions.
    public int ActiveSlot { get; set; } = 0;

    // P2-P4 entries deliberately have Key = null (no default keyboard binding) — see the
    // PlayerCount doc comment above for why. Their GamepadButton/GamepadButton2 identifiers
    // reuse the same raw SDL identifiers as P1 ("DPadUp", "A", ...): each player is expected to
    // have their own distinct physical gamepad, and SDL3GamepadMapper scopes matching to only
    // that player's "P{n} " config keys, so identical raw identifiers across players never
    // collide the way identical keyboard keys would.
    public Dictionary<string, InputBinding> InputMappings { get; set; } = new()
    {
        ["P1 Up"]     = new InputBinding("W",         "DPadUp")    { GamepadButton2 = "AnalogUp" },
        ["P1 Down"]   = new InputBinding("S",         "DPadDown")  { GamepadButton2 = "AnalogDown" },
        ["P1 Left"]   = new InputBinding("A",         "DPadLeft")  { GamepadButton2 = "AnalogLeft" },
        ["P1 Right"]  = new InputBinding("D",         "DPadRight") { GamepadButton2 = "AnalogRight" },
        ["P1 A"]      = new InputBinding("Period", "A"),
        ["P1 B"]      = new InputBinding("Comma",  "B"),
        ["P1 Start"]  = new InputBinding("Return", "Y"),
        ["P1 Select"] = new InputBinding("RShift",  "Back"),

        ["P2 Up"]     = new InputBinding(null, "DPadUp")    { GamepadButton2 = "AnalogUp" },
        ["P2 Down"]   = new InputBinding(null, "DPadDown")  { GamepadButton2 = "AnalogDown" },
        ["P2 Left"]   = new InputBinding(null, "DPadLeft")  { GamepadButton2 = "AnalogLeft" },
        ["P2 Right"]  = new InputBinding(null, "DPadRight") { GamepadButton2 = "AnalogRight" },
        ["P2 A"]      = new InputBinding(null, "A"),
        ["P2 B"]      = new InputBinding(null, "B"),
        ["P2 Start"]  = new InputBinding(null, "Y"),
        ["P2 Select"] = new InputBinding(null, "Back"),

        ["P3 Up"]     = new InputBinding(null, "DPadUp")    { GamepadButton2 = "AnalogUp" },
        ["P3 Down"]   = new InputBinding(null, "DPadDown")  { GamepadButton2 = "AnalogDown" },
        ["P3 Left"]   = new InputBinding(null, "DPadLeft")  { GamepadButton2 = "AnalogLeft" },
        ["P3 Right"]  = new InputBinding(null, "DPadRight") { GamepadButton2 = "AnalogRight" },
        ["P3 A"]      = new InputBinding(null, "A"),
        ["P3 B"]      = new InputBinding(null, "B"),
        ["P3 Start"]  = new InputBinding(null, "Y"),
        ["P3 Select"] = new InputBinding(null, "Back"),

        ["P4 Up"]     = new InputBinding(null, "DPadUp")    { GamepadButton2 = "AnalogUp" },
        ["P4 Down"]   = new InputBinding(null, "DPadDown")  { GamepadButton2 = "AnalogDown" },
        ["P4 Left"]   = new InputBinding(null, "DPadLeft")  { GamepadButton2 = "AnalogLeft" },
        ["P4 Right"]  = new InputBinding(null, "DPadRight") { GamepadButton2 = "AnalogRight" },
        ["P4 A"]      = new InputBinding(null, "A"),
        ["P4 B"]      = new InputBinding(null, "B"),
        ["P4 Start"]  = new InputBinding(null, "Y"),
        ["P4 Select"] = new InputBinding(null, "Back"),
    };

    /// <summary>Maps hotkey action names to XInput gamepad button names (see XInputHelper.GetButton).</summary>
    public Dictionary<string, string> GamepadHotkeyMappings { get; set; } = new()
    {
        ["OpenMenu"]             = "LeftShoulder", // Left bumper opens/closes the in-game menu
        // Y toggles fullscreen/windowed, but only while the game carousel is showing (see
        // NEShimApp's HotkeyFired handler) — deliberately a *different* action name from
        // HotkeyMappings["ToggleWindow"] (F11), not just a gamepad alias for it, because Y is
        // also the default gamepad button for the NES "P1 Start" input. A shared action name
        // would fire both every time a player presses Start during actual gameplay, flipping
        // the window mode on every pause-menu open. F11 stays the only fullscreen toggle once a
        // game is loaded (main menu, in-game menu, gameplay); this one never fires there.
        ["ToggleWindowCarousel"] = "Y",
    };

    public Dictionary<string, string> HotkeyMappings { get; set; } = new()
    {
        ["SaveActiveSlot"] = "F5",
        ["LoadActiveSlot"] = "F9",
        ["SelectSlot1"]   = "F1",
        ["SelectSlot2"]   = "F2",
        ["SelectSlot3"]   = "F3",
        ["SelectSlot4"]   = "F4",
        ["SelectSlot5"]   = "F6",
        ["SelectSlot6"]   = "F7",
        ["SelectSlot7"]   = "F8",
        ["SelectSlot8"]   = "F12",
        ["ToggleWindow"]  = "F11",
    };

    // ── Developer options (not exposed in any menu) ───────────────────────────

    // ECDSA-P256 public key (SubjectPublicKeyInfo DER, base64) used to verify achievement
    // signatures at runtime. Must match the private key used to seal achievements.json.
    // Generate with: pub-utils --gen-keypair
    // When empty, achievements are disabled.
    public string AchievementPublicKey { get; set; } = "";

    // Steam language code for the UI (e.g. "french", "japanese", "schinese").
    // "Auto" reads the language from Steam at startup. When Steam is running,
    // this value is always overridden by Steam's language setting.
    // Supported: english, french, german, spanish, japanese, korean, russian, schinese, portuguese.
    public string Language { get; set; } = "Auto";

    // When true, diagnostic output is appended to neshim.log next to the executable.
    public bool EnableLogging { get; set; } = false;

    // Retained for forward-compatibility with publisher config.json files — never actually
    // consulted anywhere in the codebase (deserializing config.json populates it, nothing more).
    // The GDI+ rendering path this used to select was removed; D3D11 is always attempted first
    // on Windows, with SDL_GPU as the only fallback (both platforms).
    public string ForceRenderer { get; set; } = "auto";

    // Controls the NES region used for emulation. Affects CPU clock rate, PPU scanline
    // timing, APU frame counter, and the VSync rate exposed to the frame-timing loop.
    // "Auto"  — detect from the ROM's iNES header (correct for most ROMs)
    // "NTSC"  — force NTSC (~60.099 Hz)
    // "PAL"   — force PAL  (~50.007 Hz)
    // "Dendy" — force Dendy (~49.99 Hz, Russian clone)
    public string Region { get; set; } = "Auto";

    // Controls how the left analog stick maps to the NES d-pad on diagonal input.
    // "Cardinal" — dominant axis wins; prevents accidental diagonals.
    // "Diagonal" — both axes register simultaneously (8-directional movement).
    public string AnalogStickMode { get; set; } = "Cardinal";

    // When true, Start is no longer reserved as the system menu button and can be
    // rebound to a NES button. Menu remains accessible via Escape and the
    // configured OpenMenu gamepad hotkey.
    public bool OverrideStartBindingProtection { get; set; } = false;

    // When true (default), the Settings -> Player Controls submenu (see PlayerCount above) shows
    // keyboard-binding entries only for player 1, hiding them for players 2-4 — a shared keyboard
    // rarely serves more than one local player, so most publishers only want the gamepad rows for
    // extra players visible there. Set false to show every player's keyboard-binding entry too.
    // Only in force when PlayerCount > 1: with PlayerCount == 1 there is no "beyond player 1" to
    // hide, and the Player Controls submenu itself doesn't exist.
    public bool HideKeyboardControlsForExtraPlayers { get; set; } = true;

    // When true, skips the logo splash screen shown at startup.
    public bool NoLogo { get; set; } = false;

    // ── Multi-game / DLC (only meaningful in a games/<gameId>/config.json — ignored,
    //    unread, in a single-game config.json) ────────────────────────────────

    // Label shown for this game on the carousel. Falls back to WindowTitle when empty.
    public string GameDisplayTitle { get; set; } = "";

    // Steam DLC App ID that must be installed (SteamApps.BIsDlcInstalled) for this game to
    // appear on the carousel when running under a live Steam session. 0 = unfiltered — always
    // shown (used for local dev/test game folders with no real Steam entitlement).
    public uint SteamDlcAppId { get; set; } = 0;

    // Box art shown for this game in the carousel filmstrip, in NES-box aspect ratio.
    // Relative to this game's own folder or absolute. Leave empty to show a placeholder
    // card — a missing thumbnail is not treated as a configuration error.
    public string ThumbnailPath { get; set; } = "";

    // Short blurb shown on the carousel's flip-card back face when the player presses Up
    // on this game's tile. Leave empty to show a generic "No description available" back.
    public string GameDescription { get; set; } = "";
}
