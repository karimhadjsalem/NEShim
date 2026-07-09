using NEShim.Config;

namespace NEShim.Input;

/// <summary>
/// Strategy interface for edge-triggered menu navigation input.
/// Implemented by <see cref="Sources.SDL3GamepadSource"/> and <see cref="Sources.SteamInputSource"/>.
/// <see cref="InputManager.PollMenuNav"/> composes all implementations (any-true union).
/// </summary>
internal interface IMenuNavSource
{
    MenuNavInput GetMenuNav(AppConfig config);
}
