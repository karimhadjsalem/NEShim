using NEShim.Platform;

namespace NEShim.Rendering;

/// <summary>
/// Creates the <see cref="SteamOverlayRenderer"/> for the current host window.
/// </summary>
internal static class OverlayRendererFactory
{
    internal static IOverlayRenderer Create(IWindowHost windowHost)
    {
        var steamRenderer = new SteamOverlayRenderer();
        steamRenderer.Initialize(windowHost.Handle, windowHost.ClientWidth, windowHost.ClientHeight);
        Logger.Log($"[Init] D3D overlay hook initialised ({windowHost.ClientWidth}×{windowHost.ClientHeight}).");
        return steamRenderer;
    }
}
