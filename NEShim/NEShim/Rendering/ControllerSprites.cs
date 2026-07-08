using System.Reflection;
using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Provides pre-rendered NES controller PNG sprites loaded from embedded resources.
/// One SDL surface per highlight state (none + 8 button names).
/// </summary>
internal static class ControllerSprites
{
    private static readonly IntPtr[] _surfaces = LoadAll();

    internal static int GetVariantIndex(string? activeButton) => activeButton switch
    {
        "P1 Up"     => 1,
        "P1 Down"   => 2,
        "P1 Left"   => 3,
        "P1 Right"  => 4,
        "P1 A"      => 5,
        "P1 B"      => 6,
        "P1 Start"  => 7,
        "P1 Select" => 8,
        _           => 0,
    };

    internal static IntPtr Get(string? activeButton) => _surfaces[GetVariantIndex(activeButton)];

    internal static void Dispose()
    {
        for (int i = 0; i < _surfaces.Length; i++)
        {
            if (_surfaces[i] != IntPtr.Zero)
                SDL.DestroySurface(_surfaces[i]);
            _surfaces[i] = IntPtr.Zero;
        }
    }

    private static IntPtr[] LoadAll()
    {
        string[] resourceNames =
        [
            "NEShim.Assets.controllers.controller_none.png",
            "NEShim.Assets.controllers.controller_p1_up.png",
            "NEShim.Assets.controllers.controller_p1_down.png",
            "NEShim.Assets.controllers.controller_p1_left.png",
            "NEShim.Assets.controllers.controller_p1_right.png",
            "NEShim.Assets.controllers.controller_p1_a.png",
            "NEShim.Assets.controllers.controller_p1_b.png",
            "NEShim.Assets.controllers.controller_p1_start.png",
            "NEShim.Assets.controllers.controller_p1_select.png",
        ];

        var assembly = Assembly.GetExecutingAssembly();
        var result = new IntPtr[resourceNames.Length];
        for (int i = 0; i < resourceNames.Length; i++)
        {
            using var stream = assembly.GetManifestResourceStream(resourceNames[i])
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceNames[i]}");
            result[i] = SdlSurfaceLoader.LoadFromStream(stream);
        }
        return result;
    }
}
