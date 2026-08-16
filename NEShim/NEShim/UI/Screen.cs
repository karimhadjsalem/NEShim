namespace NEShim.UI;

/// <summary>
/// Every screen reachable from either <see cref="InGameMenu"/> or <see cref="MainMenuScreen"/>.
/// Unified into one enum (previously two separate nested enums, one per menu, sharing 12 of their
/// members by coincidence of spelling) so the ~10 handler classes whose logic is identical between
/// the two menus can be shared without being generic over a screen type. Each menu's
/// <c>BuildHandlers()</c> only ever populates its own subset as dictionary keys — an enum member
/// that's meaningless for a given menu (e.g. <see cref="ConfirmExit"/> for <see cref="MainMenuScreen"/>)
/// is simply never looked up there, exactly as harmless as it was as a member of an unrelated type.
/// </summary>
public enum Screen
{
    // ---- Top screens — exactly one is ever the tree root for a given menu instance ----
    Root,           // InGameMenu only — pause-menu root
    Main,           // MainMenuScreen only — New Game / Resume / Settings / Exit
    ResumeSlots,    // MainMenuScreen only — resume-slot picker shown before a game starts

    // ---- Shared screens (identical handler logic in both menus) ----
    Settings, KeyboardBindings, GamepadBindings,
    Video, Sound, AudioFilter, AudioEq, VideoFilter, VideoMotionEffect, VideoPicture, VideoPresets, Language,

    // ---- InGameMenu only ----
    SaveSlotSelect, ConfirmLoad, ConfirmMainMenu, ConfirmExit, ConfirmChangeGame, ControllerDisconnected,
}
