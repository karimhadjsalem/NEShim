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
    public string SettingsLanguage { get; init; } = "Language";

    // ---- Language screen ----
    public string LanguageTitle { get; init; } = "LANGUAGE";
    public string LanguageAuto  { get; init; } = "Auto";

    // ---- Shared video items ----
    public string VideoWindowFullscreen { get; init; } = "Window Mode: Fullscreen";
    public string VideoWindowWindowed   { get; init; } = "Window Mode: Windowed";
    public string VideoFilterLabel      { get; init; } = "Video Filter";
    public string OverscanLabel         { get; init; } = "Overscan";
    public string VideoFpsOn            { get; init; } = "FPS Overlay: On";
    public string VideoFpsOff           { get; init; } = "FPS Overlay: Off";

    // ---- Video filter sub-menu ----
    public string VideoFilterTitle         { get; init; } = "VIDEO FILTER";
    public string VideoFilterSmooth        { get; init; } = "Smooth";
    public string VideoFilterPixelPerfect  { get; init; } = "Pixel Perfect";
    public string VideoFilterCrtScanlines  { get; init; } = "CRT Scanlines";
    public string VideoFilterCrtPhosphor   { get; init; } = "CRT Phosphor";
    public string VideoFilterNtscComposite { get; init; } = "NTSC Composite";
    public string VideoFilterCrtScreen     { get; init; } = "CRT Screen";
    public string VideoFilterXbr           { get; init; } = "Sharp Pixel";

    // ---- Overlay (cycle item on Video screen) ----
    public string VideoOverlayLabel { get; init; } = "Overlay";

    // ---- Color preset and color effect names (used in Picture screen) ----
    public string VideoColorPresetLabel     { get; init; } = "Color";
    public string VideoColorFilterNone      { get; init; } = "None";
    public string VideoColorFilterWarm      { get; init; } = "Warm";
    public string VideoColorFilterGreyscale { get; init; } = "Greyscale";
    public string VideoColorFilterNesColors { get; init; } = "NES Colors";
    public string VideoColorFilterCool          { get; init; } = "Cool";
    public string VideoColorFilterPhosphorAmber { get; init; } = "Amber Mono";
    public string VideoColorFilterPhosphorGreen { get; init; } = "Green Mono";

    // ---- Motion effect sub-menu ----
    public string VideoMotionEffectLabel    { get; init; } = "Motion";
    public string VideoMotionEffectTitle    { get; init; } = "MOTION EFFECT";
    public string VideoMotionEffectNone     { get; init; } = "None";
    public string VideoMotionEffectCrtJitter           { get; init; } = "CRT Jitter";
    public string VideoMotionEffectScanlineBob         { get; init; } = "Scanline Flicker";
    public string VideoMotionEffectMagneticDistortion  { get; init; } = "Magnetic Distortion";
    public string VideoMotionEffectPhosphorPersistence { get; init; } = "Screen Glow";

    // ---- Picture sub-menu ----
    public string VideoPictureLabel    { get; init; } = "Picture";
    public string VideoPictureTitle    { get; init; } = "PICTURE";
    public string VideoBrightnessLabel { get; init; } = "Brightness";
    public string VideoContrastLabel   { get; init; } = "Contrast";
    public string VideoSaturationLabel { get; init; } = "Saturation";
    public string VideoHueLabel        { get; init; } = "Hue";
    public string VideoResetPicture    { get; init; } = "Reset to Default";

    // ---- Overscan mode display names ----
    public string OverscanOverscan  { get; init; } = "Overscan";
    public string OverscanNormal    { get; init; } = "Normal";
    public string OverscanUnderscan { get; init; } = "Underscan";

    // ---- Shared sound items ----
    public string SoundVolume      { get; init; } = "Volume";
    public string AudioFilterLabel { get; init; } = "Audio Filter";
    public string AudioFilterTitle { get; init; } = "AUDIO FILTER";
    /// <summary>Main menu only — not shown in the in-game pause menu.</summary>
    public string SoundMusicOn  { get; init; } = "Menu Music: On";
    public string SoundMusicOff { get; init; } = "Menu Music: Off";

    // ---- Audio EQ sub-menu ----
    public string AudioEqLabel   { get; init; } = "EQ";
    public string AudioEqTitle   { get; init; } = "AUDIO EQ";
    public string AudioEqBass    { get; init; } = "Bass";
    public string AudioEqMid     { get; init; } = "Mid";
    public string AudioEqTreble  { get; init; } = "Treble";
    public string AudioEqFlat    { get; init; } = "Flat";
    public string AudioEqCustom  { get; init; } = "Custom";
    public string AudioEqReset   { get; init; } = "Reset to Default";

    // ---- Audio filter mode display names ----
    public string AudioFilterDefault      { get; init; } = "Default";
    public string AudioFilterWarm         { get; init; } = "Warm";
    public string AudioFilterPseudoStereo { get; init; } = "Pseudo Stereo";
    public string AudioFilterWarmStereo   { get; init; } = "Warm Stereo";
    public string AudioFilterCompression  { get; init; } = "Compression";
    public string AudioFilterBassBoost     { get; init; } = "Bass Boost";
    public string AudioFilterSaturation   { get; init; } = "Saturation";
    public string AudioFilterDmcStabilizer { get; init; } = "Pop Filter";

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
    /// <summary>Shown in the binding list when a key or button has not been assigned.</summary>
    public string BindNone   { get; init; } = "(none)";

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
    /// <summary>Only shown in multi-game mode — see MultiGameMode.IsActive.</summary>
    public string MainMenuChangeGame { get; init; } = "Change Game";
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
    /// <summary>Only shown in multi-game mode — see MultiGameMode.IsActive.</summary>
    public string InGameChangeGameTitle { get; init; } = "CHANGE GAME?";

    // ---- In-game menu: root items ----
    public string InGameResume        { get; init; } = "Resume";
    public string InGameResetGame     { get; init; } = "Reset Game";
    public string InGameSelectSaveSlot { get; init; } = "Select Save Slot";
    public string InGameSaveGame      { get; init; } = "Save Game";
    public string InGameLoadGame      { get; init; } = "Load Game";
    public string InGameSettings      { get; init; } = "Settings";
    public string InGameReturnToMain  { get; init; } = "Return to Main Menu";
    /// <summary>Only shown in multi-game mode — see MultiGameMode.IsActive.</summary>
    public string InGameChangeGame    { get; init; } = "Change Game";
    public string InGameExit          { get; init; } = "Exit";

    // ---- In-game menu: confirmation screens ----
    public string InGameConfirmYesLoad   { get; init; } = "Yes, load game";
    public string InGameConfirmNoStay    { get; init; } = "No, stay in game";
    public string InGameConfirmYesReturn { get; init; } = "Yes, return to main menu";
    public string InGameConfirmYesExit   { get; init; } = "Yes, exit to desktop";
    /// <summary>Only shown in multi-game mode — see MultiGameMode.IsActive.</summary>
    public string InGameConfirmYesChangeGame { get; init; } = "Yes, change game";
    public string InGameConfirmWarning   { get; init; } = "Unsaved progress will be lost.";

    // ---- In-game menu: rebind prompts ----
    public string InGameRebindPressKey               { get; init; } = "Press any key to bind\n(Esc to cancel)";
    public string InGameRebindPressButton            { get; init; } = "Press any controller button\n(Start to cancel)";
    public string InGameRebindPressButtonNoCancel    { get; init; } = "Press any controller button\n(Esc to cancel)";
    public string InGameRebindStartReserved          { get; init; } = "Start is reserved for the menu";

    // ---- System section (shown when overrideStartBindingProtection is on) ----
    public string SystemSectionLabel                 { get; init; } = "SYSTEM";
    public string BindOpenMenu                       { get; init; } = "Open Menu";
    public string MainMenuRebindPressButtonNoCancel  { get; init; } = "Press any controller button  •  Esc to cancel";

    // ---- Controller diagram label (shown above the NES controller illustration on binding screens) ----
    public string NesControllerLabel { get; init; } = "NES Controller";

    // ---- Video presets sub-menu ----
    public string VideoPresetsLabel       { get; init; } = "Presets";
    public string VideoPresetsTitle       { get; init; } = "VIDEO PRESETS";
    public string VideoPresetNoPreset     { get; init; } = "No Preset";
    public string VideoPresetNoFilters    { get; init; } = "No Filters";
    public string VideoPresetLivingRoom   { get; init; } = "Living Room";
    public string VideoPresetArcade       { get; init; } = "Arcade Monitor";
    public string VideoPresetSharp        { get; init; } = "Sharp";
    public string VideoPresetPhosphor     { get; init; } = "Phosphor";

    // ---- In-game controller-disconnected screen ----
    public string ControllerDisconnectedTitle { get; init; } = "Controller Disconnected";
    public string ControllerDisconnectedHint  { get; init; } = "Press any button to continue…";

    // ---- In-game hotkey toast messages ----
    /// <summary>Format string — {0} is the 1-based slot number.</summary>
    public string ToastSavedToSlot  { get; init; } = "Saved to Slot {0}";
    /// <summary>Format string — {0} is the 1-based slot number.</summary>
    public string ToastLoadedSlot   { get; init; } = "Loaded Slot {0}";
    /// <summary>Format string — {0} is the 1-based slot number.</summary>
    public string ToastSlotEmpty    { get; init; } = "Slot {0} — Empty";
    /// <summary>Format string — {0} is the 1-based slot number.</summary>
    public string ToastSlotSelected { get; init; } = "Slot {0} Selected";

    // ---- Multi-game carousel (only shown in multi-game mode — see MultiGameMode.IsActive) ----
    public string CarouselNoGamesAvailable { get; init; } = "No games available";
    /// <summary>Phrased by direction (Left/Right/Up) so it reads the same on keyboard or gamepad.</summary>
    public string CarouselLegendLine1      { get; init; } = "Left / Right: Browse    Up: Description    Enter / A: Select";
    public string CarouselLegendLine2      { get; init; } = "Esc / B: Quit    F11 / Y: Fullscreen";
    /// <summary>Headline shown over a structurally invalid game's box art — see GameManifest.IsValid.
    /// Deliberately generic: the specific reason is written to neshim.log (GameScanner,
    /// Logger.LogAlways) rather than shown to players — almost always either a Steam download
    /// problem Steam itself flags, or a publisher packaging mistake caught in testing.</summary>
    public string CarouselUnavailable      { get; init; } = "Game Error";
    public string CarouselContactPublisher { get; init; } = "Contact the publisher for support.";
    public string CarouselNoDescription    { get; init; } = "No description available.";
}
