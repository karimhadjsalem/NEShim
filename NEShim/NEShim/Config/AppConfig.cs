using System.Collections.Generic;

namespace NEShim.Config;

public sealed class AppConfig
{
    public string RomPath { get; set; } = "game.nes";
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
        ["P1 B"]      = new InputBinding("OemComma",   "X"),
        ["P1 Start"]  = new InputBinding("Return",     "Start"),
        ["P1 Select"] = new InputBinding("RShiftKey",  "Back"),
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
