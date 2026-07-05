using NEShim.Platform;

namespace NEShim.Rendering;

/// <summary>
/// Creates the appropriate <see cref="IOverlayRenderer"/> for the current configuration.
/// Returns <see cref="NullOverlayRenderer"/> when GDI+ is forced (no D3D11 device created);
/// otherwise returns an initialised <see cref="SteamOverlayRenderer"/>.
/// </summary>
internal static class OverlayRendererFactory
{
    internal static IOverlayRenderer Create(string forceRenderer, IWindowHost windowHost)
    {
        if (forceRenderer.Equals("gdi", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Log("[Init] GDI+ forced — D3D overlay hook skipped.");
            return new NullOverlayRenderer();
        }

        var steamRenderer = new SteamOverlayRenderer();
        steamRenderer.Initialize(windowHost.Handle, windowHost.ClientWidth, windowHost.ClientHeight);
        Logger.Log($"[Init] D3D overlay hook initialised ({windowHost.ClientWidth}×{windowHost.ClientHeight}).");
        return steamRenderer;
    }
}
