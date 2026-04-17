using System.Collections.Immutable;

namespace NEShim.Input;

/// <summary>
/// Immutable snapshot of pressed NES button names captured once per frame.
/// Thread-safe: written by emulation thread, read by NesController.
/// </summary>
internal sealed class InputSnapshot
{
    public static readonly InputSnapshot Empty = new(ImmutableHashSet<string>.Empty);

    private readonly ImmutableHashSet<string> _pressed;

    public InputSnapshot(ImmutableHashSet<string> pressed)
    {
        _pressed = pressed;
    }

    public bool IsPressed(string button) => _pressed.Contains(button);
}
