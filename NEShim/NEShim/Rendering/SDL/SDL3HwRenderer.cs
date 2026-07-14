using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using SDL3;
using NEShim.Platform;
using NEShim.Rendering.Filters;
using NEShim.Rendering.MotionEffects;

namespace NEShim.Rendering;

/// <summary>
/// Hardware-accelerated renderer backed by SDL_CreateGPURenderer (preferred) or SDL_CreateRenderer (fallback).
/// Selects Vulkan on Linux, D3D11 on Windows (when used as the SDL_GPU fallback path).
/// Used as the primary renderer on Linux and as the Windows fallback when D3D11 direct-init is unavailable.
///
/// When the GPU renderer is active (<see cref="_isGpuRenderer"/> true):
/// - Structural filters are applied via SPIR-V shaders and SDL_GPURenderState (SdlFilterFactory → ISdlFilter).
/// - Color effects share ColorGrade.hlsli with the D3D11 path; the colorMode uniform is written identically.
/// - CRT Jitter, Scanline Bob, and Magnetic Distortion motion effects are fully supported (SdlMotionEffectFactory).
/// - Video Overlay (second-pass filter slot: CrtScanlines/CrtPhosphor/CrtScreen) is supported as an
///   additional sequential pass, mirroring the D3D11 overlay slot.
/// - PhosphorPersistence (Screen Glow) is supported without a shader: SDL_GPURenderState only binds one
///   texture sampler per draw (no public API exists to bind a second, unlike D3D11's PSSetShaderResource
///   slot 1), so the D3D11 shader's <c>max(current, previous * decay)</c> formula is instead reproduced
///   with two single-sampler draws into a ping-pong accumulation texture using a custom
///   <see cref="SDL.ComposeCustomBlendMode"/> (Maximum op) plus <see cref="SDL.SetTextureColorModFloat"/>
///   for the decay scale — GPU fixed-function blending, no CPU pixel work.
/// - Picture adjust (brightness/contrast/saturation/hue) is implemented as a two-pass render-to-texture.
/// - All four post-filter stages (Overlay, Motion Effect shader / Phosphor accumulation, Picture Adjust)
///   are optional and composable; see DrawNesFrame for the cascading-target pipeline that chains
///   whichever are active in a fixed order, deferring colour-grade application to whichever stage is
///   the last colour-aware one in the chain.
/// When the GPU renderer is unavailable, SDL_CreateRenderer is used with no shader support.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SDL3HwRenderer : IFrameRenderer
{
    private readonly IntPtr _sdlRenderer;
    private readonly IntPtr _gpuDevice;
    private readonly bool   _isGpuRenderer;
    private readonly int    _nesWidth;
    private readonly int    _nesHeight;
    private          IntPtr _nesTexture;

    private IntPtr            _overlayTexture;
    private IntPtr            _overlaySurface;
    private IntPtr            _softwareRenderer;
    private SDL3FontCache?    _fontCache;
    private SDL3PaintContext? _paintContext;
    private volatile bool     _overlayDirty = true;

    private IntPtr         _leftSidebarTex;
    private (int W, int H) _leftSidebarSize;
    private IntPtr         _rightSidebarTex;
    private (int W, int H) _rightSidebarSize;
    private bool           _hasSidebars;

    private int          _contentWidth;
    private int          _contentHeight;
    private int          _viewportWidth;
    private int          _viewportHeight;
    private OverscanMode _overscanMode = OverscanMode.Overscan;

    private const int   OverscanCropRows = 8;
    private const float StandardPAR      = 8f / 7f;
    private const int   UniformFloats    = 4;

    private IMenuSceneProvider? _menuSceneProvider;

    private volatile bool  _showFps;
    private volatile float _currentFps;
    private string?        _toastText;
    private DateTime       _toastExpiry;

    private bool _vsync;

    // Active filter state
    private ISdlFilter     _activeFilter      = new PixelPerfectSdlFilter();
    private SdlGpuRenderState? _filterRenderState;
    private VideoColorFilterMode _activeColorMode = VideoColorFilterMode.None;
    private long  _frameCount;

    // Active overlay filter state (two-pass overlay: CrtScanlines/CrtPhosphor/CrtScreen
    // composited as a second pass on top of the primary structural filter).
    private Filters.ISdlFilter? _activeOverlayFilter;
    private SdlGpuRenderState?  _overlayFilterRenderState;
    private IntPtr              _overlayFilterTexture;
    private bool                _hasOverlayFilter;

    // Active motion effect state
    private IMotionEffect      _activeMotionEffect      = new NoneMotionEffect();
    private SdlGpuRenderState? _motionEffectRenderState;
    private IntPtr             _motionEffectTexture;
    private bool               _hasShaderMotionEffect;

    // Phosphor persistence: temporal accumulation via GPU blend compositing (ping-pong pair)
    // instead of a 2-sampler shader — see class doc comment for why.
    private bool   _hasPhosphorPersistence;
    private IntPtr _phosphorTexA;
    private IntPtr _phosphorTexB;
    private bool   _phosphorUseA = true;

    private static readonly SDL.BlendMode MaximumBlendMode = SDL.ComposeCustomBlendMode(
        SDL.BlendFactor.One, SDL.BlendFactor.One, SDL.BlendOperation.Maximum,
        SDL.BlendFactor.One, SDL.BlendFactor.One, SDL.BlendOperation.Maximum);

    // Picture adjust state
    private float _brightness;
    private float _contrast   = 1f;
    private float _saturation = 1f;
    private float _hue;
    private bool  _hasPictureAdjust;
    private IntPtr         _pictureAdjustTexture;
    private SdlGpuRenderState? _pictureAdjustRenderState;

    public bool OwnsFrameSurface => true;

    /// <summary>True when SDL_CreateGPURenderer succeeded (SPIR-V shader support available).</summary>
    internal bool IsGpuRendererActive => _isGpuRenderer;

#pragma warning disable CS0067
    public event EventHandler? DeviceLost;
#pragma warning restore CS0067

    internal SDL3HwRenderer(IntPtr sdlWindow, int nesWidth, int nesHeight)
    {
        if (sdlWindow == IntPtr.Zero)
            throw new ArgumentException("SDL window handle must be non-zero.", nameof(sdlWindow));

        _nesWidth  = nesWidth;
        _nesHeight = nesHeight;

        // Prefer SDL_GPU renderer (enables SPIR-V filter support); fall back to plain SDL renderer.
        IntPtr renderer = TryCreateGpuRenderer(sdlWindow, out IntPtr gpuDevice);
        if (renderer == IntPtr.Zero)
        {
            renderer = SDL.CreateRenderer(sdlWindow, null);
            if (renderer == IntPtr.Zero)
                throw new InvalidOperationException($"SDL_CreateRenderer failed: {SDL.GetError()}");
            Logger.Log("[SDL3HwRenderer] GPU renderer unavailable; using plain SDL renderer (no shader filters).");
        }
        _sdlRenderer   = renderer;
        _gpuDevice     = gpuDevice;
        _isGpuRenderer = gpuDevice != IntPtr.Zero;

        SDL.SetRenderVSync(_sdlRenderer, 1);
        _vsync = true;

        SDL.GetWindowSizeInPixels(sdlWindow, out _viewportWidth, out _viewportHeight);
        _contentWidth  = nesWidth;
        _contentHeight = nesHeight;

        _nesTexture = CreateNesTexture();
        CreateOverlayResources();

        Logger.Log($"[SDL3HwRenderer] Initialised ({nesWidth}×{nesHeight}, ARGB8888). " +
                   $"GPU filters: {(_isGpuRenderer ? "enabled" : "disabled")}.");
    }

    private static IntPtr TryCreateGpuRenderer(IntPtr sdlWindow, out IntPtr gpuDevice)
    {
        gpuDevice = IntPtr.Zero;
        try
        {
            IntPtr renderer = SDL.CreateGPURenderer(sdlWindow, new IntPtr((int)SDL.GPUShaderFormat.SPIRV));
            if (renderer == IntPtr.Zero) return IntPtr.Zero;
            gpuDevice = SDL.GetGPURendererDevice(renderer);
            if (gpuDevice == IntPtr.Zero)
            {
                SDL.DestroyRenderer(renderer);
                return IntPtr.Zero;
            }
            Logger.Log("[SDL3HwRenderer] GPU renderer created (SPIR-V).");
            return renderer;
        }
        catch (Exception ex)
        {
            Logger.Log($"[SDL3HwRenderer] GPU renderer creation threw: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    public void SetMenuSceneProvider(IMenuSceneProvider? provider) => _menuSceneProvider = provider;
    public void MarkOverlayDirty() => _overlayDirty = true;

    // ---- Frame upload ------------------------------------------------------------------

    public unsafe void UploadFrame(ReadOnlySpan<int> pixels, int contentWidth, int contentHeight)
    {
        _contentWidth  = contentWidth;
        _contentHeight = contentHeight;
        if (_nesTexture == IntPtr.Zero) return;

        if (!SDL.LockTexture(_nesTexture, IntPtr.Zero, out IntPtr dstPixels, out int dstPitch))
            return;
        try
        {
            var srcBytes    = MemoryMarshal.AsBytes(pixels);
            int srcRowBytes = contentWidth * 4;
            for (int y = 0; y < contentHeight; y++)
            {
                srcBytes.Slice(y * srcRowBytes, srcRowBytes)
                        .CopyTo(new Span<byte>((byte*)dstPixels + (long)y * dstPitch, srcRowBytes));
            }
        }
        finally
        {
            SDL.UnlockTexture(_nesTexture);
        }
    }

    // ---- Present -----------------------------------------------------------------------

    public void Tick(bool vsync)
    {
        if (vsync != _vsync)
        {
            _vsync = vsync;
            SDL.SetRenderVSync(_sdlRenderer, vsync ? 1 : 0);
        }
        DrawAndPresent();
    }

    private void DrawAndPresent()
    {
        SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 255);
        SDL.RenderClear(_sdlRenderer);

        ComputeLetterbox(out SDL.FRect nesDest, out float sidebarW);

        if (_hasSidebars)
            DrawSidebars(nesDest, sidebarW);

        DrawNesFrame(nesDest);
        DrawOverlay();

        SDL.RenderPresent(_sdlRenderer);
    }

    // ---- Geometry ----------------------------------------------------------------------

    private void ComputeLetterbox(out SDL.FRect nesDest, out float sidebarW)
    {
        int displayHeight = ComputeDisplayHeight();
        bool underscan    = _overscanMode == OverscanMode.Underscan;

        var geo = LetterboxGeometry.Compute(
            _contentWidth, Math.Max(1, displayHeight),
            StandardPAR, _viewportWidth, _viewportHeight, underscan);

        float destX = (_viewportWidth  - geo.PixelW) / 2f;
        float destY = (_viewportHeight - geo.PixelH) / 2f;

        nesDest  = new SDL.FRect { X = destX, Y = destY, W = geo.PixelW, H = geo.PixelH };
        sidebarW = destX;
    }

    private int ComputeDisplayHeight()
    {
        int overscanTop = (_overscanMode == OverscanMode.Overscan && _contentHeight >= OverscanCropRows * 2)
            ? OverscanCropRows : 0;
        return _contentHeight - overscanTop * 2;
    }

    private int ComputeOverscanTop() =>
        (_overscanMode == OverscanMode.Overscan && _contentHeight >= OverscanCropRows * 2)
            ? OverscanCropRows : 0;

    // ---- NES frame draw ----------------------------------------------------------------

    // Pipeline: [structural filter] -> [overlay?] -> [motion-effect shader or phosphor
    // accumulation?] -> [picture adjust?] -> backbuffer. Each optional stage renders into
    // whichever later stage is active next (cascading target selection below), or straight
    // to the backbuffer if it's the last one running. Colour grading is applied by whichever
    // of {filter, overlay, motion-effect shader} is the last colour-aware stage in the chain
    // — phosphor accumulation and picture adjust are colour-blind post-processes and never
    // apply it themselves.
    private void DrawNesFrame(SDL.FRect dest)
    {
        if (_nesTexture == IntPtr.Zero) return;

        int overscanTop   = ComputeOverscanTop();
        int displayHeight = _contentHeight - overscanTop * 2;
        var src = new SDL.FRect { X = 0, Y = overscanTop, W = _contentWidth, H = displayHeight };

        long frame = _frameCount++;
        _activeFilter.NotifyFrame(frame);
        _activeOverlayFilter?.NotifyFrame(frame);

        // Apply CPU-space offset from the motion effect (converts clip-space dx/dy to pixels).
        var (offsetDx, offsetDy) = _activeMotionEffect.GetFrameOffset(frame);
        if (offsetDx != 0f || offsetDy != 0f)
            dest = new SDL.FRect
            {
                X = dest.X + offsetDx * _viewportWidth  / 2f,
                Y = dest.Y + offsetDy * _viewportHeight / 2f,
                W = dest.W,
                H = dest.H,
            };

        bool hasOverlay  = _hasOverlayFilter
                        && _overlayFilterTexture     != IntPtr.Zero
                        && _overlayFilterRenderState is { IsValid: true };
        bool hasMeShader = _hasShaderMotionEffect
                        && _motionEffectTexture     != IntPtr.Zero
                        && _motionEffectRenderState is { IsValid: true };
        bool hasPhosphor = _hasPhosphorPersistence
                        && _motionEffectTexture != IntPtr.Zero
                        && _phosphorTexA        != IntPtr.Zero
                        && _phosphorTexB        != IntPtr.Zero;
        bool hasPa       = _hasPictureAdjust
                        && _pictureAdjustTexture    != IntPtr.Zero
                        && _pictureAdjustRenderState is not null;

        IntPtr motionTarget  = hasPa ? _pictureAdjustTexture : IntPtr.Zero;
        IntPtr overlayTarget = (hasMeShader || hasPhosphor) ? _motionEffectTexture : motionTarget;
        IntPtr filterTarget  = hasOverlay ? _overlayFilterTexture : overlayTarget;

        bool filterAppliesColorMode  = !hasOverlay && !hasMeShader;
        bool overlayAppliesColorMode = hasOverlay  && !hasMeShader;

        RunFilterPass(src, dest, filterTarget, filterAppliesColorMode);

        if (hasOverlay)
            RunOverlayPass(overlayTarget, overlayAppliesColorMode);

        if (hasMeShader)
            RunMotionEffectShaderPass(motionTarget);
        else if (hasPhosphor)
            RunPhosphorPass(motionTarget);

        if (hasPa)
            ApplyPictureAdjustPass(_pictureAdjustTexture);
    }

    // NES texture -> structural filter -> targetTexture (or straight to the backbuffer when
    // targetTexture is Zero).
    private void RunFilterPass(SDL.FRect src, SDL.FRect dest, IntPtr targetTexture, bool applyColorMode)
    {
        bool toIntermediate = targetTexture != IntPtr.Zero;
        if (toIntermediate)
        {
            SDL.SetRenderTarget(_sdlRenderer, targetTexture);
            SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
            SDL.RenderClear(_sdlRenderer);
        }
        ApplyFilterRenderState(applyColorMode);
        SDL.RenderTexture(_sdlRenderer, _nesTexture, in src, in dest);
        _filterRenderState?.Clear();
        if (toIntermediate)
            SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);
    }

    // Overlay intermediate -> overlay filter shader -> targetTexture (or backbuffer).
    private void RunOverlayPass(IntPtr targetTexture, bool applyColorMode)
    {
        bool toIntermediate = targetTexture != IntPtr.Zero;
        if (toIntermediate)
        {
            SDL.SetRenderTarget(_sdlRenderer, targetTexture);
            SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
            SDL.RenderClear(_sdlRenderer);
        }
        SDL.SetTextureScaleMode(_overlayFilterTexture,
            _activeOverlayFilter!.UseLinearSampler ? SDL.ScaleMode.Linear : SDL.ScaleMode.Nearest);
        Span<float> uniforms = stackalloc float[UniformFloats];
        _activeOverlayFilter.WriteUniformData(uniforms, _contentWidth, _contentHeight);
        uniforms[3] = applyColorMode ? (float)_activeColorMode : 0f;
        _overlayFilterRenderState!.Apply(uniforms);
        var fullRect = new SDL.FRect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        SDL.RenderTexture(_sdlRenderer, _overlayFilterTexture, in fullRect, in fullRect);
        _overlayFilterRenderState.Clear();
        if (toIntermediate)
            SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);
    }

    // Motion-effect intermediate -> ME shader -> targetTexture (or backbuffer). Always the
    // last colour-aware stage when active, so colour mode is never deferred here.
    private void RunMotionEffectShaderPass(IntPtr targetTexture)
    {
        bool toIntermediate = targetTexture != IntPtr.Zero;
        if (toIntermediate)
        {
            SDL.SetRenderTarget(_sdlRenderer, targetTexture);
            SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
            SDL.RenderClear(_sdlRenderer);
        }
        ApplyMotionEffectRenderState();
        var fullRect = new SDL.FRect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        SDL.RenderTexture(_sdlRenderer, _motionEffectTexture, in fullRect, in fullRect);
        _motionEffectRenderState!.Clear();
        if (toIntermediate)
            SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);
    }

    // Temporal phosphor blend: accumulates writeTex = max(current frame, history * decay)
    // using GPU fixed-function blending (no shader — see class doc comment), then composites
    // the result into targetTexture (or the backbuffer). Swaps the ping-pong roles each call.
    // Colour mode was already applied upstream (filter or overlay pass) when this stage is
    // active, since phosphor accumulation has no shader of its own to apply it.
    private void RunPhosphorPass(IntPtr targetTexture)
    {
        IntPtr historyTex = _phosphorUseA ? _phosphorTexA : _phosphorTexB;
        IntPtr writeTex   = _phosphorUseA ? _phosphorTexB : _phosphorTexA;
        var fullRect = new SDL.FRect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };

        SDL.SetRenderTarget(_sdlRenderer, writeTex);
        SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
        SDL.RenderClear(_sdlRenderer);

        SDL.RenderTexture(_sdlRenderer, _motionEffectTexture, in fullRect, in fullRect);

        Span<float> decayParams = stackalloc float[UniformFloats];
        _activeMotionEffect.WriteShaderParams(decayParams, _contentWidth, _contentHeight);
        float decay = decayParams[0];
        SDL.SetTextureColorModFloat(historyTex, decay, decay, decay);
        SDL.SetTextureBlendMode(historyTex, MaximumBlendMode);
        SDL.RenderTexture(_sdlRenderer, historyTex, in fullRect, in fullRect);

        SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);

        // Composite the accumulated frame. writeTex may have last served as the decayed
        // history source under the opposite ping-pong role, so its blend mode and colour
        // mod must be reset explicitly rather than trusted to be at their defaults.
        bool toIntermediate = targetTexture != IntPtr.Zero;
        if (toIntermediate)
        {
            SDL.SetRenderTarget(_sdlRenderer, targetTexture);
            SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
            SDL.RenderClear(_sdlRenderer);
        }
        SDL.SetTextureColorModFloat(writeTex, 1f, 1f, 1f);
        SDL.SetTextureBlendMode(writeTex, SDL.BlendMode.Blend);
        SDL.RenderTexture(_sdlRenderer, writeTex, in fullRect, in fullRect);
        if (toIntermediate)
            SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);

        _phosphorUseA = !_phosphorUseA;
    }

    private void ApplyPictureAdjustPass(IntPtr sourceTexture)
    {
        Span<float> uniforms = stackalloc float[UniformFloats];
        uniforms[0] = _brightness;
        uniforms[1] = _contrast;
        uniforms[2] = _saturation;
        uniforms[3] = _hue;
        _pictureAdjustRenderState!.Apply(uniforms);
        var fullRect = new SDL.FRect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        SDL.RenderTexture(_sdlRenderer, sourceTexture, in fullRect, in fullRect);
        _pictureAdjustRenderState!.Clear();
    }

    private void ApplyMotionEffectRenderState()
    {
        if (_motionEffectRenderState is null || !_motionEffectRenderState.IsValid) return;
        SDL.SetTextureScaleMode(_motionEffectTexture,
            (_activeMotionEffect is ISdlMotionEffect sdlEffect && sdlEffect.UseLinearSampler)
                ? SDL.ScaleMode.Linear : SDL.ScaleMode.Nearest);
        Span<float> uniforms = stackalloc float[UniformFloats];
        _activeMotionEffect.WriteShaderParams(uniforms, _contentWidth, _contentHeight);
        uniforms[3] = (float)_activeColorMode;
        _motionEffectRenderState.Apply(uniforms);
    }

    private void ApplyFilterRenderState(bool applyColorMode)
    {
        if (_filterRenderState is null || !_filterRenderState.IsValid) return;

        SDL.SetTextureScaleMode(_nesTexture, _activeFilter.UseLinearSampler
            ? SDL.ScaleMode.Linear : SDL.ScaleMode.Nearest);

        Span<float> uniforms = stackalloc float[UniformFloats];
        _activeFilter.WriteUniformData(uniforms, _contentWidth, _contentHeight);
        uniforms[3] = applyColorMode ? (float)_activeColorMode : 0f;
        _filterRenderState.Apply(uniforms);
    }

    // ---- Sidebar draw ------------------------------------------------------------------

    private void DrawSidebars(SDL.FRect nesDest, float sidebarW)
    {
        if (sidebarW < 1f) return;

        if (_leftSidebarTex != IntPtr.Zero && _leftSidebarSize != default)
        {
            var dst = new SDL.FRect { X = 0, Y = 0, W = sidebarW, H = _viewportHeight };
            var src = ComputeCoverSrcFRect(_leftSidebarSize, sidebarW, _viewportHeight);
            SDL.RenderTexture(_sdlRenderer, _leftSidebarTex, in src, in dst);
        }

        float rightX = nesDest.X + nesDest.W;
        float rightW = _viewportWidth - rightX;
        if (_rightSidebarTex != IntPtr.Zero && _rightSidebarSize != default && rightW >= 1f)
        {
            var dst = new SDL.FRect { X = rightX, Y = 0, W = rightW, H = _viewportHeight };
            var src = ComputeCoverSrcFRect(_rightSidebarSize, rightW, _viewportHeight);
            SDL.RenderTexture(_sdlRenderer, _rightSidebarTex, in src, in dst);
        }
    }

    private static SDL.FRect ComputeCoverSrcFRect((int W, int H) texSize, float destW, float destH)
    {
        float scale = Math.Max(destW / texSize.W, destH / texSize.H);
        float srcW  = destW / scale;
        float srcH  = destH / scale;
        float srcX  = (texSize.W - srcW) / 2f;
        float srcY  = (texSize.H - srcH) / 2f;
        return new SDL.FRect { X = srcX, Y = srcY, W = srcW, H = srcH };
    }

    // ---- Overlay -----------------------------------------------------------------------

    private void DrawOverlay()
    {
        var now = DateTime.UtcNow;
        if (_toastText is not null && now >= _toastExpiry) { _toastText = null; _overlayDirty = true; }

        bool hasScene     = _menuSceneProvider?.GetActiveScenePainter() is not null;
        bool hasTransient = _showFps || _toastText is not null;
        if (!hasScene && !hasTransient) return;
        if (_paintContext is null || _overlayTexture == IntPtr.Zero) return;

        if (_overlayDirty)
        {
            RenderOverlayBitmap();
            UploadOverlayToTexture();
            _overlayDirty = false;
        }

        var fullDest = new SDL.FRect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        SDL.RenderTexture(_sdlRenderer, _overlayTexture, IntPtr.Zero, in fullDest);
    }

    private void RenderOverlayBitmap()
    {
        MenuScale.UpdateViewport(_viewportWidth, _viewportHeight);
        var clientRect = new SDL.Rect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        _paintContext!.Clear(new SDL.Color { R = 0, G = 0, B = 0, A = 0 });
        _menuSceneProvider?.GetActiveScenePainter()?.Invoke(_paintContext, clientRect);
        if (_showFps) OverlayRenderer.DrawFps(_paintContext, clientRect, _currentFps);
        if (_toastText is not null) OverlayRenderer.DrawToast(_paintContext, clientRect, _toastText);
        SDL.RenderPresent(_softwareRenderer);
    }

    private unsafe void UploadOverlayToTexture()
    {
        if (_overlaySurface == IntPtr.Zero || _overlayTexture == IntPtr.Zero) return;
        SDL.Surface* surf = (SDL.Surface*)_overlaySurface;
        SDL.UpdateTexture(_overlayTexture, IntPtr.Zero, surf->Pixels, surf->Pitch);
    }

    // ---- IFrameRenderer ----------------------------------------------------------------

    public void Resize(int width, int height)
    {
        _viewportWidth  = Math.Max(width,  1);
        _viewportHeight = Math.Max(height, 1);
        CreateOverlayResources();
        SyncOverlayFilterTexture();
        SyncPictureAdjustTexture();
        SyncMotionEffectTexture();
        SyncPhosphorTextures();
        _activeMotionEffect.NotifyLayout(_viewportWidth, _viewportHeight, _viewportHeight);
        Logger.Log($"[SDL3HwRenderer] Resized to {_viewportWidth}×{_viewportHeight}.");
    }

    public void UpdateFpsOverlay(bool show, float fps)
    {
        _showFps      = show;
        _currentFps   = fps;
        _overlayDirty = true;
    }

    public void SetSidebars(IntPtr left, IntPtr right)
    {
        DisposeSidebarTextures();
        (_leftSidebarTex,  _leftSidebarSize)  = CreateSidebarTexture(left);
        (_rightSidebarTex, _rightSidebarSize) = CreateSidebarTexture(right);
        _hasSidebars = _leftSidebarTex != IntPtr.Zero || _rightSidebarTex != IntPtr.Zero;
    }

    public void ShowToast(string text)
    {
        _toastText    = text;
        _toastExpiry  = DateTime.UtcNow.AddSeconds(OverlayRenderer.ToastDurationSeconds);
        _overlayDirty = true;
    }

    public void ShowAchievementNotification(string name) => ShowToast(name);

    public void SetPictureAdjust(int brightness, int contrast, int saturation, int hue)
    {
        _brightness = brightness * 0.002f;
        _contrast   = 1f + contrast   * 0.01f;
        _saturation = 1f + saturation * 0.01f;
        _hue        = hue * (float)Math.PI / 100f;
        _hasPictureAdjust = _brightness != 0f || _contrast != 1f || _saturation != 1f || _hue != 0f;
        SyncPictureAdjustRenderState();
    }

    public void SetOverscanMode(OverscanMode mode) => _overscanMode = mode;

    public void InitializeRenderingOptions(Filters.ID3D11Filter filter, OverscanMode overscan, VideoColorFilterMode colorMode)
    {
        _overscanMode    = overscan;
        _activeColorMode = colorMode;
        ApplyGpuFilter(SdlFilterFactory.Create(filter.FilterMode));
    }

    public void SetFilter(Filters.ID3D11Filter filter) => ApplyGpuFilter(SdlFilterFactory.Create(filter.FilterMode));

    public void SetOverlayFilter(Filters.ID3D11Filter? overlay)
    {
        _overlayFilterRenderState?.Dispose();
        _overlayFilterRenderState = null;
        _activeOverlayFilter      = null;
        _hasOverlayFilter         = false;

        if (overlay is not null && _isGpuRenderer)
        {
            var filter = SdlFilterFactory.Create(overlay.FilterMode);
            _activeOverlayFilter = filter;
            _hasOverlayFilter    = true;
            if (filter.PixelShaderResourceName is { } resourceName)
            {
                _overlayFilterRenderState = new SdlGpuRenderState(
                    _sdlRenderer, _gpuDevice,
                    resourceName,
                    filter.NumFragmentSamplers,
                    filter.NumFragmentUniformBuffers);
            }
        }

        SyncOverlayFilterTexture();
    }

    public void SetColorFilter(VideoColorFilterMode mode) => _activeColorMode = mode;

    public void SetMotionEffect(VideoMotionEffectMode mode)
    {
        _motionEffectRenderState?.Dispose();
        _motionEffectRenderState = null;
        _hasShaderMotionEffect   = false;
        _hasPhosphorPersistence  = false;
        _activeMotionEffect      = SdlMotionEffectFactory.Create(mode);
        _activeMotionEffect.NotifyLayout(_viewportWidth, _viewportHeight, _viewportHeight);

        if (_isGpuRenderer && _activeMotionEffect.NeedsTemporalBuffer)
        {
            // PhosphorPersistence: no shader on this path — handled via blend compositing
            // in RunPhosphorPass. See class doc comment.
            _hasPhosphorPersistence = true;
        }
        else if (_isGpuRenderer
            && _activeMotionEffect is ISdlMotionEffect sdlEffect
            && sdlEffect.SpvResourceName is { } spvName)
        {
            _hasShaderMotionEffect = true;
            _motionEffectRenderState = new SdlGpuRenderState(
                _sdlRenderer, _gpuDevice,
                spvName,
                sdlEffect.NumFragmentSamplers,
                sdlEffect.NumFragmentUniformBuffers);
        }

        SyncMotionEffectTexture();
        SyncPhosphorTextures();
    }

    public void Dispose()
    {
        _filterRenderState?.Dispose();
        _filterRenderState = null;
        _overlayFilterRenderState?.Dispose();
        _overlayFilterRenderState = null;
        if (_overlayFilterTexture != IntPtr.Zero) { SDL.DestroyTexture(_overlayFilterTexture); _overlayFilterTexture = IntPtr.Zero; }
        _motionEffectRenderState?.Dispose();
        _motionEffectRenderState = null;
        if (_motionEffectTexture  != IntPtr.Zero) { SDL.DestroyTexture(_motionEffectTexture);  _motionEffectTexture  = IntPtr.Zero; }
        if (_phosphorTexA != IntPtr.Zero) { SDL.DestroyTexture(_phosphorTexA); _phosphorTexA = IntPtr.Zero; }
        if (_phosphorTexB != IntPtr.Zero) { SDL.DestroyTexture(_phosphorTexB); _phosphorTexB = IntPtr.Zero; }
        _pictureAdjustRenderState?.Dispose();
        _pictureAdjustRenderState = null;
        if (_pictureAdjustTexture != IntPtr.Zero) { SDL.DestroyTexture(_pictureAdjustTexture); _pictureAdjustTexture = IntPtr.Zero; }
        DisposeOverlayResources();
        DisposeSidebarTextures();
        if (_nesTexture   != IntPtr.Zero) { SDL.DestroyTexture(_nesTexture); _nesTexture = IntPtr.Zero; }
        SDL.DestroyRenderer(_sdlRenderer);
    }

    // ---- GPU filter helpers ------------------------------------------------------------

    private void ApplyGpuFilter(ISdlFilter filter)
    {
        _filterRenderState?.Dispose();
        _filterRenderState = null;
        _activeFilter = filter;
        if (!_isGpuRenderer || filter.PixelShaderResourceName is null) return;
        _filterRenderState = new SdlGpuRenderState(
            _sdlRenderer, _gpuDevice,
            filter.PixelShaderResourceName,
            filter.NumFragmentSamplers,
            filter.NumFragmentUniformBuffers);
    }

    private void SyncPictureAdjustTexture()
    {
        if (_pictureAdjustTexture != IntPtr.Zero) { SDL.DestroyTexture(_pictureAdjustTexture); _pictureAdjustTexture = IntPtr.Zero; }
        if (!_isGpuRenderer || !_hasPictureAdjust) return;
        _pictureAdjustTexture = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Target, _viewportWidth, _viewportHeight);
        if (_pictureAdjustTexture == IntPtr.Zero)
        {
            Logger.Log($"[SDL3HwRenderer] Failed to create picture-adjust texture: {SDL.GetError()}");
            return;
        }
        // Blend so transparent pixels outside the letterbox don't overwrite sidebar textures.
        SDL.SetTextureBlendMode(_pictureAdjustTexture, SDL.BlendMode.Blend);
    }

    private void SyncMotionEffectTexture()
    {
        if (_motionEffectTexture != IntPtr.Zero) { SDL.DestroyTexture(_motionEffectTexture); _motionEffectTexture = IntPtr.Zero; }
        if ((!_hasShaderMotionEffect && !_hasPhosphorPersistence) || !_isGpuRenderer) return;
        _motionEffectTexture = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Target, _viewportWidth, _viewportHeight);
        if (_motionEffectTexture != IntPtr.Zero)
            SDL.SetTextureBlendMode(_motionEffectTexture, SDL.BlendMode.Blend);
        else
            Logger.Log($"[SDL3HwRenderer] Failed to create motion-effect texture: {SDL.GetError()}");
    }

    private void SyncOverlayFilterTexture()
    {
        if (_overlayFilterTexture != IntPtr.Zero) { SDL.DestroyTexture(_overlayFilterTexture); _overlayFilterTexture = IntPtr.Zero; }
        if (!_hasOverlayFilter || !_isGpuRenderer) return;
        _overlayFilterTexture = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Target, _viewportWidth, _viewportHeight);
        if (_overlayFilterTexture != IntPtr.Zero)
            SDL.SetTextureBlendMode(_overlayFilterTexture, SDL.BlendMode.Blend);
        else
            Logger.Log($"[SDL3HwRenderer] Failed to create overlay filter texture: {SDL.GetError()}");
    }

    private void SyncPhosphorTextures()
    {
        if (_phosphorTexA != IntPtr.Zero) { SDL.DestroyTexture(_phosphorTexA); _phosphorTexA = IntPtr.Zero; }
        if (_phosphorTexB != IntPtr.Zero) { SDL.DestroyTexture(_phosphorTexB); _phosphorTexB = IntPtr.Zero; }
        if (!_hasPhosphorPersistence || !_isGpuRenderer) return;

        _phosphorTexA = CreatePhosphorAccumulationTexture();
        _phosphorTexB = CreatePhosphorAccumulationTexture();
        _phosphorUseA = true;

        if (_phosphorTexA == IntPtr.Zero || _phosphorTexB == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] Failed to create phosphor accumulation textures: {SDL.GetError()}");
    }

    // Creates a viewport-sized accumulation buffer and clears it to transparent black so
    // the first frame's history term contributes nothing to max(current, history * decay).
    private IntPtr CreatePhosphorAccumulationTexture()
    {
        IntPtr tex = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Target, _viewportWidth, _viewportHeight);
        if (tex == IntPtr.Zero) return tex;

        SDL.SetTextureBlendMode(tex, SDL.BlendMode.Blend);
        SDL.SetRenderTarget(_sdlRenderer, tex);
        SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
        SDL.RenderClear(_sdlRenderer);
        SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);
        return tex;
    }

    private void SyncPictureAdjustRenderState()
    {
        if (!_isGpuRenderer) return;
        bool needTexture = _hasPictureAdjust;
        bool hasTexture  = _pictureAdjustTexture != IntPtr.Zero;
        if (needTexture && !hasTexture) SyncPictureAdjustTexture();
        else if (!needTexture && hasTexture)
        {
            SDL.DestroyTexture(_pictureAdjustTexture);
            _pictureAdjustTexture = IntPtr.Zero;
        }

        if (!_hasPictureAdjust) { _pictureAdjustRenderState?.Dispose(); _pictureAdjustRenderState = null; return; }
        if (_pictureAdjustRenderState is not null) return;
        _pictureAdjustRenderState = new SdlGpuRenderState(
            _sdlRenderer, _gpuDevice,
            "NEShim.Rendering.Shaders.Vulkan.PictureAdjust.ps.spv",
            numFragmentSamplers:     1,
            numFragmentUniformBuffers: 1);
    }

    // ---- Resource helpers --------------------------------------------------------------

    private IntPtr CreateNesTexture()
    {
        IntPtr tex = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Streaming, _nesWidth, _nesHeight);
        if (tex == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] CreateTexture (NES) failed: {SDL.GetError()}");
        return tex;
    }

    private void CreateOverlayResources()
    {
        DisposeOverlayResources();

        _fontCache        = new SDL3FontCache();
        _overlaySurface   = SDL.CreateSurface(_viewportWidth, _viewportHeight, SDL.PixelFormat.ARGB8888);
        _softwareRenderer = SDL.CreateSoftwareRenderer(_overlaySurface);
        _paintContext     = new SDL3PaintContext(_softwareRenderer, _fontCache, _viewportWidth, _viewportHeight);

        _overlayTexture = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Streaming, _viewportWidth, _viewportHeight);
        if (_overlayTexture != IntPtr.Zero)
            SDL.SetTextureBlendMode(_overlayTexture, SDL.BlendMode.Blend);

        _overlayDirty = true;
    }

    private void DisposeOverlayResources()
    {
        if (_overlayTexture   != IntPtr.Zero) { SDL.DestroyTexture(_overlayTexture);    _overlayTexture   = IntPtr.Zero; }
        _paintContext = null;
        if (_softwareRenderer != IntPtr.Zero) { SDL.DestroyRenderer(_softwareRenderer); _softwareRenderer = IntPtr.Zero; }
        if (_overlaySurface   != IntPtr.Zero) { SDL.DestroySurface(_overlaySurface);    _overlaySurface   = IntPtr.Zero; }
        _fontCache?.Dispose();
        _fontCache = null;
    }

    private (IntPtr texture, (int W, int H) size) CreateSidebarTexture(IntPtr surface)
    {
        if (surface == IntPtr.Zero) return (IntPtr.Zero, default);
        var (w, h) = SDL3PaintContext.GetSurfaceSize(surface);
        if (w <= 0 || h <= 0) return (IntPtr.Zero, default);

        IntPtr converted = SDL.ConvertSurface(surface, SDL.PixelFormat.ARGB8888);
        if (converted == IntPtr.Zero) return (IntPtr.Zero, default);
        try
        {
            IntPtr tex = SDL.CreateTextureFromSurface(_sdlRenderer, converted);
            return (tex, (w, h));
        }
        finally
        {
            SDL.DestroySurface(converted);
        }
    }

    private void DisposeSidebarTextures()
    {
        if (_leftSidebarTex  != IntPtr.Zero) { SDL.DestroyTexture(_leftSidebarTex);  _leftSidebarTex  = IntPtr.Zero; }
        if (_rightSidebarTex != IntPtr.Zero) { SDL.DestroyTexture(_rightSidebarTex); _rightSidebarTex = IntPtr.Zero; }
        _leftSidebarSize  = default;
        _rightSidebarSize = default;
        _hasSidebars      = false;
    }
}
