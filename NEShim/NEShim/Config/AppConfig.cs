using System.Collections.Generic;

namespace NEShim.Config;

public sealed class AppConfig
{
    public string RomPath { get; set; } = "game.nes";
    public string WindowTitle { get; set; } = "NEShim";
    public string WindowMode { get; set; } = "Fullscreen"; // "Fullscreen" or "Windowed"
    public string SaveStateDirectory { get; set; } = "saves";
    public string SaveRamPath { get; set; } = "game.srm";
    public int ActiveSlot { get; set; } = 0;
    public int AudioBufferFrames { get; set; } = 3;
    public string AudioDevice { get; set; } = "";
    public int GamepadDeadzone { get; set; } = 8000;

    public Dictionary<string, InputBinding> InputMappings { get; set; } = new()
    {
        ["P1 Up"]     = new InputBinding("W",          "DPadUp"),
        ["P1 Down"]   = new InputBinding("S",          "DPadDown"),
        ["P1 Left"]   = new InputBinding("A",          "DPadLeft"),
        ["P1 Right"]  = new InputBinding("D",          "DPadRight"),
        ["P1 A"]      = new InputBinding("OemPeriod",  "A"),
        ["P1 B"]      = new InputBinding("OemComma",   "B"),
        ["P1 Start"]  = new InputBinding("Return",     "Y"),
        ["P1 Select"] = new InputBinding("RShiftKey",  "Back"),
    };

    /// <summary>Maps hotkey action names to XInput gamepad button names (see XInputHelper.GetButton).</summary>
    public Dictionary<string, string> GamepadHotkeyMappings { get; set; } = new()
    {
        ["OpenMenu"] = "LeftShoulder",   // Left bumper opens/closes the in-game menu
    };

    public Dictionary<string, string> HotkeyMappings { get; set; } = new()
    {
        ["OpenMenu"]      = "Escape",
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

    // Path to the image shown on the main (pre-game) menu. Relative to exe or absolute.
    public string MainMenuBackgroundPath { get; set; } = "";

    // Paths to images drawn in the left and right letterbox bars during gameplay.
    // Relative to exe or absolute. Leave empty to show plain black bars.
    public string SidebarLeftPath  { get; set; } = "";
    public string SidebarRightPath { get; set; } = "";
    
    // Path to an audio file (MP3 recommended) played on the pre-game main menu.
    // Relative to exe or absolute. Leave empty to disable.
    public string MainMenuMusicPath    { get; set; } = "";

    // Master volume for game audio (0–100).
    public int Volume { get; set; } = 100;

    // When true, applies an additional LP@8kHz stage for warmer, less harsh sound.
    public bool SoundScrubberEnabled { get; set; } = false;

    // When false, main menu music is silenced regardless of MainMenuMusicPath.
    public bool MainMenuMusicEnabled { get; set; } = true;

    // When true, bilinear filtering is applied when scaling the NES frame for a softer look.
    public bool GraphicsSmoothingEnabled { get; set; } = false;

    // Position of the main menu panel: "BottomCenter", "Center", "BottomLeft", "BottomRight", "TopLeft", "TopCenter", "TopRight"
    public string MainMenuPosition { get; set; } = "BottomCenter";

    public DeveloperSettings Developer { get; set; } = new();
}

public sealed class InputBinding
{
    public string? Key { get; set; }
    public string? GamepadButton { get; set; }

    public InputBinding() { }
    public InputBinding(string? key, string? gamepadButton)
    {
        Key = key;
        GamepadButton = gamepadButton;
    }
}

public sealed class DeveloperSettings
{
    public bool ShowFps { get; set; } = false;
    public bool AllowUnsafeRom { get; set; } = false;
}
