namespace NEShim.Config;

public sealed class InputBinding
{
    public string? Key           { get; set; }
    public string? GamepadButton { get; set; }

    public InputBinding() { }
    public InputBinding(string? key, string? gamepadButton)
    {
        Key           = key;
        GamepadButton = gamepadButton;
    }
}
