using SDL3;

namespace NEShim.Input;

/// <summary>
/// Strategy interface for reading gamepad state. Implemented by <see cref="SDL3GamepadDevice"/>
/// for production use and substituted in tests via NSubstitute.
/// </summary>
internal interface IGamepadDevice : IDisposable
{
    GamepadState GetState(uint userIndex = 0);

    /// <summary>
    /// Returns the SDL-detected controller brand for the currently open gamepad (cached, only
    /// re-read on a fresh connect), or <see cref="SDL.GamepadType.Unknown"/> when no gamepad is
    /// open. Used by <see cref="Sources.SdlBundledGlyphSource"/> for the offline glyph fallback.
    /// </summary>
    SDL.GamepadType GetGamepadType(uint userIndex = 0);
}
