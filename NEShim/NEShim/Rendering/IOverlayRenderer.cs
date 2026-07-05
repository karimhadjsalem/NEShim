using Vortice.Direct3D11;
using Vortice.DXGI;

namespace NEShim.Rendering;

/// <summary>
/// Provides the D3D11 device and swap chain required by <see cref="D3D11Renderer"/>,
/// and the Present/Resize heartbeat required by <see cref="GdiRenderer"/> to keep
/// Steam's GameOverlayRenderer64.dll hook alive.
/// </summary>
internal interface IOverlayRenderer : IDisposable
{
    /// <summary>
    /// The D3D11 device. Shared with <see cref="D3D11Renderer"/>.
    /// Null if initialisation failed or this is a null implementation.
    /// </summary>
    ID3D11Device? Device { get; }

    /// <summary>
    /// The DXGI swap chain. Shared with <see cref="D3D11Renderer"/>.
    /// Null if initialisation failed or this is a null implementation.
    /// </summary>
    IDXGISwapChain? SwapChain { get; }

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
