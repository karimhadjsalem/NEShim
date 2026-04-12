using BizHawk.Emulation.Common;
using NEShim.Input;

namespace NEShim.Emulation;

/// <summary>
/// Implements IController by reading from an atomically-swapped InputSnapshot.
/// The emulation thread updates the snapshot before each FrameAdvance call.
/// </summary>
internal sealed class NesController : IController
{
    private volatile InputSnapshot _snapshot = InputSnapshot.Empty;

    public ControllerDefinition Definition { get; }

    public NesController(ControllerDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>Called by EmulationThread before each FrameAdvance.</summary>
    public void Update(InputSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public bool IsPressed(string button)
        => _snapshot.IsPressed(button);

    public int AxisValue(string name) => 0;

    public IReadOnlyCollection<(string Name, int Strength)> GetHapticsSnapshot()
        => Array.Empty<(string, int)>();

    public void SetHapticChannelStrength(string name, int strength) { }
}
