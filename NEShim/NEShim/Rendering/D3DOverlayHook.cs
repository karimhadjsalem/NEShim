using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace NEShim.Rendering;

/// <summary>
/// Creates a minimal D3D11 swap chain on the main application window so that
/// Steam's GameOverlayRenderer64.dll can hook IDXGISwapChain::Present and enable
/// the overlay. Without a Present() call, Steam's overlay DLL does not activate.
///
/// <para>Steam renders its overlay UI directly into the swap chain's back buffer
/// via the vtable hook. Because <see cref="GamePanel"/> (a GDI+ child control)
/// is composited above the swap chain surface by DWM, the overlay is only visible
/// when GamePanel is hidden — see MainForm's overlay toggle handler.</para>
///
/// <para>If D3D11 is unavailable, initialization silently fails and all methods
/// become no-ops; the game continues without overlay support.</para>
/// </summary>
internal sealed class D3DOverlayHook : IDisposable
{
    private ID3D11Device?   _device;
    private IDXGISwapChain? _swapChain;

    /// <summary>
    /// Creates the D3D11 device and swap chain bound to <paramref name="hwnd"/>.
    /// Pass the top-level MainForm handle. Must be called on the UI thread after
    /// the window has been sized to its final dimensions.
    /// </summary>
    public void Initialize(IntPtr hwnd, int width, int height)
    {
        try
        {
            var result = D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.None,
                new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_0, FeatureLevel.Level_9_3 },
                out _device);

            if (result.Failure || _device is null)
                return;

            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var adapter    = dxgiDevice.GetAdapter();
            using var factory    = adapter.GetParent<IDXGIFactory>();

            _swapChain = factory.CreateSwapChain(_device, new SwapChainDescription
            {
                BufferCount       = 2,
                BufferDescription = new ModeDescription(
                    (uint)Math.Max(width,  1),
                    (uint)Math.Max(height, 1),
                    Format.B8G8R8A8_UNorm),
                BufferUsage       = Usage.RenderTargetOutput,
                OutputWindow      = hwnd,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect        = SwapEffect.Discard,
                Windowed          = true,
            });

            // Prevent DXGI from hijacking Alt+Enter — window mode is managed by MainForm.
            factory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);
        }
        catch (Exception ex)
        {
            Logger.Log($"[D3DOverlayHook] Init failed: {ex.Message}");
            _device?.Dispose();
            _device    = null;
            _swapChain = null;
        }
    }

    /// <summary>
    /// Call periodically on the UI thread alongside SteamAPI.RunCallbacks().
    /// Gives Steam's overlay hook a Present() frame to intercept.
    /// </summary>
    public void Present()
    {
        if (_swapChain is null) return;
        try { _swapChain.Present(0, PresentFlags.None); }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Resizes the swap chain buffers after a window resize or mode change.
    /// Must be called on the UI thread.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (_swapChain is null) return;
        try
        {
            _swapChain.ResizeBuffers(2,
                (uint)Math.Max(width,  1),
                (uint)Math.Max(height, 1),
                Format.B8G8R8A8_UNorm,
                SwapChainFlags.None);
        }
        catch { /* best-effort */ }
    }

    public void Dispose()
    {
        _swapChain?.Dispose();
        _device?.Dispose();
        _swapChain = null;
        _device    = null;
    }
}
