namespace NEShim.Rendering;

/// <summary>
/// Provides the D3D11 device and swap chain required by <see cref="D3D11Renderer"/>, and a
/// Present/Resize heartbeat to keep Steam's GameOverlayRenderer64.dll hook alive on Windows.
/// Windows: <see cref="SteamOverlayRenderer"/>. Linux: <see cref="NullOverlayRenderer"/>
/// (Steam's overlay is injected via LD_PRELOAD there instead, so no swap-chain hook is needed).
/// </summary>
internal interface IOverlayRenderer : IDisposable
{
    /// <summary>
    /// The D3D11 device native pointer. Shared with <see cref="D3D11Renderer"/>.
    /// Null if initialisation failed or this is a null implementation.
    /// </summary>
    nint? Device { get; }

    /// <summary>
    /// The DXGI swap chain native pointer. Shared with <see cref="D3D11Renderer"/>.
    /// Null if initialisation failed or this is a null implementation.
    /// </summary>
    nint? SwapChain { get; }

    /// <summary>
    /// Gives Steam's overlay hook a Present frame to intercept.
    /// Call periodically on the UI thread alongside SteamAPI.RunCallbacks().
    /// No-op if no swap chain is available.
    /// </summary>
    void Present();

    /// <summary>
    /// Resizes the swap chain buffers after a window resize or mode change.
    /// Must be called on the UI thread. No-op if no swap chain is available.
    /// </summary>
    void Resize(int width, int height);
}
