using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace NEShim.Rendering;

/// <summary>
/// Renders NES frames directly to the D3D11 swap chain using a fullscreen passthrough quad.
/// Handles letterbox aspect ratio, sidebar images, and GDI+-sourced overlay textures
/// (FPS counter, toasts, achievement notifications).
///
/// Owned objects: NES texture, SRV, RTV, vertex buffer, shaders, input layout, sampler,
/// blend state, overlay texture + bitmap, sidebar textures.
/// NOT owned: device and swap chain — those belong to <see cref="D3DOverlayHook"/>.
/// </summary>
internal sealed class D3D11Renderer : IFrameRenderer
{
    // Not owned — created and disposed by D3DOverlayHook.
    private readonly ID3D11Device        _device;
    private readonly IDXGISwapChain      _swapChain;
    private readonly ID3D11DeviceContext _context;

    // Owned D3D11 resources — all disposed in Dispose().
    private readonly ID3D11Texture2D          _nesTexture;
    private readonly ID3D11ShaderResourceView _nesTextureView;
    private readonly ID3D11Buffer             _vertexBuffer;
    private readonly ID3D11VertexShader       _vertexShader;
    private readonly ID3D11PixelShader        _passthroughPixelShader;
    private readonly ID3D11InputLayout        _inputLayout;
    private readonly ID3D11SamplerState       _samplerState;
    private readonly ID3D11BlendState         _alphaBlendState;
    private readonly ID3D11Buffer             _filterCbuffer;

    // Shader cache — keyed by resource name; passthrough reuses _passthroughPixelShader.
    private readonly Dictionary<string, ID3D11PixelShader> _shaderCache = new();

    // Active pixel shader — points at passthrough or a cached structural shader.
    private ID3D11PixelShader _activePixelShader;

    // Recreated on Resize.
    private ID3D11RenderTargetView _renderTargetView;

    // Viewport-sized overlay texture + GDI+ bitmap — recreated on Resize.
    // Used for FPS counter, toasts, and achievement notifications.
    private ID3D11Texture2D?          _overlayTexture;
    private ID3D11ShaderResourceView? _overlaySrv;
    private Bitmap?                   _overlayBitmap;
    private volatile bool             _overlayDirty = true;

    // Sidebar textures — recreated when SetSidebars is called.
    private ID3D11Texture2D?          _leftSidebarTex;
    private ID3D11ShaderResourceView? _leftSidebarSrv;
    private System.Drawing.Size       _leftSidebarBmpSize;
    private ID3D11Texture2D?          _rightSidebarTex;
    private ID3D11ShaderResourceView? _rightSidebarSrv;
    private System.Drawing.Size       _rightSidebarBmpSize;
    private bool                      _hasSidebars;

    private readonly int _nesWidth;
    private readonly int _nesHeight;
    private int _viewportWidth;
    private int _viewportHeight;

    private Filters.ID3D11Filter   _activeFilter    = new Filters.PixelPerfectD3D11Filter();
    private OverscanMode           _overscanMode    = OverscanMode.Overscan;
    private VideoColorFilterMode   _activeColorMode = VideoColorFilterMode.None;

    private const float UnderscanScale = 0.88f;

    // Active NES content size as reported by the emulator. Updated in UploadFrame.
    private int _contentWidth;
    private int _contentHeight;

    // UV range for the NES quad. Normally v0=0, v1=contentHeight/textureHeight.
    // In Overscan mode, 8 rows are cropped from top (v0) and bottom (v1).
    private float _nesV0 = 0f;
    private float _nesV1 = 1f;

    // Effective display height used for aspect ratio computation.
    // Differs from _contentHeight when overscan mode is active.
    private int _displayHeight;

    private const int OverscanCropRows = 8;

    // Letterbox clip-space edges — updated by UpdateLetterboxRect().
    // _nesY0 is the TOP edge (higher clip-space y), _nesY1 is the BOTTOM (lower clip-space y).
    private float _nesX0, _nesX1, _nesY0, _nesY1;

    // Menu/logo scene provider — set by MainForm after renderer creation.
    private IMenuSceneProvider? _menuSceneProvider;

    // Overlay state — FPS fields written from emulation thread (volatile), rest from UI thread.
    private volatile bool  _showFps;
    private volatile float _currentFps;
    private string?  _toastText;
    private DateTime _toastExpiry;

    private const int VertexStride = sizeof(float) * 4; // pos(xy) + texcoord(uv)

    public bool OwnsFrameSurface => true;

    public void SetMenuSceneProvider(IMenuSceneProvider? provider) => _menuSceneProvider = provider;
    public void MarkOverlayDirty() => _overlayDirty = true;

    public event EventHandler? DeviceLost;

    internal D3D11Renderer(ID3D11Device device, IDXGISwapChain swapChain, int nesWidth, int nesHeight)
    {
        _device        = device;
        _swapChain     = swapChain;
        _context       = device.ImmediateContext;
        _nesWidth      = nesWidth;
        _nesHeight     = nesHeight;
        _contentWidth  = nesWidth;
        _contentHeight = nesHeight;
        _displayHeight = nesHeight;

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

        // Dynamic vertex buffer — updated each draw call to support the NES quad,
        // sidebar quads, and overlay quad from a single buffer object.
        unsafe
        {
            float[] placeholder = new float[6 * 4]; // 6 vertices × (pos_xy + uv)
            fixed (float* p = placeholder)
            {
                device.CreateBuffer(
                    new BufferDescription
                    {
                        ByteWidth      = (uint)(placeholder.Length * sizeof(float)),
                        Usage          = ResourceUsage.Dynamic,
                        BindFlags      = BindFlags.VertexBuffer,
                        CPUAccessFlags = CpuAccessFlags.Write,
                    },
                    new SubresourceData((IntPtr)p, 0, 0),
                    out var vb);
                _vertexBuffer = vb!;
            }
        }

        byte[] vsBytes = LoadShaderResource("NEShim.Rendering.Shaders.Passthrough.vs.cso");
        byte[] psBytes = LoadShaderResource("NEShim.Rendering.Shaders.Passthrough.ps.cso");

        // DXVK on Proton compiles these DXBC bytecodes to SPIR-V at first launch
        // and caches them in ~/.local/share/Steam/steamapps/shadercache/<appid>/.
        // The passthrough shaders are trivially simple, so first-launch compile is near-instant.
        _vertexShader           = device.CreateVertexShader(vsBytes);
        _passthroughPixelShader = device.CreatePixelShader(psBytes);
        _activePixelShader      = _passthroughPixelShader;

        // 16-byte cbuffer (4 floats): structural params [0..2] + colorMode [3].
        // Always present — every pixel shader reads from b0.
        unsafe
        {
            float[] zeros = new float[4];
            fixed (float* p = zeros)
            {
                device.CreateBuffer(
                    new BufferDescription
                    {
                        ByteWidth      = (uint)(4 * sizeof(float)),
                        Usage          = ResourceUsage.Dynamic,
                        BindFlags      = BindFlags.ConstantBuffer,
                        CPUAccessFlags = CpuAccessFlags.Write,
                    },
                    new SubresourceData((IntPtr)p, 0, 0),
                    out var cb);
                _filterCbuffer = cb!;
            }
        }

        _inputLayout = device.CreateInputLayout(
            new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0, InputClassification.PerVertexData, 0),
            },
            vsBytes);

        // Point-clamp sampler — preserves hard NES pixel edges.
        // Clamping prevents edge wrap artefacts on the NES texture boundary.
        _samplerState = device.CreateSamplerState(new SamplerDescription
        {
            Filter        = Filter.MinMagMipPoint,
            AddressU      = TextureAddressMode.Clamp,
            AddressV      = TextureAddressMode.Clamp,
            AddressW      = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunction.Never,
            MaxAnisotropy = 1,
            MaxLOD        = float.MaxValue,
        });

        // Alpha blend state for the GDI+ overlay quad (FPS, toast, achievement).
        var blendDesc = new BlendDescription();
        blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
        {
            BlendEnable           = true,
            SourceBlend           = Vortice.Direct3D11.Blend.SourceAlpha,
            DestinationBlend      = Vortice.Direct3D11.Blend.InverseSourceAlpha,
            BlendOperation        = BlendOperation.Add,
            SourceBlendAlpha      = Vortice.Direct3D11.Blend.One,
            DestinationBlendAlpha = Vortice.Direct3D11.Blend.InverseSourceAlpha,
            BlendOperationAlpha   = BlendOperation.Add,
            RenderTargetWriteMask = ColorWriteEnable.All,
        };
        _alphaBlendState = device.CreateBlendState(blendDesc);

        CreateOverlayResources();
        UpdateOverscanUV();
        UpdateLetterboxRect();

        Logger.Log($"[D3D11Renderer] Initialized ({nesWidth}×{nesHeight}, B8G8R8A8_UNorm, point-clamp). Renderer mode: D3D11.");
    }

    // ---- IFrameRenderer ----------------------------------------------------------------

    /// <summary>
    /// Uploads one NES frame's pixel data to the GPU texture.
    /// Called on the UI thread via BeginInvoke from the emulation thread.
    /// </summary>
    public unsafe void UploadFrame(ReadOnlySpan<int> nesPixels, int contentWidth, int contentHeight)
    {
        if (contentWidth != _contentWidth || contentHeight != _contentHeight)
        {
            _contentWidth  = contentWidth;
            _contentHeight = contentHeight;
            UpdateOverscanUV();
            UpdateLetterboxRect();
        }

        var mapped = _context.Map(_nesTexture, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var srcBytes = MemoryMarshal.AsBytes(nesPixels);
            int srcStride = _nesWidth * 4;

            // MappedSubresource.RowPitch is NOT guaranteed to equal nesWidth * 4.
            // DXVK aligns texture rows for Vulkan buffer compatibility — always
            // copy row-by-row and respect RowPitch, not the source stride.
            byte* dst = (byte*)mapped.DataPointer;
            for (int row = 0; row < contentHeight; row++)
                srcBytes.Slice(row * srcStride, srcStride)
                    .CopyTo(new Span<byte>(dst + row * mapped.RowPitch, srcStride));
        }
        finally
        {
            _context.Unmap(_nesTexture, 0);
        }
    }

    /// <summary>
    /// Draws the last uploaded NES frame (plus sidebars and overlays) and calls Present.
    /// Called on the UI thread from steamTimer at ~60 Hz.
    /// Also serves as the heartbeat that keeps the Steam overlay hook fed during pause.
    /// </summary>
    public void Tick(bool vsync) => DrawAndPresent(vsync);

    public void UpdateFpsOverlay(bool show, float fps)
    {
        _showFps    = show;
        _currentFps = fps;
        _overlayDirty = true;
    }

    public void SetSidebars(Bitmap? left, Bitmap? right)
    {
        UploadSidebarBitmap(left,  ref _leftSidebarTex,  ref _leftSidebarSrv,  ref _leftSidebarBmpSize);
        UploadSidebarBitmap(right, ref _rightSidebarTex, ref _rightSidebarSrv, ref _rightSidebarBmpSize);
        _hasSidebars = _leftSidebarSrv is not null || _rightSidebarSrv is not null;
    }

    public void ShowToast(string text)
    {
        _toastText   = text;
        _toastExpiry = DateTime.UtcNow.AddSeconds(OverlayRenderer.ToastDurationSeconds);
        _overlayDirty = true;
    }

    // No-op in D3D11 mode — Steam's overlay shows its own achievement notification
    // when SetAchievement + StoreStats are called. GdiRenderer keeps its custom banner
    // as a fallback for when the overlay isn't available.
    public void ShowAchievementNotification(string name) { }

    /// <summary>
    /// Injects a new D3D11 filter. Swaps the active pixel shader and updates the destination rect.
    /// Called by MainForm with a filter created by D3D11FilterFactory.
    /// </summary>
    public void SetFilter(Filters.ID3D11Filter filter)
    {
        _activeFilter      = filter;
        _activePixelShader = ResolvePixelShader(filter);
        UpdateLetterboxRect();
    }

    /// <summary>
    /// Updates the active colour grade applied on top of the structural filter.
    /// No layout or rect change required — colour is a purely per-pixel operation.
    /// </summary>
    public void SetColorFilter(VideoColorFilterMode mode)
    {
        _activeColorMode = mode;
    }

    /// <inheritdoc/>
    public void SetOverscanMode(OverscanMode mode)
    {
        _overscanMode = mode;
        UpdateOverscanUV();
        UpdateLetterboxRect();
    }

    /// <summary>
    /// Applies filter, overscan, and colour grade in one call. Used at startup and after device recovery.
    /// </summary>
    public void InitializeRenderingOptions(
        Filters.ID3D11Filter filter, OverscanMode overscan, VideoColorFilterMode colorMode = VideoColorFilterMode.None)
    {
        _activeFilter      = filter;
        _activePixelShader = ResolvePixelShader(filter);
        _overscanMode      = overscan;
        _activeColorMode   = colorMode;
        UpdateOverscanUV();
        UpdateLetterboxRect();
    }

    /// <summary>
    /// Recreates size-dependent D3D11 resources after a window resize or mode change.
    /// Must be called on the UI thread.
    /// </summary>
    public void Resize(int width, int height)
    {
        Logger.Log($"[D3D11Renderer] Resize to {width}×{height}.");

        // Unbind all pipeline state (including RTVs) before ResizeBuffers.
        _context.ClearState();
        _renderTargetView.Dispose();

        DisposeOverlayResources();

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

        CreateOverlayResources();
        UpdateLetterboxRect();
    }

    public void Dispose()
    {
        Logger.Log("[D3D11Renderer] Disposed.");
        DisposeOverlayResources();
        DisposeSidebarResources();
        _alphaBlendState.Dispose();
        _samplerState.Dispose();
        _renderTargetView.Dispose();
        _nesTextureView.Dispose();
        _nesTexture.Dispose();
        _vertexBuffer.Dispose();
        _filterCbuffer.Dispose();
        _inputLayout.Dispose();
        foreach (var shader in _shaderCache.Values)
            shader.Dispose();
        _passthroughPixelShader.Dispose();
        _vertexShader.Dispose();
        // _context, _device, _swapChain are not owned — do not dispose.
    }

    // ---- Internal draw -----------------------------------------------------------------

    private void DrawAndPresent(bool vsync)
    {
        _context.OMSetRenderTargets(_renderTargetView);
        _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
        _context.ClearRenderTargetView(_renderTargetView, new Color4(0f, 0f, 0f, 1f));

        SetupPipelineState();
        UpdateFilterCbuffer();

        // Draw sidebar images in letterbox bars (opaque, no blend).
        if (_hasSidebars)
            DrawSidebars();

        // Draw the letterboxed NES frame.
        // _nesV0/_nesV1 define the UV crop window (affected by overscan mode).
        _context.PSSetShaderResource(0, _nesTextureView);
        WriteQuadToVB(_nesX0, _nesY0, _nesX1, _nesY1, 0f, _nesV0, 1f, _nesV1);
        _context.Draw(6, 0);

        // Draw GDI+-sourced overlay (FPS, toast, achievement) — alpha-blended.
        DrawOverlay();

        var result = _swapChain.Present(vsync ? 1u : 0u, PresentFlags.None);

        // DXGI_ERROR_DEVICE_REMOVED = 0x887A0005, DXGI_ERROR_DEVICE_RESET = 0x887A0007
        if (result.Code == unchecked((int)0x887A0005) ||
            result.Code == unchecked((int)0x887A0007))
        {
            Logger.Log($"[D3D11Renderer] Device lost (HRESULT 0x{result.Code:X8}). Firing DeviceLost event.");
            DeviceLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetupPipelineState()
    {
        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetVertexBuffer(0, _vertexBuffer, VertexStride);
        _context.IASetInputLayout(_inputLayout);
        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_activePixelShader);
        _context.PSSetSampler(0, _samplerState);
    }

    private unsafe void UpdateFilterCbuffer()
    {
        Span<float> p = stackalloc float[4];
        _activeFilter.WriteBaseParams(p, _contentWidth, _contentHeight);
        p[3] = (float)_activeColorMode;

        var mapped = _context.Map(_filterCbuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            fixed (float* src = p)
                Buffer.MemoryCopy(src, (void*)mapped.DataPointer, 4 * sizeof(float), 4 * sizeof(float));
        }
        finally { _context.Unmap(_filterCbuffer, 0); }

        _context.PSSetConstantBuffers(0, 1, new[] { _filterCbuffer });
    }

    // ---- Sidebar rendering -------------------------------------------------------------

    private void DrawSidebars()
    {
        // Sidebar pixel width on each side = destX (the letterbox bar width).
        float sidebarPixelW = (_nesX0 + 1f) / 2f * _viewportWidth;

        if (_leftSidebarSrv is not null && _leftSidebarBmpSize != default)
        {
            var (u0, v0, u1, v1) = ComputeCoverUV(_leftSidebarBmpSize, sidebarPixelW, _viewportHeight);
            _context.PSSetShaderResource(0, _leftSidebarSrv);
            WriteQuadToVB(-1f, 1f, _nesX0, -1f, u0, v0, u1, v1);
            _context.Draw(6, 0);
        }

        if (_rightSidebarSrv is not null && _rightSidebarBmpSize != default)
        {
            var (u0, v0, u1, v1) = ComputeCoverUV(_rightSidebarBmpSize, sidebarPixelW, _viewportHeight);
            _context.PSSetShaderResource(0, _rightSidebarSrv);
            WriteQuadToVB(_nesX1, 1f, 1f, -1f, u0, v0, u1, v1);
            _context.Draw(6, 0);
        }
    }

    private void UploadSidebarBitmap(
        Bitmap?                           bmp,
        ref ID3D11Texture2D?              tex,
        ref ID3D11ShaderResourceView?     srv,
        ref System.Drawing.Size           bmpSize)
    {
        tex?.Dispose(); tex = null;
        srv?.Dispose(); srv = null;
        bmpSize = default;

        if (bmp is null) return;

        bmpSize = bmp.Size;
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            // GDI+ Format32bppArgb stores bytes [B,G,R,A] — same as B8G8R8A8_UNorm.
            var initData = new SubresourceData(bmpData.Scan0, (uint)Math.Abs(bmpData.Stride), 0);
            tex = _device.CreateTexture2D(
                new Texture2DDescription
                {
                    Width             = (uint)bmp.Width,
                    Height            = (uint)bmp.Height,
                    MipLevels         = 1,
                    ArraySize         = 1,
                    Format            = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage             = ResourceUsage.Immutable,
                    BindFlags         = BindFlags.ShaderResource,
                },
                new[] { initData });
            srv = _device.CreateShaderResourceView(tex);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }
    }

    // ---- Overlay rendering (scene + FPS, toast, achievement) ---------------------------

    private void DrawOverlay()
    {
        // Expire elapsed notifications.
        var now = DateTime.UtcNow;
        if (_toastText is not null && now >= _toastExpiry) { _toastText = null; _overlayDirty = true; }

        bool hasTransient = _showFps || _toastText is not null;
        bool hasScene     = _menuSceneProvider?.GetActiveScenePainter() is not null;

        if (!hasTransient && !hasScene) return;
        if (_overlayBitmap is null || _overlayTexture is null) return;

        // Upload only when dirty. All state changes that affect the overlay
        // (input, navigation, timer ticks for animations) call MarkOverlayDirty().
        // Static menu frames between inputs produce no GDI+ render or GPU upload.
        if (_overlayDirty)
        {
            RenderOverlayBitmap();
            UploadOverlayBitmap();
            _overlayDirty = false;
        }

        if (_overlaySrv is null) return;

        // The overlay is GDI+-rendered content (menus, frozen frame, HUD). Draw it through
        // the passthrough shader so structural filters (scanlines, NTSC) are not applied to
        // the 2D overlay bitmap. Color grade (colorMode) is still applied via the cbuffer
        // because the passthrough shader reads it.
        _context.PSSetShader(_passthroughPixelShader);
        _context.OMSetBlendState(_alphaBlendState);
        _context.PSSetShaderResource(0, _overlaySrv);
        WriteQuadToVB(-1f, 1f, 1f, -1f);
        _context.Draw(6, 0);
        _context.OMSetBlendState(null);
        _context.PSSetShader(_activePixelShader);
    }

    private void RenderOverlayBitmap()
    {
        var clientRect = new Rectangle(0, 0, _viewportWidth, _viewportHeight);
        using var g = Graphics.FromImage(_overlayBitmap!);
        g.Clear(System.Drawing.Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;

        // Draw active menu/logo scene first; transient overlays composite on top.
        _menuSceneProvider?.GetActiveScenePainter()?.Invoke(g, clientRect);

        if (_showFps)
            OverlayRenderer.DrawFps(g, clientRect, _currentFps);

        if (_toastText is not null)
            OverlayRenderer.DrawToast(g, clientRect, _toastText);
    }

    private unsafe void UploadOverlayBitmap()
    {
        var bmpData = _overlayBitmap!.LockBits(
            new Rectangle(0, 0, _overlayBitmap.Width, _overlayBitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var mapped = _context.Map(_overlayTexture!, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                int srcStride = Math.Abs(bmpData.Stride);
                byte* dst = (byte*)mapped.DataPointer;
                byte* src = (byte*)bmpData.Scan0;
                for (int row = 0; row < _overlayBitmap.Height; row++)
                    Buffer.MemoryCopy(src + row * srcStride, dst + row * mapped.RowPitch, srcStride, srcStride);
            }
            finally { _context.Unmap(_overlayTexture!, 0); }
        }
        finally { _overlayBitmap.UnlockBits(bmpData); }
    }

    private void CreateOverlayResources()
    {
        _overlayBitmap  = new Bitmap(_viewportWidth, _viewportHeight, PixelFormat.Format32bppArgb);
        _overlayTexture = _device.CreateTexture2D(new Texture2DDescription
        {
            Width             = (uint)_viewportWidth,
            Height            = (uint)_viewportHeight,
            MipLevels         = 1,
            ArraySize         = 1,
            Format            = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage             = ResourceUsage.Dynamic,
            BindFlags         = BindFlags.ShaderResource,
            CPUAccessFlags    = CpuAccessFlags.Write,
        });
        _overlaySrv   = _device.CreateShaderResourceView(_overlayTexture);
        _overlayDirty = true;
    }

    private void DisposeOverlayResources()
    {
        _overlaySrv?.Dispose();     _overlaySrv     = null;
        _overlayTexture?.Dispose(); _overlayTexture = null;
        _overlayBitmap?.Dispose();  _overlayBitmap  = null;
    }

    private void DisposeSidebarResources()
    {
        _leftSidebarSrv?.Dispose();  _leftSidebarSrv  = null;
        _leftSidebarTex?.Dispose();  _leftSidebarTex  = null;
        _rightSidebarSrv?.Dispose(); _rightSidebarSrv = null;
        _rightSidebarTex?.Dispose(); _rightSidebarTex = null;
    }

    // ---- Overscan UV helpers -----------------------------------------------------------

    /// <summary>
    /// Recomputes _nesV0, _nesV1, and _displayHeight from the current overscan mode and
    /// content height. Must be called before UpdateLetterboxRect whenever either changes.
    /// </summary>
    private void UpdateOverscanUV()
    {
        if (_overscanMode == OverscanMode.Overscan && _contentHeight >= OverscanCropRows * 2)
        {
            _nesV0         = (float)OverscanCropRows / _nesHeight;
            _nesV1         = (float)(_contentHeight - OverscanCropRows) / _nesHeight;
            _displayHeight = _contentHeight - OverscanCropRows * 2;
        }
        else
        {
            _nesV0         = 0f;
            _nesV1         = (float)_contentHeight / _nesHeight;
            _displayHeight = _contentHeight;
        }
    }

    // ---- Vertex buffer helpers ---------------------------------------------------------

    /// <summary>
    /// Computes the letterbox clip-space edges using the NES 8:7 pixel aspect ratio.
    /// Applies Underscan scaling when the overscan mode is set to Underscan.
    /// Called once on construction and again after each Resize or overscan mode change.
    /// </summary>
    private void UpdateLetterboxRect()
    {
        // Use _activeFilter.PixelAspectRatio so future shader-based filters (e.g. NTSC,
        // which may alter perceived pixel width) are handled without touching this method.
        float displayAspect = _contentWidth * _activeFilter.PixelAspectRatio / _displayHeight;
        float windowAspect  = (float)_viewportWidth / _viewportHeight;

        float destW, destH;
        if (windowAspect > displayAspect)
        {
            destH = _viewportHeight;
            destW = destH * displayAspect;
        }
        else
        {
            destW = _viewportWidth;
            destH = destW / displayAspect;
        }

        if (_overscanMode == OverscanMode.Underscan)
        {
            destW *= UnderscanScale;
            destH *= UnderscanScale;
        }

        float destX = (_viewportWidth  - destW) / 2f;
        float destY = (_viewportHeight - destH) / 2f;

        // D3D clip space: x ∈ [-1,1], y=+1 at top, y=-1 at bottom.
        _nesX0 = (destX / _viewportWidth)           * 2f - 1f;  // left edge
        _nesX1 = ((destX + destW) / _viewportWidth) * 2f - 1f;  // right edge
        _nesY0 = 1f - (destY / _viewportHeight) * 2f;           // top edge
        _nesY1 = 1f - ((destY + destH) / _viewportHeight) * 2f; // bottom edge
    }

    /// <summary>
    /// Writes a quad (two triangles) into the dynamic vertex buffer.
    /// yTop > yBottom in D3D clip space (+y is up).
    /// </summary>
    private unsafe void WriteQuadToVB(
        float xLeft, float yTop, float xRight, float yBottom,
        float u0 = 0f, float v0 = 0f, float u1 = 1f, float v1 = 1f)
    {
        float[] verts =
        {
            xLeft,  yTop,    u0, v0,   // TL
            xRight, yTop,    u1, v0,   // TR
            xLeft,  yBottom, u0, v1,   // BL
            xRight, yTop,    u1, v0,   // TR
            xRight, yBottom, u1, v1,   // BR
            xLeft,  yBottom, u0, v1,   // BL
        };
        var mapped = _context.Map(_vertexBuffer, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            fixed (float* p = verts)
                Buffer.MemoryCopy(p, (void*)mapped.DataPointer,
                    verts.Length * sizeof(float), verts.Length * sizeof(float));
        }
        finally { _context.Unmap(_vertexBuffer, 0); }
    }

    /// <summary>
    /// Computes UV sub-rect for cover-scale rendering (zoom to fill, center-crop).
    /// Mirrors <see cref="OverlayRenderer.ComputeSidebarCover"/> but returns UV floats
    /// for the D3D11 vertex buffer instead of a GDI+ source rectangle.
    /// </summary>
    private static (float u0, float v0, float u1, float v1) ComputeCoverUV(
        System.Drawing.Size bmpSize, float quadPixelW, float quadPixelH)
    {
        float scale = Math.Max(quadPixelW / bmpSize.Width, quadPixelH / bmpSize.Height);
        float srcW  = quadPixelW / scale;
        float srcH  = quadPixelH / scale;
        float srcX  = (bmpSize.Width  - srcW) / 2f;
        float srcY  = (bmpSize.Height - srcH) / 2f;
        return (srcX / bmpSize.Width,  srcY / bmpSize.Height,
                (srcX + srcW) / bmpSize.Width, (srcY + srcH) / bmpSize.Height);
    }

    // ---- Shader loading ----------------------------------------------------------------

    private ID3D11PixelShader ResolvePixelShader(Filters.ID3D11Filter filter)
    {
        string? resourceName = filter.PixelShaderResourceName;
        if (resourceName is null)
            return _passthroughPixelShader;

        if (_shaderCache.TryGetValue(resourceName, out var cached))
            return cached;

        byte[] bytecode = LoadShaderResource(resourceName);
        var shader = _device.CreatePixelShader(bytecode);
        _shaderCache[resourceName] = shader;
        return shader;
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
