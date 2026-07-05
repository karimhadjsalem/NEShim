namespace NEShim.Rendering;

/// <summary>
/// Creates the appropriate <see cref="IOverlayRenderer"/> for the current configuration.
/// Returns <see cref="NullOverlayRenderer"/> when GDI+ is forced (no D3D11 device created);
/// otherwise returns an initialised <see cref="SteamOverlayRenderer"/>.
/// </summary>
internal static class OverlayRendererFactory
{
    internal static IOverlayRenderer Create(string forceRenderer, IntPtr hwnd, int width, int height)
    {
        if (forceRenderer.Equals("gdi", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Log("[Init] GDI+ forced — D3D overlay hook skipped.");
            return new NullOverlayRenderer();
        }

        var steamRenderer = new SteamOverlayRenderer();
        steamRenderer.Initialize(hwnd, width, height);
        Logger.Log($"[Init] D3D overlay hook initialised ({width}×{height}).");
        return steamRenderer;
    }
}
