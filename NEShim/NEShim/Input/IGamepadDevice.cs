namespace NEShim.Input;

/// <summary>
/// Strategy interface for reading gamepad state. Implemented by <see cref="SDL3GamepadDevice"/>
/// for production use and substituted in tests via NSubstitute.
/// </summary>
internal interface IGamepadDevice : IDisposable
{
    GamepadState GetState(uint userIndex = 0);
}
