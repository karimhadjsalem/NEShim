using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace NEShim.Rendering;

/// <summary>
/// Renders NES frames directly to the D3D11 swap chain using a fullscreen passthrough quad.
/// Owned objects: NES texture, SRV, RTV, vertex buffer, shaders, input layout, sampler.
/// NOT owned: device and swap chain — those belong to <see cref="D3DOverlayHook"/>.
/// </summary>
internal sealed class D3D11Renderer : IDisposable
{
    // Not owned — created and disposed by D3DOverlayHook.
    private readonly ID3D11Device         _device;
    private readonly IDXGISwapChain       _swapChain;
    private readonly ID3D11DeviceContext  _context;

    // Owned D3D11 resources — disposed in Dispose().
    private readonly ID3D11Texture2D          _nesTexture;
    private readonly ID3D11ShaderResourceView _nesTextureView;
    private readonly ID3D11Buffer             _vertexBuffer;
    private readonly ID3D11VertexShader       _vertexShader;
    private readonly ID3D11PixelShader        _pixelShader;
    private readonly ID3D11InputLayout        _inputLayout;
    private readonly ID3D11SamplerState       _samplerState;

    // Recreated on Resize — not readonly.
    private ID3D11RenderTargetView _renderTargetView;

    private readonly int _nesWidth;
    private readonly int _nesHeight;
    private int _viewportWidth;
    private int _viewportHeight;

    private const int VertexStride = sizeof(float) * 4; // pos(xy) + texcoord(uv)

    public event EventHandler? DeviceLost;

    internal D3D11Renderer(ID3D11Device device, IDXGISwapChain swapChain, int nesWidth, int nesHeight)
    {
        _device    = device;
        _swapChain = swapChain;
        _context   = device.ImmediateContext;
        _nesWidth  = nesWidth;
        _nesHeight = nesHeight;

        // BizHawk IVideoProvider returns int[] where each int is 0xAARRGGBB.
        // In little-endian memory the bytes are [B, G, R, A] — BGRA — which maps
        // directly to B8G8R8A8_UNorm with no byte swapping required.
        _nesTexture = device.CreateTexture2D(new Texture2DDescription
        {
            Width             = (uint)nesWidth,
            Height            = (uint)nesHeight,
            MipLevels         = 1,
            ArraySize         = 1,
            Format            = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage             = ResourceUsage.Dynamic,
            BindFlags         = BindFlags.ShaderResource,
            CPUAccessFlags    = CpuAccessFlags.Write,
        });

        _nesTextureView = device.CreateShaderResourceView(_nesTexture);

        using var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = device.CreateRenderTargetView(backBuffer);

        var swapDesc = swapChain.Description;
        _viewportWidth  = (int)swapDesc.BufferDescription.Width;
        _viewportHeight = (int)swapDesc.BufferDescription.Height;

        // Fullscreen quad — two triangles covering clip space (-1,-1) to (1,1).
        // Vertex layout: float2 pos, float2 texcoord.
        float[] vertices =
        {
            -1f,  1f,  0f, 0f,  // top-left
             1f,  1f,  1f, 0f,  // top-right
            -1f, -1f,  0f, 1f,  // bottom-left
             1f,  1f,  1f, 0f,  // top-right
             1f, -1f,  1f, 1f,  // bottom-right
            -1f, -1f,  0f, 1f,  // bottom-left
        };
        unsafe
        {
            fixed (float* vertexDataPtr = vertices)
            {
                device.CreateBuffer(
                    new BufferDescription
                    {
                        ByteWidth = (uint)(vertices.Length * sizeof(float)),
                        Usage     = ResourceUsage.Immutable,
                        BindFlags = BindFlags.VertexBuffer,
                    },
                    new SubresourceData((IntPtr)vertexDataPtr, 0, 0),
                    out var vb);
                _vertexBuffer = vb!;
            }
        }

        byte[] vsBytes = LoadShaderResource("NEShim.Rendering.Shaders.Passthrough.vs.cso");
        byte[] psBytes = LoadShaderResource("NEShim.Rendering.Shaders.Passthrough.ps.cso");

        // DXVK on Proton compiles these DXBC bytecodes to SPIR-V at first launch
        // and caches them in ~/.local/share/Steam/steamapps/shadercache/<appid>/.
        // The passthrough shaders are trivially simple, so first-launch compile is
        // near-instant. Subsequent launches use the cached SPIR-V.
        _vertexShader = device.CreateVertexShader(vsBytes);
        _pixelShader  = device.CreatePixelShader(psBytes);

        _inputLayout = device.CreateInputLayout(
            new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0,  0, InputClassification.PerVertexData, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8,  0, InputClassification.PerVertexData, 0),
            },
            vsBytes);

        // Point-clamp sampler — preserves hard NES pixel edges (equivalent to
        // NearestNeighborScaler in the GDI+ path). Clamping prevents edge wrap artefacts.
        _samplerState = device.CreateSamplerState(new SamplerDescription
        {
            Filter             = Filter.MinMagMipPoint,
            AddressU           = TextureAddressMode.Clamp,
            AddressV           = TextureAddressMode.Clamp,
            AddressW           = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunction.Never,
            MaxAnisotropy      = 1,
            MaxLOD             = float.MaxValue,
        });

        Logger.Log($"[D3D11Renderer] Initialized ({nesWidth}×{nesHeight}, B8G8R8A8_UNorm, point-clamp). Renderer mode: D3D11.");
    }

    /// <summary>
    /// Uploads one NES frame's pixel data to the GPU texture.
    /// Must be called on the UI thread (via BeginInvoke from the emulation thread).
    /// </summary>
    internal unsafe void UploadFrame(ReadOnlySpan<int> nesPixels)
    {
        var mapped = _context.Map(_nesTexture, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var srcBytes = MemoryMarshal.AsBytes(nesPixels);
            int srcStride = _nesWidth * 4;

            // MappedSubresource.RowPitch is NOT guaranteed to equal nesWidth * 4.
            // DXVK aligns texture rows for Vulkan buffer compatibility — always
            // copy row-by-row and respect RowPitch, not the source stride.
            byte* dst = (byte*)mapped.DataPointer;
            for (int row = 0; row < _nesHeight; row++)
            {
                srcBytes.Slice(row * srcStride, srcStride)
                    .CopyTo(new Span<byte>(dst + row * mapped.RowPitch, srcStride));
            }
        }
        finally
        {
            _context.Unmap(_nesTexture, 0);
        }
    }

    /// <summary>
    /// Draws the last uploaded NES frame to the swap chain and calls Present.
    /// Called on the UI thread from the steamTimer at ~60 Hz.
    /// Also serves as the heartbeat that keeps the Steam overlay hook fed during pause.
    /// </summary>
    internal void DrawAndPresent(bool vsync)
    {
        _context.OMSetRenderTargets(_renderTargetView);
        _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
        _context.ClearRenderTargetView(_renderTargetView, new Color4(0f, 0f, 0f, 1f));

        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetVertexBuffer(0, _vertexBuffer, VertexStride);
        _context.IASetInputLayout(_inputLayout);

        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_pixelShader);
        _context.PSSetShaderResource(0, _nesTextureView);
        _context.PSSetSampler(0, _samplerState);

        _context.Draw(6, 0);

        var result = _swapChain.Present(vsync ? 1u : 0u, PresentFlags.None);

        // DXGI_ERROR_DEVICE_REMOVED = 0x887A0005, DXGI_ERROR_DEVICE_RESET = 0x887A0007
        if (result.Code == unchecked((int)0x887A0005) ||
            result.Code == unchecked((int)0x887A0007))
        {
            Logger.Log($"[D3D11Renderer] Device lost (HRESULT 0x{result.Code:X8}). Firing DeviceLost event.");
            DeviceLost?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Recreates size-dependent D3D11 resources after a window resize or mode change.
    /// Must be called on the UI thread.
    /// </summary>
    internal void Resize(int width, int height)
    {
        Logger.Log($"[D3D11Renderer] Resize to {width}×{height}.");

        // Unbind all pipeline state (including RTVs) before ResizeBuffers.
        _context.ClearState();
        _renderTargetView.Dispose();

        // DXVK ≤ 2.3 stalls one frame on ResizeBuffers for FlipDiscard swap chains.
        // Current Proton ships DXVK ≥ 2.4 where this is fixed; no workaround needed.
        _swapChain.ResizeBuffers(0,
            (uint)Math.Max(width,  1),
            (uint)Math.Max(height, 1),
            Format.Unknown,
            SwapChainFlags.None);

        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device.CreateRenderTargetView(backBuffer);
        _viewportWidth    = Math.Max(width,  1);
        _viewportHeight   = Math.Max(height, 1);
        _context.OMSetRenderTargets(_renderTargetView);
    }

    public void Dispose()
    {
        Logger.Log("[D3D11Renderer] Disposed.");
        _samplerState.Dispose();
        _renderTargetView.Dispose();
        _nesTextureView.Dispose();
        _nesTexture.Dispose();
        _vertexBuffer.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
        // _context, _device, _swapChain are not owned — do not dispose.
    }

    private static byte[] LoadShaderResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException(
                $"[D3D11Renderer] Embedded shader resource '{resourceName}' not found. This is a build error — recompile.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
