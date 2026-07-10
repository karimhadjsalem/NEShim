using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using SDL3;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace NEShim.Rendering;

/// <summary>
/// Renders NES frames directly to the D3D11 swap chain using a fullscreen passthrough quad.
/// Handles letterbox aspect ratio, sidebar images, and GDI+-sourced overlay textures
/// (FPS counter, toasts, and achievement notifications).
///
/// Owned objects: NES texture, SRV, RTV, vertex buffer, shaders, input layout, sampler,
/// blend state, overlay texture + bitmap, sidebar textures.
/// NOT owned: device and swap chain — those belong to <see cref="SteamOverlayRenderer"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class D3D11Renderer : IFrameRenderer
{
    // Not owned — created and disposed by SteamOverlayRenderer.
    private readonly ID3D11Device        _device;
    private readonly IDXGISwapChain      _swapChain;
    private readonly ID3D11DeviceContext _context;

    // Owned D3D11 resources — all disposed in Dispose().
    private ID3D11Texture2D          _nesTexture     = null!;
    private ID3D11ShaderResourceView _nesTextureView = null!;
    private readonly ID3D11Buffer             _vertexBuffer;
    private readonly ID3D11VertexShader       _vertexShader;
    private readonly ID3D11PixelShader        _passthroughPixelShader;
    private readonly ID3D11InputLayout        _inputLayout;
    private readonly ID3D11SamplerState       _pointSamplerState;
    private readonly ID3D11SamplerState       _linearSamplerState;
    private readonly ID3D11BlendState         _alphaBlendState;
    private readonly ID3D11RasterizerState    _scissorRasterizerState;
    private readonly ID3D11Buffer             _filterCbuffer;

    private bool _isDisposed;

    // Shader cache — keyed by resource name; passthrough reuses _passthroughPixelShader.
    private readonly Dictionary<string, ID3D11PixelShader> _shaderCache = new();

    // Active pixel shader — points at passthrough or a cached structural shader.
    private ID3D11PixelShader _activePixelShader;

    // Recreated on Resize.
    private ID3D11RenderTargetView _renderTargetView;

    // Viewport-sized overlay texture + SDL software renderer surface — recreated on Resize.
    private ID3D11Texture2D?          _overlayTexture;
    private ID3D11ShaderResourceView? _overlaySrv;
    private IntPtr                    _overlaySurface;
    private IntPtr                    _overlayRenderer;
    private SDL3FontCache?            _fontCache;
    private SDL3PaintContext?         _paintContext;
    private volatile bool             _overlayDirty = true;

    // Sidebar textures — recreated when SetSidebars is called.
    private ID3D11Texture2D?          _leftSidebarTex;
    private ID3D11ShaderResourceView? _leftSidebarSrv;
    private (int W, int H)            _leftSidebarBmpSize;
    private ID3D11Texture2D?          _rightSidebarTex;
    private ID3D11ShaderResourceView? _rightSidebarSrv;
    private (int W, int H)            _rightSidebarBmpSize;
    private bool                      _hasSidebars;

    private int _nesTextureWidth;
    private readonly int _nesHeight;
    private int _viewportWidth;
    private int _viewportHeight;

    private Filters.ID3D11Filter        _activeFilter       = new Filters.PixelPerfectD3D11Filter();
    private Filters.ID3D11Filter?       _activeOverlay;
    private ID3D11PixelShader?          _activeOverlayPixelShader;
    private OverscanMode                _overscanMode       = OverscanMode.Overscan;
    private VideoColorFilterMode        _activeColorMode    = VideoColorFilterMode.None;
    private MotionEffects.IMotionEffect _activeMotionEffect = new MotionEffects.NoneMotionEffect();
    private ID3D11PixelShader?          _motionEffectPixelShader;
    private int                         _drawFrameCount;

    // Intermediate render targets — each groups a D3D11 texture, RTV, SRV, and pixel
    // dimensions into a single object so the three sub-resources always move together.
    private readonly IntermediateRenderTarget _overlayRt        = new();
    private readonly IntermediateRenderTarget _motionEffectRt   = new();
    private readonly IntermediateRenderTarget _temporalRtA      = new();
    private readonly IntermediateRenderTarget _temporalRtB      = new();
    private          bool                     _temporalUseA     = true;
    private readonly IntermediateRenderTarget _pictureAdjustRt  = new();
    private          ID3D11PixelShader?       _pictureAdjustPixelShader;
    private          float                    _brightness        = 0f;
    private          float                    _contrast          = 1f;
    private          float                    _saturation        = 1f;
    private          float                    _hue               = 0f;

    // Pixel dimensions of the letterbox rect — updated in UpdateLetterboxRect.
    private int _letterboxPixelW;
    private int _letterboxPixelH;

    // UnderscanScale is defined in LetterboxGeometry.

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

    internal D3D11Renderer(nint devicePtr, nint swapChainPtr, int nesWidth, int nesHeight)
    {
        _device        = new ID3D11Device(devicePtr);
        _swapChain     = new IDXGISwapChain(swapChainPtr);
        _context       = _device.ImmediateContext;
        _nesHeight     = nesHeight;
        _contentWidth  = nesWidth;
        _contentHeight = nesHeight;
        _displayHeight = nesHeight;

        // BizHawk IVideoProvider returns int[] where each int is 0xAARRGGBB.
        // In little-endian memory the bytes are [B, G, R, A] — BGRA — which maps
        // directly to B8G8R8A8_UNorm with no byte swapping required.
        CreateNesTexture(nesWidth);

        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device.CreateRenderTargetView(backBuffer);

        var swapDesc = _swapChain.Description;
        _viewportWidth  = (int)swapDesc.BufferDescription.Width;
        _viewportHeight = (int)swapDesc.BufferDescription.Height;

        // Dynamic vertex buffer — updated each draw call to support the NES quad,
        // sidebar quads, and overlay quad from a single buffer object.
        unsafe
        {
            float[] placeholder = new float[6 * 4]; // 6 vertices × (pos_xy + uv)
            fixed (float* p = placeholder)
            {
                _device.CreateBuffer(
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

        byte[] vsBytes = LoadShaderResource("NEShim.Rendering.Shaders.Dx11.Passthrough.vs.cso");
        byte[] psBytes = LoadShaderResource("NEShim.Rendering.Shaders.Dx11.Passthrough.ps.cso");

        // DXVK on Proton compiles these DXBC bytecodes to SPIR-V at first launch
        // and caches them in ~/.local/share/Steam/steamapps/shadercache/<appid>/.
        // The passthrough shaders are trivially simple, so first-launch compile is near-instant.
        _vertexShader              = _device.CreateVertexShader(vsBytes);
        _passthroughPixelShader    = _device.CreatePixelShader(psBytes);
        _activePixelShader         = _passthroughPixelShader;
        _pictureAdjustPixelShader  = ResolvePixelShader("NEShim.Rendering.Shaders.Dx11.PictureAdjust.ps.cso");

        // 16-byte cbuffer (4 floats): structural params [0..2] + colorMode [3].
        // Always present — every pixel shader reads from b0.
        unsafe
        {
            float[] zeros = new float[4];
            fixed (float* p = zeros)
            {
                _device.CreateBuffer(
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

        _inputLayout = _device.CreateInputLayout(
            new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0, InputClassification.PerVertexData, 0),
            },
            vsBytes);

        // Point-clamp sampler — preserves hard NES pixel edges.
        // Clamping prevents edge wrap artefacts on the NES texture boundary.
        _pointSamplerState = _device.CreateSamplerState(new SamplerDescription
        {
            Filter         = Filter.MinMagMipPoint,
            AddressU       = TextureAddressMode.Clamp,
            AddressV       = TextureAddressMode.Clamp,
            AddressW       = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunction.Never,
            MaxAnisotropy  = 1,
            MaxLOD         = float.MaxValue,
        });

        // Linear-clamp sampler — bilinear interpolation for smooth scaling.
        _linearSamplerState = _device.CreateSamplerState(new SamplerDescription
        {
            Filter         = Filter.MinMagMipLinear,
            AddressU       = TextureAddressMode.Clamp,
            AddressV       = TextureAddressMode.Clamp,
            AddressW       = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunction.Never,
            MaxAnisotropy  = 1,
            MaxLOD         = float.MaxValue,
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
        _alphaBlendState = _device.CreateBlendState(blendDesc);

        _scissorRasterizerState = _device.CreateRasterizerState(new RasterizerDescription
        {
            FillMode        = Vortice.Direct3D11.FillMode.Solid,
            CullMode        = CullMode.None,
            ScissorEnable   = true,
            DepthClipEnable = true,
        });

        CreateOverlayResources();
        UpdateOverscanUV();
        UpdateLetterboxRect();

        Logger.Log($"[D3D11Renderer] Initialized ({_nesTextureWidth}×{_nesHeight}, B8G8R8A8_UNorm, point-clamp). Renderer mode: D3D11.");
    }

    // ---- IFrameRenderer ----------------------------------------------------------------

    /// <summary>
    /// Uploads one NES frame's pixel data to the GPU texture.
    /// Called on the UI thread via BeginInvoke from the emulation thread.
    /// </summary>
    public unsafe void UploadFrame(ReadOnlySpan<int> nesPixels, int contentWidth, int contentHeight)
    {
        if (_isDisposed) return;
        if (contentWidth != _contentWidth || contentHeight != _contentHeight)
        {
            _contentWidth  = contentWidth;
            _contentHeight = contentHeight;
            UpdateOverscanUV();
            UpdateLetterboxRect();
        }

        RecreateNesTextureIfNeeded(contentWidth);

        var mapped = _context.Map(_nesTexture, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var srcBytes = MemoryMarshal.AsBytes(nesPixels);
            int srcStride = contentWidth * 4;

            // MappedSubresource.RowPitch is NOT guaranteed to equal contentWidth * 4.
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
    public void Tick(bool vsync)
    {
        if (_isDisposed) return;
        DrawAndPresent(vsync);
    }

    public void UpdateFpsOverlay(bool show, float fps)
    {
        _showFps      = show;
        _currentFps   = fps;
        _overlayDirty = true;
    }

    public void SetSidebars(IntPtr left, IntPtr right)
    {
        UploadSidebarSurface(left,  ref _leftSidebarTex,  ref _leftSidebarSrv,  ref _leftSidebarBmpSize);
        UploadSidebarSurface(right, ref _rightSidebarTex, ref _rightSidebarSrv, ref _rightSidebarBmpSize);
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

    public void SetMotionEffect(VideoMotionEffectMode mode)
    {
        _activeMotionEffect      = MotionEffects.MotionEffectFactory.Create(mode);
        _motionEffectPixelShader = _activeMotionEffect.PixelShaderResourceName is { } r
            ? ResolvePixelShader(r)
            : null;
        _activeMotionEffect.NotifyLayout(_viewportWidth, _viewportHeight, _letterboxPixelH);
        SyncMotionEffectRt();
    }

    /// <summary>
    /// Updates the brightness, contrast, saturation, and hue post-process pass.
    /// Values are on a -100..100 scale; 0 = neutral.
    /// </summary>
    public void SetPictureAdjust(int brightness, int contrast, int saturation, int hue)
    {
        _brightness = brightness * 0.002f;
        _contrast   = 1f + contrast   * 0.01f;
        _saturation = 1f + saturation * 0.01f;
        _hue        = hue * (float)Math.PI / 100f; // -100..100 → -π..+π rad
        SyncPictureAdjustRt();
    }

    public void SetOverlayFilter(Filters.ID3D11Filter? overlay)
    {
        _activeOverlay            = overlay;
        _activeOverlayPixelShader = overlay is not null ? ResolvePixelShader(overlay) : null;
        SyncOverlayRt();
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

    // Routes NES content draws to the picture adjust RT when B/C/S are non-default.
    private ID3D11RenderTargetView CurrentFinalTarget =>
        _pictureAdjustRt.IsReady ? _pictureAdjustRt.Rtv! : _renderTargetView;

    private bool IsPictureAdjustActive =>
        _brightness != 0f || _contrast != 1f || _saturation != 1f || _hue != 0f;

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
        UpdateLetterboxRect();  // -> SyncMotionEffectRt (which calls SyncTemporalRts) at new letterbox dimensions
        SyncOverlayRt();        // overlay RT also needs to reflect new letterbox dimensions
        SyncPictureAdjustRt();  // picture adjust RT matches viewport size
    }

    public void Dispose()
    {
        _isDisposed = true;
        Logger.Log("[D3D11Renderer] Disposed.");
        DisposeOverlayResources();
        _overlayRt.Dispose();
        _motionEffectRt.Dispose();
        _temporalRtA.Dispose();
        _temporalRtB.Dispose();
        _pictureAdjustRt.Dispose();
        DisposeSidebarResources();
        _alphaBlendState.Dispose();
        _scissorRasterizerState.Dispose();
        _pointSamplerState.Dispose();
        _linearSamplerState.Dispose();
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
        _activeFilter.NotifyFrame(_drawFrameCount);
        _activeOverlay?.NotifyFrame(_drawFrameCount);
        _drawFrameCount++;

        if (_activeOverlay is not null && _overlayRt.IsReady)
            DrawTwoPass();
        else
            DrawSinglePass();

        DrawPictureAdjust();
        DrawOverlay();
        PresentAndCheckResult(vsync);
    }

    private void DrawSinglePass()
    {
        if (_motionEffectRt.IsReady)
        {
            // Sub-pass A: structural filter → motion effect intermediate, colorMode deferred.
            DrawStructuralFilterToTarget(_motionEffectRt);

            // Sub-pass B: motion effect (or temporal blend) → final target.
            if (_temporalRtA.IsReady)
                DrawTemporalMotionEffect();
            else
                DrawMotionEffectToBackbuffer();
        }
        else
        {
            var finalTarget = CurrentFinalTarget;
            _context.OMSetRenderTargets(finalTarget);
            _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
            _context.ClearRenderTargetView(finalTarget, new Color4(0f, 0f, 0f, 1f));
            SetupPipelineState();
            if (_hasSidebars) DrawSidebars();
            UpdateFilterCbuffer();
            DrawNesQuad(_nesTextureView, _activeMotionEffect.GetFrameOffset(_drawFrameCount),
                        0f, _nesV0, 1f, _nesV1);
        }
        _context.RSSetState(null);
    }

    private void DrawTwoPass()
    {
        // Pass 1: primary filter → overlay intermediate, colorMode deferred.
        DrawStructuralFilterToTarget(_overlayRt);

        if (_motionEffectRt.IsReady)
        {
            // Pass 2: overlay filter → motion effect intermediate, colorMode deferred.
            _context.OMSetRenderTargets(_motionEffectRt.Rtv!);
            _context.RSSetViewport(0, 0, _motionEffectRt.Width, _motionEffectRt.Height);
            _context.ClearRenderTargetView(_motionEffectRt.Rtv!, new Color4(0f, 0f, 0f, 1f));
            UpdateOverlayCbuffer(colorModeOverride: 0f);
            _context.PSSetShader(_activeOverlayPixelShader!);
            _context.PSSetSampler(0, _activeOverlay!.UseLinearSampler ? _linearSamplerState : _pointSamplerState);
            _context.PSSetShaderResource(0, _overlayRt.Srv!);
            WriteQuadToVB(-1f, 1f, 1f, -1f, 0f, 0f, 1f, 1f);
            _context.Draw(6, 0);

            // Pass 3: motion effect (or temporal blend) → final target.
            if (_temporalRtA.IsReady)
                DrawTemporalMotionEffect();
            else
                DrawMotionEffectToBackbuffer();
        }
        else
        {
            // Pass 2: overlay filter → final target, reading from overlay intermediate.
            var finalTarget = CurrentFinalTarget;
            _context.OMSetRenderTargets(finalTarget);
            _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
            _context.ClearRenderTargetView(finalTarget, new Color4(0f, 0f, 0f, 1f));
            if (_hasSidebars) DrawSidebars();
            UpdateOverlayCbuffer();
            _context.PSSetShader(_activeOverlayPixelShader!);
            _context.PSSetSampler(0, _activeOverlay!.UseLinearSampler ? _linearSamplerState : _pointSamplerState);
            DrawNesQuad(_overlayRt.Srv!, _activeMotionEffect.GetFrameOffset(_drawFrameCount),
                        0f, 0f, 1f, 1f);
        }

        _context.RSSetState(null);
        _context.PSSetShaderResource(0, _nesTextureView);
    }

    // Renders the NES texture through the structural filter (colorMode deferred to 0)
    // into the given intermediate render target. Both DrawSinglePass and DrawTwoPass
    // use this as their first sub-pass.
    private void DrawStructuralFilterToTarget(IntermediateRenderTarget target)
    {
        _context.OMSetRenderTargets(target.Rtv!);
        _context.RSSetViewport(0, 0, target.Width, target.Height);
        _context.ClearRenderTargetView(target.Rtv!, new Color4(0f, 0f, 0f, 1f));
        SetupPipelineState();
        UpdateFilterCbuffer(colorModeOverride: 0f);
        _context.PSSetShaderResource(0, _nesTextureView);
        WriteQuadToVB(-1f, 1f, 1f, -1f, 0f, _nesV0, 1f, _nesV1);
        _context.Draw(6, 0);
    }

    // Renders from the motion effect intermediate RT through the ME pixel shader to the
    // final target. Sidebars are drawn first so they appear behind the NES viewport.
    private void DrawMotionEffectToBackbuffer()
    {
        var finalTarget = CurrentFinalTarget;
        _context.OMSetRenderTargets(finalTarget);
        _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
        _context.ClearRenderTargetView(finalTarget, new Color4(0f, 0f, 0f, 1f));
        if (_hasSidebars) DrawSidebars();
        _context.PSSetShader(_motionEffectPixelShader);
        _context.PSSetSampler(0, _linearSamplerState);
        var jitter = _activeMotionEffect.GetFrameOffset(_drawFrameCount);
        WriteMotionEffectCbuffer();
        DrawNesQuad(_motionEffectRt.Srv!, jitter, 0f, 0f, 1f, 1f);
    }

    // Temporal phosphor blend: reads current frame (t0) + history (t1) → write RT,
    // then blits the result to the final target with sidebars. Flips the ping-pong each call.
    private void DrawTemporalMotionEffect()
    {
        var historyRt = _temporalUseA ? _temporalRtA : _temporalRtB;
        var writeRt   = _temporalUseA ? _temporalRtB : _temporalRtA;

        // Blend pass: current (_motionEffectRt = t0) + history (t1) → writeRt
        _context.OMSetRenderTargets(writeRt.Rtv!);
        _context.RSSetViewport(0, 0, writeRt.Width, writeRt.Height);
        _context.ClearRenderTargetView(writeRt.Rtv!, new Color4(0f, 0f, 0f, 0f));
        _context.PSSetShader(_motionEffectPixelShader);
        _context.PSSetSampler(0, _activeMotionEffect.UseLinearSampler ? _linearSamplerState : _pointSamplerState);
        _context.PSSetShaderResource(0, _motionEffectRt.Srv!);
        _context.PSSetShaderResource(1, historyRt.Srv!);
        WriteMotionEffectCbuffer();
        WriteQuadToVB(-1f, 1f, 1f, -1f, 0f, 0f, 1f, 1f);
        _context.Draw(6, 0);
        _context.PSSetShaderResource(1, null!); // unbind history SRV slot at D3D11 interop boundary

        // Blit to final target (colorMode=0; already applied in blend pass)
        var finalTarget = CurrentFinalTarget;
        _context.OMSetRenderTargets(finalTarget);
        _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
        _context.ClearRenderTargetView(finalTarget, new Color4(0f, 0f, 0f, 1f));
        if (_hasSidebars) DrawSidebars();
        _context.PSSetShader(_passthroughPixelShader);
        _context.PSSetSampler(0, _linearSamplerState);
        UpdateFilterCbuffer(colorModeOverride: 0f);
        DrawNesQuad(writeRt.Srv!, _activeMotionEffect.GetFrameOffset(_drawFrameCount), 0f, 0f, 1f, 1f);
        _context.PSSetShader(_activePixelShader);

        _temporalUseA = !_temporalUseA;
    }

    private void DrawNesQuad(ID3D11ShaderResourceView srv, (float dx, float dy) jitter,
                             float u0, float v0, float u1, float v1)
    {
        int scissorL = (int)((_nesX0 + 1f) * 0.5f * _viewportWidth);
        int scissorR = (int)((_nesX1 + 1f) * 0.5f * _viewportWidth);
        int scissorT = (int)((1f - _nesY0) * 0.5f * _viewportHeight);
        int scissorB = (int)((1f - _nesY1) * 0.5f * _viewportHeight);
        SetScissorRect(scissorL, scissorT, scissorR, scissorB);
        _context.RSSetState(_scissorRasterizerState);
        _context.PSSetShaderResource(0, srv);
        WriteQuadToVB(_nesX0 + jitter.dx, _nesY0 + jitter.dy,
                      _nesX1 + jitter.dx, _nesY1 + jitter.dy, u0, v0, u1, v1);
        _context.Draw(6, 0);
    }

    private void PresentAndCheckResult(bool vsync)
    {
        var result = _swapChain.Present(vsync ? 1u : 0u, PresentFlags.None);
        if (result.Code == unchecked((int)0x887A0005) ||
            result.Code == unchecked((int)0x887A0007))
        {
            Logger.Log($"[D3D11Renderer] Device lost (HRESULT 0x{result.Code:X8}). Firing DeviceLost event.");
            DeviceLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetScissorRect(int left, int top, int right, int bottom)
    {
        _context.RSSetScissorRects(1u, [new Vortice.RawRect(left, top, right, bottom)]);
    }

    private void SetupPipelineState()
    {
        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetVertexBuffer(0, _vertexBuffer, VertexStride);
        _context.IASetInputLayout(_inputLayout);
        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_activePixelShader);
        _context.PSSetSampler(0, _activeFilter.UseLinearSampler ? _linearSamplerState : _pointSamplerState);
    }

    private unsafe void UpdateFilterCbuffer(float? colorModeOverride = null)
    {
        Span<float> p = stackalloc float[4];
        _activeFilter.WriteBaseParams(p, _contentWidth, _nesHeight);
        p[3] = colorModeOverride ?? (float)_activeColorMode;
        WriteCbufferParams(p);
    }

    private unsafe void UpdateOverlayCbuffer(float? colorModeOverride = null)
    {
        Span<float> p = stackalloc float[4];
        _activeOverlay!.WriteBaseParams(p, _contentWidth, _nesHeight);
        p[3] = colorModeOverride ?? (float)_activeColorMode;
        WriteCbufferParams(p);
    }

    private unsafe void WriteMotionEffectCbuffer()
    {
        Span<float> p = stackalloc float[4];
        _activeMotionEffect.WriteShaderParams(p, _contentWidth, _contentHeight);
        p[3] = (float)_activeColorMode;
        WriteCbufferParams(p);
    }

    private unsafe void WriteCbufferParams(Span<float> p)
    {
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
        // Sidebar artwork: no structural filter, no color grade.
        // Callers must re-update the cbuffer after this returns.
        _context.PSSetShader(_passthroughPixelShader);
        UpdateFilterCbuffer(colorModeOverride: 0f);

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

        _context.PSSetShader(_activePixelShader);
    }

    private unsafe void UploadSidebarSurface(
        IntPtr                        surface,
        ref ID3D11Texture2D?          tex,
        ref ID3D11ShaderResourceView? srv,
        ref (int W, int H)            surfaceSize)
    {
        tex?.Dispose(); tex = null;
        srv?.Dispose(); srv = null;
        surfaceSize = default;

        if (surface == IntPtr.Zero) return;

        // Image.Load returns the PNG in whatever format the decoder chooses (often RGBA8888
        // on Windows). Convert to ARGB8888 so the pixel bytes land as [B,G,R,A] in memory,
        // matching B8G8R8A8_UNorm that D3D11 expects. Without this, red and blue are swapped.
        IntPtr converted = SDL.ConvertSurface(surface, SDL.PixelFormat.ARGB8888);
        if (converted == IntPtr.Zero) return;

        try
        {
            SDL.Surface* surf = (SDL.Surface*)converted;
            int surfW = surf->Width;
            int surfH = surf->Height;
            if (surfW <= 0 || surfH <= 0 || surf->Pixels == IntPtr.Zero) return;

            surfaceSize = (surfW, surfH);
            var initData = new SubresourceData(surf->Pixels, (uint)surf->Pitch, 0);
            tex = _device.CreateTexture2D(
                new Texture2DDescription
                {
                    Width             = (uint)surfW,
                    Height            = (uint)surfH,
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
            SDL.DestroySurface(converted);
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
        if (_paintContext is null || _overlayTexture is null) return;

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
        var clientRect = new SDL.Rect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        _paintContext!.Clear(new SDL.Color { R = 0, G = 0, B = 0, A = 0 });

        // Draw active menu/logo scene first; transient overlays composite on top.
        _menuSceneProvider?.GetActiveScenePainter()?.Invoke(_paintContext, clientRect);

        if (_showFps)
            OverlayRenderer.DrawFps(_paintContext, clientRect, _currentFps);

        if (_toastText is not null)
            OverlayRenderer.DrawToast(_paintContext, clientRect, _toastText);

        SDL.RenderPresent(_overlayRenderer);
    }

    private unsafe void UploadOverlayBitmap()
    {
        SDL.Surface* overlaySurf = (SDL.Surface*)_overlaySurface;
        if (overlaySurf->Pixels == IntPtr.Zero) return;

        var mapped = _context.Map(_overlayTexture!, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            int srcStride = overlaySurf->Pitch;
            byte* dst = (byte*)mapped.DataPointer;
            byte* src = (byte*)overlaySurf->Pixels;
            for (int row = 0; row < _viewportHeight; row++)
                Buffer.MemoryCopy(src + row * srcStride, dst + row * mapped.RowPitch, srcStride, srcStride);
        }
        finally { _context.Unmap(_overlayTexture!, 0); }
    }

    private void CreateOverlayResources()
    {
        _fontCache       = new SDL3FontCache();
        _overlaySurface  = SDL.CreateSurface(_viewportWidth, _viewportHeight, SDL.PixelFormat.ARGB8888);
        _overlayRenderer = SDL.CreateSoftwareRenderer(_overlaySurface);
        _paintContext    = new SDL3PaintContext(_overlayRenderer, _fontCache, _viewportWidth, _viewportHeight);

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
        _paintContext = null;
        if (_overlayRenderer != IntPtr.Zero) { SDL.DestroyRenderer(_overlayRenderer); _overlayRenderer = IntPtr.Zero; }
        if (_overlaySurface  != IntPtr.Zero) { SDL.DestroySurface(_overlaySurface);  _overlaySurface  = IntPtr.Zero; }
        _fontCache?.Dispose(); _fontCache = null;
    }

    // ---- Intermediate render target management -----------------------------------------

    private void SyncOverlayRt()
    {
        bool changed = _overlayRt.Sync(_device,
            _activeOverlay is not null ? _letterboxPixelW : 0,
            _activeOverlay is not null ? _letterboxPixelH : 0);
        if (changed && _overlayRt.IsReady)
            Logger.Log($"[D3D11Renderer] Intermediate RT created ({_overlayRt.Width}×{_overlayRt.Height}) for two-pass overlay.");
    }

    private void SyncMotionEffectRt()
    {
        bool changed = _motionEffectRt.Sync(_device,
            _motionEffectPixelShader is not null ? _letterboxPixelW : 0,
            _motionEffectPixelShader is not null ? _letterboxPixelH : 0);
        if (changed && _motionEffectRt.IsReady)
            Logger.Log($"[D3D11Renderer] Motion effect intermediate RT created ({_motionEffectRt.Width}×{_motionEffectRt.Height}).");
        SyncTemporalRts();
    }

    private void SyncTemporalRts()
    {
        bool needsTemporal = _activeMotionEffect.NeedsTemporalBuffer && _motionEffectPixelShader is not null;
        int w = needsTemporal ? _letterboxPixelW : 0;
        int h = needsTemporal ? _letterboxPixelH : 0;

        bool changedA = _temporalRtA.Sync(_device, w, h);
        bool changedB = _temporalRtB.Sync(_device, w, h);

        if ((changedA || changedB) && _temporalRtA.IsReady)
        {
            _context.ClearRenderTargetView(_temporalRtA.Rtv!, new Color4(0f, 0f, 0f, 0f));
            _context.ClearRenderTargetView(_temporalRtB.Rtv!, new Color4(0f, 0f, 0f, 0f));
            _temporalUseA = true;
            Logger.Log($"[D3D11Renderer] Temporal RTs created ({_temporalRtA.Width}×{_temporalRtA.Height}) for phosphor persistence.");
        }
    }

    private void SyncPictureAdjustRt()
    {
        bool changed = _pictureAdjustRt.Sync(_device,
            IsPictureAdjustActive ? _viewportWidth : 0,
            IsPictureAdjustActive ? _viewportHeight : 0);
        if (changed && _pictureAdjustRt.IsReady)
            Logger.Log($"[D3D11Renderer] Picture adjust RT created ({_pictureAdjustRt.Width}×{_pictureAdjustRt.Height}).");
    }

    // Applies brightness/contrast/saturation/hue to the picture adjust RT and blits to _renderTargetView.
    // No-op when IsPictureAdjustActive is false (_pictureAdjustRt.IsReady is also false in that case).
    private void DrawPictureAdjust()
    {
        if (!_pictureAdjustRt.IsReady) return;

        Span<float> p = stackalloc float[4];
        p[0] = _brightness;
        p[1] = _contrast;
        p[2] = _saturation;
        p[3] = _hue;
        WriteCbufferParams(p);

        _context.OMSetRenderTargets(_renderTargetView);
        _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);
        _context.PSSetShader(_pictureAdjustPixelShader);
        _context.PSSetSampler(0, _linearSamplerState);
        _context.PSSetShaderResource(0, _pictureAdjustRt.Srv!);
        WriteQuadToVB(-1f, 1f, 1f, -1f);
        _context.Draw(6, 0);

        // Restore filter cbuffer so DrawOverlay sees the correct colorMode.
        _context.PSSetShader(_activePixelShader);
        UpdateFilterCbuffer();
    }

    private void DisposeSidebarResources()
    {
        _leftSidebarSrv?.Dispose();  _leftSidebarSrv  = null;
        _leftSidebarTex?.Dispose();  _leftSidebarTex  = null;
        _rightSidebarSrv?.Dispose(); _rightSidebarSrv = null;
        _rightSidebarTex?.Dispose(); _rightSidebarTex = null;
    }

    // ---- NES texture management --------------------------------------------------------

    /// <summary>
    /// Creates the NES texture and SRV at the given width (height is always _nesHeight).
    /// Called from the constructor and from RecreateNesTextureIfNeeded.
    /// </summary>
    private void CreateNesTexture(int width)
    {
        _nesTexture = _device.CreateTexture2D(new Texture2DDescription
        {
            Width             = (uint)width,
            Height            = (uint)_nesHeight,
            MipLevels         = 1,
            ArraySize         = 1,
            Format            = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage             = ResourceUsage.Dynamic,
            BindFlags         = BindFlags.ShaderResource,
            CPUAccessFlags    = CpuAccessFlags.Write,
        });
        _nesTextureView  = _device.CreateShaderResourceView(_nesTexture);
        _nesTextureWidth = width;
    }

    private void RecreateNesTextureIfNeeded(int width)
    {
        if (width == _nesTextureWidth) return;
        _nesTextureView.Dispose();
        _nesTexture.Dispose();
        CreateNesTexture(width);
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
        // Use _activeFilter.PixelAspectRatio so filters that alter perceived pixel width
        // (e.g. NtscComposite at 8/7 PAR) are handled without touching this method.
        var r = LetterboxGeometry.Compute(
            _contentWidth, _displayHeight, _activeFilter.PixelAspectRatio,
            _viewportWidth, _viewportHeight,
            _overscanMode == OverscanMode.Underscan);

        _nesX0 = r.X0;
        _nesX1 = r.X1;
        _nesY0 = r.Y0;
        _nesY1 = r.Y1;
        _letterboxPixelW = r.PixelW;
        _letterboxPixelH = r.PixelH;
        _activeMotionEffect.NotifyLayout(_viewportWidth, _viewportHeight, _letterboxPixelH);
        SyncMotionEffectRt();
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
    /// Returns UV floats for the D3D11 vertex buffer for cover-crop scaling of a sidebar surface.
    /// </summary>
    internal static (float u0, float v0, float u1, float v1) ComputeCoverUV(
        (int W, int H) surfaceSize, float quadPixelW, float quadPixelH)
    {
        float scale = Math.Max(quadPixelW / surfaceSize.W, quadPixelH / surfaceSize.H);
        float srcW  = quadPixelW / scale;
        float srcH  = quadPixelH / scale;
        float srcX  = (surfaceSize.W - srcW) / 2f;
        float srcY  = (surfaceSize.H - srcH) / 2f;
        return (srcX / surfaceSize.W,  srcY / surfaceSize.H,
                (srcX + srcW) / surfaceSize.W, (srcY + srcH) / surfaceSize.H);
    }

    // ---- Shader loading ----------------------------------------------------------------

    private ID3D11PixelShader ResolvePixelShader(Filters.ID3D11Filter filter)
        => ResolvePixelShader(filter.PixelShaderResourceName);

    private ID3D11PixelShader ResolvePixelShader(string? resourceName)
    {
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

    // ---- IntermediateRenderTarget ------------------------------------------------------

    // Groups a D3D11 texture, RTV, and SRV that always move together, along with the
    // pixel dimensions used to configure the viewport when rendering into this target.
    // Sync() keeps the RT in sync with the current letterbox size; it disposes without
    // recreating when called with zero dimensions (overlay or motion effect not active).
    private sealed class IntermediateRenderTarget : IDisposable
    {
        private ID3D11Texture2D?          _texture;
        private ID3D11RenderTargetView?   _rtv;
        private ID3D11ShaderResourceView? _srv;

        public int Width  { get; private set; }
        public int Height { get; private set; }

        public ID3D11RenderTargetView?   Rtv     => _rtv;
        public ID3D11ShaderResourceView? Srv     => _srv;
        public bool                      IsReady => _rtv is not null;

        // Returns true when the RT was newly created or disposed; false when unchanged.
        public bool Sync(ID3D11Device device, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                bool wasReady = IsReady;
                Dispose();
                return wasReady;
            }
            if (_texture is not null && width == Width && height == Height)
                return false;

            Dispose();
            Width  = width;
            Height = height;
            _texture = device.CreateTexture2D(new Texture2DDescription
            {
                Width             = (uint)Width,
                Height            = (uint)Height,
                MipLevels         = 1,
                ArraySize         = 1,
                Format            = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage             = ResourceUsage.Default,
                BindFlags         = BindFlags.RenderTarget | BindFlags.ShaderResource,
            });
            _rtv = device.CreateRenderTargetView(_texture);
            _srv = device.CreateShaderResourceView(_texture);
            return true;
        }

        public void Dispose()
        {
            _srv?.Dispose();     _srv     = null;
            _rtv?.Dispose();     _rtv     = null;
            _texture?.Dispose(); _texture = null;
            Width  = 0;
            Height = 0;
        }
    }
}

