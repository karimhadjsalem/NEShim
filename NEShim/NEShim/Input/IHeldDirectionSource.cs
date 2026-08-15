using NEShim.Config;

namespace NEShim.Input;

/// <summary>
/// Strategy interface for continuous held-direction detection, used to drive slider
/// auto-repeat (Volume, Audio EQ bands, Picture Adjustment) while a menu is open.
/// Implemented by <see cref="Sources.SteamInputSource"/>. Keyboard and SDL3 gamepad don't need
/// this seam — <see cref="InputManager.GetHeldSliderDir"/> reads their held state directly from
/// <see cref="KeyboardInputSource"/>/<see cref="IGamepadDevice"/>, which are already injected.
/// </summary>
internal interface IHeldDirectionSource
{
    (bool Left, bool Right) GetHeldLeftRight(AppConfig config);
}
