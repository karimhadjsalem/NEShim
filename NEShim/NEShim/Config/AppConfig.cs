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

    // ── Window & display ──────────────────────────────────────────────────────

    public string WindowTitle { get; set; } = "NEShim";

    // "Fullscreen" or "Windowed"
    public string WindowMode { get; set; } = "Fullscreen";

    // Position of the main menu panel: "BottomCenter", "Center", "BottomLeft",
    // "BottomRight", "TopLeft", "TopCenter", "TopRight"
    public string MainMenuPosition { get; set; } = "BottomCenter";

    // When true, displays a live FPS counter in the top-right corner during gameplay.
    public bool ShowFps { get; set; } = false;

    // When true, bilinear filtering is applied when scaling the NES frame.
    public bool GraphicsSmoothingEnabled { get; set; } = false;

    // ── Audio ─────────────────────────────────────────────────────────────────

    // Output device friendly name. Empty string selects the system default.
    public string AudioDevice      { get; set; } = "";
    public int    AudioBufferFrames { get; set; } = 3;

    // Master volume for game audio (0–100).
    public int Volume { get; set; } = 100;

    // When true, applies an additional LP@8kHz stage for warmer, less harsh sound.
    public bool SoundScrubberEnabled { get; set; } = false;

    // When false, main menu music is silenced regardless of MainMenuMusicPath.
    public bool MainMenuMusicEnabled { get; set; } = true;

    // ── Input ─────────────────────────────────────────────────────────────────

    public int GamepadDeadzone { get; set; } = 8000;

    // Persists the last-used save slot index (0–7) across sessions.
    public int ActiveSlot { get; set; } = 0;

    public Dictionary<string, InputBinding> InputMappings { get; set; } = new()
    {
        ["P1 Up"]     = new InputBinding("W",         "DPadUp"),
        ["P1 Down"]   = new InputBinding("S",         "DPadDown"),
        ["P1 Left"]   = new InputBinding("A",         "DPadLeft"),
        ["P1 Right"]  = new InputBinding("D",         "DPadRight"),
        ["P1 A"]      = new InputBinding("OemPeriod", "A"),
        ["P1 B"]      = new InputBinding("OemComma",  "B"),
        ["P1 Start"]  = new InputBinding("Return",    "Y"),
        ["P1 Select"] = new InputBinding("RShiftKey", "Back"),
    };

    /// <summary>Maps hotkey action names to XInput gamepad button names (see XInputHelper.GetButton).</summary>
    public Dictionary<string, string> GamepadHotkeyMappings { get; set; } = new()
    {
        ["OpenMenu"] = "LeftShoulder",   // Left bumper opens/closes the in-game menu
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
    // Generate with: seal-achievements --gen-keypair
    // When empty, achievements are disabled.
    public string AchievementPublicKey { get; set; } = "";

    // Steam language code for the UI (e.g. "french", "japanese", "schinese").
    // "Auto" reads the language from Steam at startup. When Steam is running,
    // this value is always overridden by Steam's language setting.
    // Supported: english, french, german, spanish, japanese, korean, russian, schinese, portuguese.
    public string Language { get; set; } = "Auto";

    // When true, diagnostic output is appended to neshim.log next to the executable.
    public bool EnableLogging { get; set; } = false;

    // Forces a specific rendering backend, bypassing automatic selection.
    // "auto"  — try D3D11 first; fall back to GDI+ if init fails (default)
    // "d3d11" — require D3D11; fall back to GDI+ only if D3D11 init throws
    // "gdi"   — always use GDI+ (useful when diagnosing D3D11-specific issues)
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

    // When true, skips the logo splash screen shown at startup.
    public bool NoLogo { get; set; } = false;
}
