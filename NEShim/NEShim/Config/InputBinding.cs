namespace NEShim.Config;

public sealed class InputBinding
{
    public string? Key            { get; set; }
    public string? GamepadButton  { get; set; }
    /// <summary>
    /// Secondary gamepad binding slot — used for analog-stick identifiers ("AnalogUp" etc.)
    /// so both D-pad and analog stick work simultaneously without hardcoding the mapping in
    /// the mapper. Set in the default config; the binding UI only writes to GamepadButton.
    /// </summary>
    public string? GamepadButton2 { get; set; }

    public InputBinding() { }
    public InputBinding(string? key, string? gamepadButton)
    {
        Key           = key;
        GamepadButton = gamepadButton;
    }
}
