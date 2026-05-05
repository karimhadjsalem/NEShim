namespace NEShim.Localization;

/// <summary>
/// All localizable UI strings, with English defaults.
/// Loaded from lang/&lt;language&gt;.json at startup.
/// Missing keys in a non-English file fall back to these defaults.
/// </summary>
internal sealed class LocalizationData
{
    // ---- Font ----
    public string FontFamily { get; init; } = "Segoe UI";

    // ---- Shared navigation ----
    public string Back { get; init; } = "← Back";

    // ---- Shared settings screen ----
    public string SettingsTitle    { get; init; } = "SETTINGS";
    public string VideoTitle       { get; init; } = "VIDEO";
    public string SoundTitle       { get; init; } = "SOUND";
    public string SettingsKeyboard { get; init; } = "Keyboard Controls";
    public string SettingsGamepad  { get; init; } = "Gamepad Controls";
    public string SettingsVideo    { get; init; } = "Video";
    public string SettingsSound    { get; init; } = "Sound";

    // ---- Shared video items ----
    public string VideoWindowFullscreen { get; init; } = "Window Mode: Fullscreen";
    public string VideoWindowWindowed   { get; init; } = "Window Mode: Windowed";
    public string VideoGraphicsSmooth   { get; init; } = "Graphics: Smooth";
    public string VideoGraphicsOriginal { get; init; } = "Graphics: Original";
    public string VideoFpsOn            { get; init; } = "FPS Overlay: On";
    public string VideoFpsOff           { get; init; } = "FPS Overlay: Off";

    // ---- Shared sound items ----
    /// <summary>Format string — {0} is the volume value (0–100).</summary>
    public string SoundVolume     { get; init; } = "◀  Volume: {0}  ▶";
    public string SoundScrubberOn  { get; init; } = "Sound Scrubber: On";
    public string SoundScrubberOff { get; init; } = "Sound Scrubber: Off";
    /// <summary>Main menu only — not shown in the in-game pause menu.</summary>
    public string SoundMusicOn  { get; init; } = "Menu Music: On";
    public string SoundMusicOff { get; init; } = "Menu Music: Off";

    // ---- Shared rebind screen titles ----
    /// <summary>Format string — {0} is the uppercase binding label, e.g. "UP".</summary>
    public string PressKeyTitle    { get; init; } = "PRESS KEY FOR  {0}";
    public string PressButtonTitle { get; init; } = "PRESS BUTTON FOR  {0}";

    // ---- Shared controller binding labels ----
    public string BindUp     { get; init; } = "Up";
    public string BindDown   { get; init; } = "Down";
    public string BindLeft   { get; init; } = "Left";
    public string BindRight  { get; init; } = "Right";
    public string BindA      { get; init; } = "A";
    public string BindB      { get; init; } = "B";
    public string BindStart  { get; init; } = "Start";
    public string BindSelect { get; init; } = "Select";

    // ---- Shared save-slot strings ----
    /// <summary>Format string — {0} is the 1-based slot number.</summary>
    public string SlotLabel   { get; init; } = "Slot {0}";
    /// <summary>Appended to disabled items when no save exists (leading spaces included).</summary>
    public string SlotNoSave  { get; init; } = "  (no save)";
    /// <summary>Appended to the active slot in the save-slot selection list.</summary>
    public string SlotActive  { get; init; } = "  ◀ active";
    public string SlotAutoSave { get; init; } = "Auto Save";

    // ---- Main menu ----
    public string MainMenuTitle   { get; init; } = "MAIN MENU";
    public string MainMenuLoadTitle { get; init; } = "LOAD GAME";
    public string MainMenuNewGame   { get; init; } = "New Game";
    public string MainMenuResumeGame { get; init; } = "Resume Game";
    public string MainMenuSettings  { get; init; } = "Settings";
    public string MainMenuExit      { get; init; } = "Exit";
    public string MainMenuRebindPressKey    { get; init; } = "Press any key  •  Esc to cancel";
    public string MainMenuRebindPressButton { get; init; } = "Press any controller button  •  Start to cancel";

    // ---- In-game menu: screen titles ----
    public string InGamePausedTitle  { get; init; } = "PAUSED";
    /// <summary>Format string — {0} is the 1-based active slot number.</summary>
    public string InGameSelectSlotTitle { get; init; } = "SELECT SLOT  (active: {0})";
    public string InGameLoadTitle    { get; init; } = "LOAD GAME?";
    public string InGameReturnTitle  { get; init; } = "RETURN TO MAIN MENU?";
    public string InGameExitTitle    { get; init; } = "EXIT TO DESKTOP?";

    // ---- In-game menu: root items ----
    public string InGameResume        { get; init; } = "Resume";
    public string InGameResetGame     { get; init; } = "Reset Game";
    public string InGameSelectSaveSlot { get; init; } = "Select Save Slot";
    public string InGameSaveGame      { get; init; } = "Save Game";
    public string InGameLoadGame      { get; init; } = "Load Game";
    public string InGameSettings      { get; init; } = "Settings";
    public string InGameReturnToMain  { get; init; } = "Return to Main Menu";
    public string InGameExit          { get; init; } = "Exit";

    // ---- In-game menu: confirmation screens ----
    public string InGameConfirmYesLoad   { get; init; } = "Yes, load game";
    public string InGameConfirmNoStay    { get; init; } = "No, stay in game";
    public string InGameConfirmYesReturn { get; init; } = "Yes, return to main menu";
    public string InGameConfirmYesExit   { get; init; } = "Yes, exit to desktop";
    public string InGameConfirmWarning   { get; init; } = "Unsaved progress will be lost.";

    // ---- In-game menu: rebind prompts ----
    public string InGameRebindPressKey      { get; init; } = "Press any key to bind\n(Esc to cancel)";
    public string InGameRebindPressButton   { get; init; } = "Press any controller button\n(Start to cancel)";
    public string InGameRebindStartReserved { get; init; } = "Start is reserved for the menu";
}
