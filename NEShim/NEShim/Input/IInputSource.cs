using System.Collections.Generic;
using NEShim.Config;

namespace NEShim.Input;

/// <summary>
/// Strategy interface for raw hardware input acquisition.
/// Each implementation returns device-specific string identifiers
/// (key names, gamepad button names, or Steam action names) for
/// currently active inputs on that device.
/// </summary>
internal interface IInputSource
{
    /// <summary>
    /// True when this device is connected and supplying native input.
    /// Updated as a side-effect of each <see cref="GetActiveIdentifiers"/> call.
    /// Keyboard always returns true; Steam returns false during XInput passthrough.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns the set of currently active raw device identifiers.
    /// Always updates <see cref="IsAvailable"/> as a side-effect.
    /// Returns an empty set when the device is disconnected; never null.
    /// </summary>
    IReadOnlySet<string> GetActiveIdentifiers(AppConfig config);
}
