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
/// - Structural filters, the overlay filter, shader-backed motion effects, and picture adjust all
///   run through <see cref="SdlGpuPipeline"/> — a full custom SDL_GPUGraphicsPipeline per filter,
///   sharing one vertex shader and one static fullscreen-quad vertex buffer (see the fields near
///   the top of this class). This replaced SDL_CreateGPURenderState (SdlFilterFactory →
///   ISdlFilter still supplies the fragment shader resource name/sampler count either way), which
///   reproducibly caused SDL's input event queue to go silent forever the first time a
///   shader-bound frame was drawn, on both WSL2 and Steam Deck — see SdlGpuPipeline's own doc
///   comment and github.com/libsdl-org/SDL/issues/13892 for the matching upstream report.
/// - Color effects share ColorGrade.hlsli with the D3D11 path; the colorMode uniform is written identically.
/// - CRT Jitter, Scanline Bob, and Magnetic Distortion motion effects are fully supported (SdlMotionEffectFactory).
/// - Video Overlay (second-pass filter slot: CrtScanlines/CrtPhosphor/CrtScreen) is supported as an
///   additional sequential pass, mirroring the D3D11 overlay slot.
/// - PhosphorPersistence (Screen Glow) uses its own real 2-sampler shader (currentFrame +
///   historyFrame, both bound simultaneously — see RunPhosphorPass), now that the custom pipeline
///   supports more than one fragment sampler.
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
    // The NES source frame's *own* SDL_GPUTexture, uploaded via SDL_UploadToGPUTexture (cycle=true)
    // instead of going through _nesTexture's SDL_Renderer-managed Streaming/LockTexture path. Every
    // other texture our custom pipeline reads via the GPU-texture-pointer property bridge
    // (SdlGpuPipeline.GetGpuTexture) is written *only* by our own render passes, so SDL_Renderer's
    // own internal tracking for that resource never conflicts with our manual reads. _nesTexture is
    // the one exception — SDL_LockTexture/UnlockTexture write it through SDL_Renderer's own opaque,
    // undocumented Streaming-texture upload mechanism, entirely separate from the SDL_GPU-tracked
    // resource state our manually-submitted render passes rely on — mixing the two caused SDL's
    // input event pump to silently stop delivering events the moment real gameplay frames began
    // uploading (reproduced on both WSL2 and Steam Deck; confirmed absent with no filter active,
    // since the PixelPerfect fast path only ever reads _nesTexture via SDL_Renderer's own
    // SDL_RenderTexture, never through the property bridge). Keeping the NES frame entirely inside
    // the SDL_GPU-managed system end-to-end (our own texture, our own cycle-aware upload, our own
    // render pass read) avoids the conflict — matches SDL's documented cycling model, under which
    // resources synchronize automatically as long as all access goes through the SDL_GPU API.
    private IntPtr _nesGpuTexture;
    private IntPtr _nesUploadTransferBuffer;

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

    // Picture-adjust scaling: -100..100 input range mapped to each field's working range.
    private const float BrightnessScale = 0.002f;
    private const float PercentScale    = 0.01f;  // contrast/saturation: 1 + value * PercentScale
    private const float HueScale        = MathF.PI / 100f; // -100..100 → -π..+π rad

    private IMenuSceneProvider? _menuSceneProvider;

    private volatile bool  _showFps;
    private volatile float _currentFps;
    private string?        _toastText;
    private DateTime       _toastExpiry;

    private bool _vsync;

    // Shared custom-pipeline infrastructure (see SdlGpuPipeline's class doc comment for why this
    // replaces SDL_CreateGPURenderState). One vertex shader + one static fullscreen-quad vertex
    // buffer, reused by every SdlGpuPipeline instance below — every custom-pipeline draw targets
    // a full-size offscreen texture (never the swapchain). Every stage *after* the structural
    // filter reads and writes that same full-viewport-sized texture 1:1 (no resize between them),
    // so the static -1,1..1,-1 clip-space quad with 0,0..1,1 UV is correct for all of them — no
    // per-draw vertex updates needed.
    private IntPtr _gpuVertexShader;
    private IntPtr _quadVertexBuffer;
    // The structural filter is the one custom-pipeline consumer that both samples a *cropped*
    // region of its source texture (overscan) and must land its output at the real letterboxed
    // destination rect within the full-viewport-sized target texture, not stretched to fill it —
    // rendering to the full -1,1..1,-1 quad here (as every later stage correctly does, since by
    // then the content already occupies the right sub-rect) would draw the NES image at the wrong,
    // aspect-distorted scale, then rely on a second SDL_RenderTexture downscale back to the correct
    // letterbox size at final composite to fix it up. That second resize is lossy enough to drop
    // whole texel rows from filters that produce exact single-pixel features (Xbr/"Sharp" — same
    // failure mode as the D3D11 floor()/frac() line-truncation bug, but caused here by the extra
    // resize pass rather than by shader-internal precision). So this buffer holds the same
    // clip-space quad but with both UVs matching the current overscan crop *and* positions matching
    // the real destination rect (in clip space, relative to the full viewport-sized target),
    // rewritten once per frame (cheap — 96 bytes) in RunFilterPass/UpdateFilterSourceQuad. Every
    // later stage then reads/writes that already-correctly-positioned content 1:1 via the plain
    // full quad above, and the final composite becomes a same-size positional copy (jitter offset
    // only) instead of a scale.
    private IntPtr _filterSourceVertexBuffer;
    private IntPtr _filterSourceTransferBuffer;
    private IntPtr _nearestSampler;
    private IntPtr _linearSampler;
    private SDL.GPUTextureFormat _gpuColorTargetFormat = SDL.GPUTextureFormat.B8G8R8A8Unorm;
    private IntPtr _activeCommandBuffer; // valid only between DrawAndPresent's acquire/submit

    // Active filter state
    private ISdlFilter     _activeFilter      = new PixelPerfectSdlFilter();
    private SdlGpuPipeline? _filterPipeline;

    // Shared fallback used only when the active structural filter has no shader of its own
    // (PixelPerfect) but a color filter is active — without it, colorMode is silently dropped
    // whenever nothing else this frame (overlay/motion-effect shader/picture adjust) already
    // provides a shader pass to apply it in. Created once alongside the rest of the custom-
    // pipeline infrastructure; see RunFilterPass's fallback branch and DrawNesFrame's fast-path
    // gate, which both need to agree on when this is actually needed a given frame.
    private SdlGpuPipeline? _passthroughColorGradePipeline;
    private VideoColorFilterMode _activeColorMode = VideoColorFilterMode.None;
    private long  _frameCount;

    // Written into by whichever custom-pipeline stage is last in the active chain that frame
    // (filter alone, overlay, motion-effect shader/phosphor, or picture adjust — see DrawNesFrame)
    // since none of them may render straight to the backbuffer. The structural filter pass already
    // placed its output at the real (unjittered) letterbox rect within this full-viewport-sized
    // texture (see _filterSourceVertexBuffer's doc comment), so the final plain SDL_RenderTexture
    // composite is a same-size positional copy — source rect = that same unjittered letterbox rect,
    // dest rect = it shifted by motion-effect jitter — never a scale. Allocated whenever the GPU
    // renderer is active, regardless of which filter/effects are currently selected, since any of
    // them could be switched on at any time.
    private IntPtr _finalStageOutputTexture;

    // Active overlay filter state (two-pass overlay: CrtScanlines/CrtPhosphor/CrtScreen
    // composited as a second pass on top of the primary structural filter).
    private Filters.ISdlFilter? _activeOverlayFilter;
    private SdlGpuPipeline?     _overlayFilterPipeline;
    private IntPtr              _overlayFilterTexture;
    private bool                _hasOverlayFilter;

    // Active motion effect state
    private IMotionEffect      _activeMotionEffect      = new NoneMotionEffect();
    private SdlGpuPipeline?    _motionEffectPipeline;
    private IntPtr             _motionEffectTexture;
    private bool               _hasShaderMotionEffect;

    // Phosphor persistence: temporal accumulation via its own 2-sampler shader (currentFrame +
    // historyFrame), now that the custom pipeline supports more than one fragment sampler —
    // see PhosphorPersistenceMotionEffect's class doc comment.
    private bool   _hasPhosphorPersistence;
    private SdlGpuPipeline? _phosphorPipeline;
    private IntPtr _phosphorTexA;
    private IntPtr _phosphorTexB;
    private bool   _phosphorUseA = true;

    // Picture adjust state
    private float _brightness;
    private float _contrast   = 1f;
    private float _saturation = 1f;
    private float _hue;
    private bool  _hasPictureAdjust;
    private IntPtr         _pictureAdjustTexture;
    private SdlGpuPipeline? _pictureAdjustPipeline;

    public bool OwnsFrameSurface => true;

    /// <summary>True when SDL_CreateGPURenderer succeeded (SPIR-V shader support available).</summary>
    internal bool IsGpuRendererActive => _isGpuRenderer;

    // Consecutive RenderPresent failures before treating the GPU/Vulkan device as genuinely
    // lost — see PresentAndCheckDeviceLost's doc comment for why this needs debouncing (unlike
    // D3D11Renderer's single-shot DXGI error-code check).
    private const int DeviceLostFailureThreshold = 3;
    private int _consecutivePresentFailures;

    public event EventHandler? DeviceLost;

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

        if (_isGpuRenderer)
            InitializeCustomPipelineInfrastructure();

        Logger.Log($"[SDL3HwRenderer] Initialised ({nesWidth}×{nesHeight}, ARGB8888). " +
                   $"GPU filters: {(_isGpuRenderer ? "enabled" : "disabled")}.");
    }

    // Shared vertex shader, static fullscreen-quad vertex buffer, and the two samplers every
    // SdlGpuPipeline draw needs — see the fields' own doc comment for why one static quad works
    // for every consumer. The vertex buffer is populated once via a throwaway transfer buffer
    // (destroyed immediately after) since its content never changes afterward.
    // {pos.xy, uv.xy} per vertex, 16 bytes/vertex, 6 vertices (2 triangles) — the size of every
    // full-quad GPU buffer this class uploads (shared quad + filter-source quad alike).
    private const uint QuadVertexBufferSizeBytes = 6 * 4 * sizeof(float);

    private unsafe void InitializeCustomPipelineInfrastructure()
    {
        _gpuVertexShader = SdlGpuPipeline.LoadVertexShader(_gpuDevice);
        if (_gpuVertexShader == IntPtr.Zero)
        {
            Logger.Log("[SDL3HwRenderer] Shared vertex shader failed to load; custom-pipeline filters disabled.");
            return;
        }

        _nearestSampler = CreateSampler(linear: false);
        _linearSampler  = CreateSampler(linear: true);
        SyncFinalStageOutputTexture();

        if (!CreateSharedQuadVertexBuffer()) return;

        CreateFilterSourceBuffers();
        CreateNesGpuUploadPath();
        CreatePassthroughColorGradePipeline();
    }

    // Creates and uploads the static full-quad vertex buffer shared by every custom-pipeline draw
    // that doesn't need per-frame positioning (see UpdateFilterSourceQuad's doc comment for the
    // one draw that does). Returns false only for the two failures that leave nothing left to draw
    // with — a failed one-time upload (mapping) is logged nowhere in the original code either and
    // does not abort the rest of initialization, so that non-fatal case is preserved as-is here.
    private unsafe bool CreateSharedQuadVertexBuffer()
    {
        Span<float> quad = stackalloc float[]
        {
            -1f,  1f, 0f, 0f,
             1f,  1f, 1f, 0f,
             1f, -1f, 1f, 1f,
            -1f,  1f, 0f, 0f,
             1f, -1f, 1f, 1f,
            -1f, -1f, 0f, 1f,
        };
        uint quadBytes = QuadVertexBufferSizeBytes;

        var vbCreateInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Vertex, Size = quadBytes, Props = 0 };
        _quadVertexBuffer = SDL.CreateGPUBuffer(_gpuDevice, in vbCreateInfo);
        if (_quadVertexBuffer == IntPtr.Zero)
        {
            Logger.Log($"[SDL3HwRenderer] CreateGPUBuffer (quad) failed: {SDL.GetError()}");
            return false;
        }

        var transferInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = quadBytes, Props = 0 };
        IntPtr transferBuffer = SDL.CreateGPUTransferBuffer(_gpuDevice, in transferInfo);
        if (transferBuffer == IntPtr.Zero)
        {
            Logger.Log($"[SDL3HwRenderer] CreateGPUTransferBuffer (quad) failed: {SDL.GetError()}");
            return false;
        }

        IntPtr mapped = SDL.MapGPUTransferBuffer(_gpuDevice, transferBuffer, false);
        if (mapped != IntPtr.Zero)
        {
            fixed (float* src = quad)
                Buffer.MemoryCopy(src, (void*)mapped, quadBytes, quadBytes);
            SDL.UnmapGPUTransferBuffer(_gpuDevice, transferBuffer);

            IntPtr uploadCmd = SDL.AcquireGPUCommandBuffer(_gpuDevice);
            if (uploadCmd != IntPtr.Zero)
            {
                IntPtr copyPass = SDL.BeginGPUCopyPass(uploadCmd);
                var srcLoc = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
                var dstReg = new SDL.GPUBufferRegion { Buffer = _quadVertexBuffer, Offset = 0, Size = quadBytes };
                SDL.UploadToGPUBuffer(copyPass, in srcLoc, in dstReg, false);
                SDL.EndGPUCopyPass(copyPass);
                SDL.SubmitGPUCommandBuffer(uploadCmd);
            }
        }
        SDL.ReleaseGPUTransferBuffer(_gpuDevice, transferBuffer);
        return true;
    }

    // Per-frame-rewritable quad buffer used by UpdateFilterSourceQuad — see that method's doc
    // comment. Failure here (like the NES upload path below) is logged but non-fatal: the affected
    // custom-pipeline stage simply doesn't draw, the rest of initialization still proceeds.
    private void CreateFilterSourceBuffers()
    {
        var filterVbInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Vertex, Size = QuadVertexBufferSizeBytes, Props = 0 };
        _filterSourceVertexBuffer = SDL.CreateGPUBuffer(_gpuDevice, in filterVbInfo);
        if (_filterSourceVertexBuffer == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] CreateGPUBuffer (filter source quad) failed: {SDL.GetError()}");

        var filterTransferInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = QuadVertexBufferSizeBytes, Props = 0 };
        _filterSourceTransferBuffer = SDL.CreateGPUTransferBuffer(_gpuDevice, in filterTransferInfo);
        if (_filterSourceTransferBuffer == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] CreateGPUTransferBuffer (filter source quad) failed: {SDL.GetError()}");
    }

    // GPU-side NES source texture + its upload transfer buffer, sized once at NES resolution
    // (never resized — the NES frame size is fixed for the process lifetime).
    private void CreateNesGpuUploadPath()
    {
        var nesTexInfo = new SDL.GPUTextureCreateInfo
        {
            Type              = SDL.GPUTextureType.TextureType2D,
            Format            = _gpuColorTargetFormat,
            Usage             = SDL.GPUTextureUsageFlags.Sampler,
            Width             = (uint)_nesWidth,
            Height            = (uint)_nesHeight,
            LayerCountOrDepth = 1,
            NumLevels         = 1,
            SampleCount       = SDL.GPUSampleCount.SampleCount1,
            Props             = 0,
        };
        _nesGpuTexture = SDL.CreateGPUTexture(_gpuDevice, in nesTexInfo);
        if (_nesGpuTexture == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] CreateGPUTexture (NES source) failed: {SDL.GetError()}");

        uint nesBytes = (uint)(_nesWidth * _nesHeight * 4);
        var nesTransferInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = nesBytes, Props = 0 };
        _nesUploadTransferBuffer = SDL.CreateGPUTransferBuffer(_gpuDevice, in nesTransferInfo);
        if (_nesUploadTransferBuffer == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] CreateGPUTransferBuffer (NES source) failed: {SDL.GetError()}");
    }

    // See _passthroughColorGradePipeline's field doc comment. NumFragmentSamplers/
    // NumFragmentUniformBuffers match Passthrough.ps.hlsl's declared bindings (1 combined
    // image sampler, 1 uniform buffer) via ISdlFilter's interface defaults.
    private void CreatePassthroughColorGradePipeline()
    {
        _passthroughColorGradePipeline = new SdlGpuPipeline(
            _gpuDevice, _gpuVertexShader, _gpuColorTargetFormat,
            "NEShim.Rendering.Shaders.Vulkan.Passthrough.ps.spv",
            numFragmentSamplers: 1, numFragmentUniformBuffers: 1);
    }

    // Rewrites _filterSourceVertexBuffer's positions and UVs to match the current overscan crop
    // (srcPixels, in NES-texture pixel coordinates) and the real letterboxed destination rect
    // (destPixels, in viewport pixel coordinates) — see the buffer's own field doc comment for why
    // the structural filter alone needs this instead of the static full-quad buffer. Must run on
    // _activeCommandBuffer, between DrawAndPresent's acquire/submit.
    private unsafe void UpdateFilterSourceQuad(SDL.FRect srcPixels, SDL.FRect destPixels)
    {
        if (_filterSourceVertexBuffer == IntPtr.Zero || _filterSourceTransferBuffer == IntPtr.Zero
            || _activeCommandBuffer == IntPtr.Zero || _nesWidth == 0 || _nesHeight == 0
            || _viewportWidth == 0 || _viewportHeight == 0)
            return;

        float u0 = srcPixels.X / _nesWidth;
        float v0 = srcPixels.Y / _nesHeight;
        float u1 = (srcPixels.X + srcPixels.W) / _nesWidth;
        float v1 = (srcPixels.Y + srcPixels.H) / _nesHeight;

        // destPixels is in top-left-origin, Y-down pixel space; clip space is centre-origin,
        // Y-up — flip Y on the way in.
        float x0 = destPixels.X                   / _viewportWidth  * 2f - 1f;
        float x1 = (destPixels.X + destPixels.W)   / _viewportWidth  * 2f - 1f;
        float y0 = 1f - destPixels.Y                 / _viewportHeight * 2f;
        float y1 = 1f - (destPixels.Y + destPixels.H) / _viewportHeight * 2f;

        Span<float> quad = stackalloc float[]
        {
            x0, y0, u0, v0,
            x1, y0, u1, v0,
            x1, y1, u1, v1,
            x0, y0, u0, v0,
            x1, y1, u1, v1,
            x0, y1, u0, v1,
        };
        uint quadBytes = (uint)(quad.Length * sizeof(float));

        // cycle=true on both the map and the upload: this buffer is rewritten every frame, so
        // cycling lets SDL rotate to a fresh underlying allocation if the previous frame's is
        // still in use by in-flight GPU work, avoiding a CPU-side stall waiting on the GPU.
        IntPtr mapped = SDL.MapGPUTransferBuffer(_gpuDevice, _filterSourceTransferBuffer, true);
        if (mapped == IntPtr.Zero) return;
        fixed (float* src = quad)
            Buffer.MemoryCopy(src, (void*)mapped, quadBytes, quadBytes);
        SDL.UnmapGPUTransferBuffer(_gpuDevice, _filterSourceTransferBuffer);

        IntPtr copyPass = SDL.BeginGPUCopyPass(_activeCommandBuffer);
        var srcLoc = new SDL.GPUTransferBufferLocation { TransferBuffer = _filterSourceTransferBuffer, Offset = 0 };
        var dstReg = new SDL.GPUBufferRegion { Buffer = _filterSourceVertexBuffer, Offset = 0, Size = quadBytes };
        SDL.UploadToGPUBuffer(copyPass, in srcLoc, in dstReg, true);
        SDL.EndGPUCopyPass(copyPass);
    }

    private IntPtr CreateSampler(bool linear)
    {
        var info = new SDL.GPUSamplerCreateInfo
        {
            MinFilter    = linear ? SDL.GPUFilter.Linear : SDL.GPUFilter.Nearest,
            MagFilter    = linear ? SDL.GPUFilter.Linear : SDL.GPUFilter.Nearest,
            MipmapMode   = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
        };
        IntPtr sampler = SDL.CreateGPUSampler(_gpuDevice, in info);
        if (sampler == IntPtr.Zero)
            Logger.Log($"[SDL3HwRenderer] CreateGPUSampler ({(linear ? "linear" : "nearest")}) failed: {SDL.GetError()}");
        return sampler;
    }

    private static IntPtr TryCreateGpuRenderer(IntPtr sdlWindow, out IntPtr gpuDevice)
    {
        gpuDevice = IntPtr.Zero;
        try
        {
            // SDL_CreateGPURenderer's real signature is (SDL_GPUDevice *device, SDL_Window
            // *window) — device first, window second, with no shader-format parameter at all
            // (verified against the SDL wiki and the SDL3-CS binding's declared parameter
            // names). Passing IntPtr.Zero for device lets SDL create one automatically; its
            // shader format is queried afterward via GetGPURendererDevice, not requested here.
            IntPtr renderer = SDL.CreateGPURenderer(IntPtr.Zero, sdlWindow);
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
    public void InvalidateSurfaceTexture(IntPtr surface) => _paintContext?.InvalidateTexture(surface);

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

        // _nesGpuTexture is only ever sampled by RunFilterPass's custom-pipeline branch (the
        // active filter has a real fragment shader, or the passthrough+color-grade fallback
        // below is needed) — PixelPerfect with no color filter never reads it, so skip the
        // upload entirely rather than paying for a wasted acquire/copy-pass/submit cycle every
        // single frame. That "no-op" GPU submit turned out not to be free: on a
        // software/virtualized Vulkan backend (VirtualBox's llvmpipe fallback, reproduced there
        // directly) it was one of two needless per-frame submits (DrawAndPresent's own empty
        // command buffer is the other, see its comment) that together sent PixelPerfect's frame
        // pacing into a repeating ~1 FPS / ~200 FPS / multi-second-stall cycle, while any real
        // filter/effect — which actually uses this texture and so always needed the upload
        // anyway — ran fine throughout. A non-default color filter is an equally deliberate,
        // uncommon choice, so paying this cost for it too (even in the rare case it turns out
        // unnecessary because an overlay/motion-effect shader will apply colorMode instead this
        // frame) is an acceptable, simple trade — see RunFilterPass's fallback branch.
        if (_isGpuRenderer && (_filterPipeline is { IsValid: true } || _activeColorMode != VideoColorFilterMode.None))
            UploadNesGpuTexture(pixels, contentWidth, contentHeight);
    }

    // Uploads the same pixel data into _nesGpuTexture — see that field's doc comment for why this
    // exists alongside _nesTexture's own SDL_LockTexture-based upload above. Uses its own
    // acquire/submit since this runs before DrawAndPresent's _activeCommandBuffer exists for the
    // frame; cycle=true on both the map and the upload lets SDL rotate to a fresh underlying
    // allocation if the previous frame's copy is still being sampled by in-flight GPU work.
    private unsafe void UploadNesGpuTexture(ReadOnlySpan<int> pixels, int contentWidth, int contentHeight)
    {
        if (_nesGpuTexture == IntPtr.Zero || _nesUploadTransferBuffer == IntPtr.Zero) return;

        IntPtr mapped = SDL.MapGPUTransferBuffer(_gpuDevice, _nesUploadTransferBuffer, true);
        if (mapped == IntPtr.Zero) return;
        var srcBytes = MemoryMarshal.AsBytes(pixels);
        fixed (byte* src = srcBytes)
            Buffer.MemoryCopy(src, (void*)mapped, srcBytes.Length, srcBytes.Length);
        SDL.UnmapGPUTransferBuffer(_gpuDevice, _nesUploadTransferBuffer);

        IntPtr cmd = SDL.AcquireGPUCommandBuffer(_gpuDevice);
        if (cmd == IntPtr.Zero) return;
        IntPtr copyPass = SDL.BeginGPUCopyPass(cmd);
        var source = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = _nesUploadTransferBuffer,
            Offset         = 0,
            PixelsPerRow   = (uint)contentWidth,
            RowsPerLayer   = (uint)contentHeight,
        };
        var destination = new SDL.GPUTextureRegion
        {
            Texture  = _nesGpuTexture,
            MipLevel = 0,
            Layer    = 0,
            X        = 0,
            Y        = 0,
            Z        = 0,
            W        = (uint)contentWidth,
            H        = (uint)contentHeight,
            D        = 1,
        };
        SDL.UploadToGPUTexture(copyPass, in source, in destination, true);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(cmd);
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
        // Acquired once per frame — every custom-pipeline draw this frame (structural filter,
        // overlay filter, motion-effect shader/phosphor, picture adjust) shares it, submitted
        // once at the end so all of that GPU work is queued and ordered before SDL_Renderer's
        // own subsequent draws/present of the same textures (submission order on one device's
        // queue is what gives this the synchronization it needs — no explicit fences required).
        // Skipped entirely when nothing this frame will actually record into it (e.g. pure
        // PixelPerfect with no overlay/motion-effect/picture-adjust — DrawNesFrame's fast path
        // never touches _activeCommandBuffer) — acquiring+submitting a command buffer with zero
        // recorded work in it every frame is wasted work, and on a software/virtualized Vulkan
        // backend (VirtualBox's llvmpipe fallback, reproduced there directly) it was one of two
        // needless per-frame submits (UploadNesGpuTexture's own redundant upload is the other, see
        // its comment) that together sent PixelPerfect's frame pacing into a repeating ~1 FPS /
        // ~200 FPS / multi-second-stall cycle.
        if (_isGpuRenderer && NeedsCustomPipelineWork())
            _activeCommandBuffer = SDL.AcquireGPUCommandBuffer(_gpuDevice);

        SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 255);
        SDL.RenderClear(_sdlRenderer);

        ComputeLetterbox(out SDL.FRect nesDest, out float sidebarW);

        if (_hasSidebars)
            DrawSidebars(nesDest, sidebarW);

        DrawNesFrame(nesDest);
        DrawOverlay();

        if (_activeCommandBuffer != IntPtr.Zero)
        {
            SDL.SubmitGPUCommandBuffer(_activeCommandBuffer);
            _activeCommandBuffer = IntPtr.Zero;
        }

        PresentAndCheckDeviceLost();
    }

    /// <summary>
    /// Unlike D3D11Renderer's PresentAndCheckResult, which checks the swap chain Present call's
    /// HRESULT for the specific DXGI_ERROR_DEVICE_REMOVED/RESET codes, SDL's renderer API has no
    /// structured error codes to check — RenderPresent returns only a bool plus a free-form
    /// SDL.GetError() string, which can't reliably distinguish a genuinely lost GPU/Vulkan device
    /// from a benign, self-recovering hiccup (e.g. a swapchain-recreation race during a window
    /// resize). Requiring several CONSECUTIVE failures before firing DeviceLost avoids tearing
    /// down and rebuilding the entire renderer over a single transient frame, while still
    /// recovering — rather than permanently freezing on a black/stale frame, which is what
    /// happened before this existed — once a real device loss is confirmed by repetition.
    /// </summary>
    private void PresentAndCheckDeviceLost()
    {
        if (SDL.RenderPresent(_sdlRenderer))
        {
            _consecutivePresentFailures = 0;
            return;
        }

        _consecutivePresentFailures++;
        Logger.Log($"[SDL3HwRenderer] RenderPresent failed ({_consecutivePresentFailures}/{DeviceLostFailureThreshold}): {SDL.GetError()}");

        if (_consecutivePresentFailures >= DeviceLostFailureThreshold)
        {
            Logger.Log("[SDL3HwRenderer] Device appears lost after repeated present failures. Firing DeviceLost event.");
            DeviceLost?.Invoke(this, EventArgs.Empty);
        }
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

    private int ComputeDisplayHeight() => _contentHeight - ComputeOverscanTop() * 2;

    private int ComputeOverscanTop() =>
        (_overscanMode == OverscanMode.Overscan && _contentHeight >= OverscanCropRows * 2)
            ? OverscanCropRows : 0;

    // ---- NES frame draw ----------------------------------------------------------------

    // True when at least one custom-pipeline (SdlGpuPipeline) stage will run this frame — mirrors
    // DrawNesFrame's own usingCustomFilter/hasOverlay/hasMeShader/hasPhosphor/hasPa flags exactly,
    // duplicated here (rather than computed once and passed in) only so DrawAndPresent can decide
    // whether to acquire _activeCommandBuffer at all before DrawNesFrame runs. Keep both in sync.
    private bool NeedsCustomPipelineWork() =>
        _filterPipeline is { IsValid: true }
        || (_hasOverlayFilter && _overlayFilterTexture != IntPtr.Zero && _overlayFilterPipeline is { IsValid: true })
        || (_hasShaderMotionEffect && _motionEffectTexture != IntPtr.Zero && _motionEffectPipeline is { IsValid: true })
        || (_hasPhosphorPersistence && _motionEffectTexture != IntPtr.Zero && _phosphorTexA != IntPtr.Zero
            && _phosphorTexB != IntPtr.Zero && _phosphorPipeline is { IsValid: true })
        || (_hasPictureAdjust && _pictureAdjustTexture != IntPtr.Zero && _pictureAdjustPipeline is { IsValid: true });

    // Pipeline: [structural filter] -> [overlay?] -> [motion-effect shader or phosphor
    // accumulation?] -> [picture adjust?] -> backbuffer. Each optional stage renders into
    // whichever later stage is active next (cascading target selection below), or straight
    // to the backbuffer if it's the last one running. Colour grading is applied by whichever
    // of {filter, overlay, motion-effect shader} is the last colour-aware stage in the chain
    // — phosphor accumulation and picture adjust are colour-blind post-processes and never
    // apply it themselves.
    private void DrawNesFrame(SDL.FRect nominalDest)
    {
        if (_nesTexture == IntPtr.Zero) return;

        int overscanTop   = ComputeOverscanTop();
        int displayHeight = _contentHeight - overscanTop * 2;
        var src = new SDL.FRect { X = 0, Y = overscanTop, W = _contentWidth, H = displayHeight };

        long frame = _frameCount++;
        _activeFilter.NotifyFrame(frame);
        _activeOverlayFilter?.NotifyFrame(frame);

        // Apply CPU-space offset from the motion effect (converts clip-space dx/dy to pixels).
        // nominalDest (unjittered) is what the custom-pipeline stages below draw their content at,
        // within the full-viewport-sized intermediate textures; dest (jittered) is only where the
        // final composite places that already-rendered content on screen — see
        // _filterSourceVertexBuffer's doc comment for why the two must stay separate.
        var (offsetDx, offsetDy) = _activeMotionEffect.GetFrameOffset(frame);
        SDL.FRect dest = nominalDest;
        if (offsetDx != 0f || offsetDy != 0f)
            dest = new SDL.FRect
            {
                X = nominalDest.X + offsetDx * _viewportWidth  / 2f,
                Y = nominalDest.Y + offsetDy * _viewportHeight / 2f,
                W = nominalDest.W,
                H = nominalDest.H,
            };

        bool usingCustomFilter = _filterPipeline is { IsValid: true };
        bool hasOverlay  = _hasOverlayFilter
                        && _overlayFilterTexture   != IntPtr.Zero
                        && _overlayFilterPipeline is { IsValid: true };
        bool hasMeShader = _hasShaderMotionEffect
                        && _motionEffectTexture   != IntPtr.Zero
                        && _motionEffectPipeline is { IsValid: true };
        bool hasPhosphor = _hasPhosphorPersistence
                        && _motionEffectTexture != IntPtr.Zero
                        && _phosphorTexA        != IntPtr.Zero
                        && _phosphorTexB        != IntPtr.Zero
                        && _phosphorPipeline is { IsValid: true };
        bool hasPa       = _hasPictureAdjust
                        && _pictureAdjustTexture   != IntPtr.Zero
                        && _pictureAdjustPipeline is { IsValid: true };

        // A color filter still needs a shader pass even when nothing else this frame provides
        // one (PixelPerfect, no overlay/motion-effect/picture-adjust) — see
        // _passthroughColorGradePipeline's field doc comment and RunFilterPass's fallback branch.
        bool needsColorGradeOnly = !usingCustomFilter && !hasOverlay && !hasMeShader
                                && _activeColorMode != VideoColorFilterMode.None;

        if (!usingCustomFilter && !hasOverlay && !hasMeShader && !hasPhosphor && !hasPa && !needsColorGradeOnly)
        {
            // Fast path: no custom-pipeline stage active this frame — draw straight to the
            // backbuffer via SDL_Renderer's own default pipeline, exactly as before this rewrite.
            SDL.SetTextureScaleMode(_nesTexture, _activeFilter.UseLinearSampler ? SDL.ScaleMode.Linear : SDL.ScaleMode.Nearest);
            SDL.RenderTexture(_sdlRenderer, _nesTexture, in src, in dest);
            return;
        }

        // At least one custom-pipeline stage is active this frame. None of them may render
        // straight to the backbuffer (see SdlGpuPipeline's class doc comment) — every stage
        // always targets an intermediate texture, and exactly one final plain composite
        // (SDL_Renderer's own, unchanged 2D API, at the bottom of this method) lands the last
        // stage's output at the real letterboxed, motion-effect-jittered dest rect.
        IntPtr motionTarget  = hasPa ? _pictureAdjustTexture : _finalStageOutputTexture;
        IntPtr overlayTarget = (hasMeShader || hasPhosphor) ? _motionEffectTexture : motionTarget;
        IntPtr filterTarget  = hasOverlay ? _overlayFilterTexture : overlayTarget;

        var colorStage = RenderPassPlanner.ColorApplyingStage(hasOverlay, hasMeShader);
        bool filterAppliesColorMode  = colorStage == RenderStage.Filter;
        bool overlayAppliesColorMode = colorStage == RenderStage.Overlay;

        RunFilterPass(src, nominalDest, filterTarget, filterAppliesColorMode);

        if (hasOverlay)
            RunOverlayPass(overlayTarget, overlayAppliesColorMode);

        if (hasMeShader)
            RunMotionEffectShaderPass(motionTarget);
        else if (hasPhosphor)
            RunPhosphorPass(motionTarget);

        if (hasPa)
            ApplyPictureAdjustPass(_pictureAdjustTexture, _finalStageOutputTexture);

        // Same-size positional copy, not a scale: the structural filter pass already placed its
        // content at nominalDest within the full-viewport-sized texture, so the source rect here
        // is that same unjittered rect and the dest rect is only shifted by motion-effect jitter.
        SDL.RenderTexture(_sdlRenderer, _finalStageOutputTexture, in nominalDest, in dest);
    }

    // Redirects rendering to an intermediate texture via SDL_Renderer's own SetRenderTarget,
    // clearing it to transparent black first. Only used for the plain (non-custom-pipeline)
    // draws left in this file — RunFilterPass's no-shader (PixelPerfect) fallback and the
    // phosphor-accumulation composite below — never for a custom-pipeline stage, which always
    // targets its own intermediate directly via SdlGpuPipeline.Draw's own render pass instead.
    private bool BeginRenderToTarget(IntPtr target)
    {
        bool toIntermediate = target != IntPtr.Zero;
        if (toIntermediate)
        {
            SDL.SetRenderTarget(_sdlRenderer, target);
            SDL.SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0);
            SDL.RenderClear(_sdlRenderer);
        }
        return toIntermediate;
    }

    // Restores the backbuffer as the render target, undoing a BeginRenderToTarget call that
    // actually redirected (toIntermediate == false means BeginRenderToTarget was already a
    // no-op, so there's nothing to restore here either).
    private void EndRenderToTarget(bool toIntermediate)
    {
        if (toIntermediate)
            SDL.SetRenderTarget(_sdlRenderer, IntPtr.Zero);
    }

    // NES texture -> structural filter (if the active filter has one) -> targetTexture, landed at
    // dest (the real letterboxed rect, in viewport pixel coordinates) within the full-viewport-sized
    // target — never stretched to fill it; see _filterSourceVertexBuffer's doc comment for why.
    // Always redirects to an intermediate now that at least one custom-pipeline stage is active
    // this frame (DrawNesFrame's fast-path check already ruled out "nothing active") — even when
    // the filter itself has no shader (PixelPerfect), since a later stage still needs this
    // texture's content, so that case falls back to a plain SDL_Renderer draw instead of a
    // custom-pipeline one.
    private void RunFilterPass(SDL.FRect src, SDL.FRect dest, IntPtr targetTexture, bool applyColorMode)
    {
        // Falls back to the shared passthrough+color-grade pipeline when the active filter has
        // no shader of its own (PixelPerfect) but a color filter is active — see
        // _passthroughColorGradePipeline's field doc comment. Without this, colorMode would be
        // silently dropped whenever applyColorMode is true (nothing later in the chain — overlay,
        // shader motion effect — already applies it) and the active filter has no shader.
        bool needsColorGradeOnly = applyColorMode && _activeColorMode != VideoColorFilterMode.None;
        SdlGpuPipeline? effectivePipeline = _filterPipeline is { IsValid: true } fp
            ? fp
            : (needsColorGradeOnly ? _passthroughColorGradePipeline : null);

        if (effectivePipeline is { IsValid: true } pipeline)
        {
            UpdateFilterSourceQuad(src, dest);
            if (_nesGpuTexture == IntPtr.Zero) return;

            Span<SDL.GPUTextureSamplerBinding> bindings = stackalloc SDL.GPUTextureSamplerBinding[1];
            bindings[0] = new SDL.GPUTextureSamplerBinding
            {
                Texture = _nesGpuTexture,
                Sampler = _activeFilter.UseLinearSampler ? _linearSampler : _nearestSampler,
            };

            // FilterUniformWriter.Write always uses _nesHeight (fixed, allocated texture height),
            // never _contentHeight (this frame's BizHawk buffer height, which varies) — see its
            // own doc comment for why (this exact field mix-up is what caused Xbr to visibly
            // drop/misalign rows mid-frame, Linux only, before this was extracted into a single
            // shared function both renderers call).
            Span<float> uniforms = stackalloc float[UniformFloats];
            FilterUniformWriter.Write(uniforms, _activeFilter.WriteUniformData, _contentWidth, _nesHeight, _activeColorMode, applyColorMode);

            pipeline.Draw(_activeCommandBuffer, _filterSourceVertexBuffer, targetTexture, bindings, uniforms);
            return;
        }

        // No shader for the active filter (PixelPerfect) and no color filter active — a plain
        // copy via SDL_Renderer's own default pipeline still needs to land the NES texture's
        // cropped content in targetTexture, at dest, for whichever custom-pipeline stage runs next.
        bool toIntermediate = BeginRenderToTarget(targetTexture);
        SDL.SetTextureScaleMode(_nesTexture, SDL.ScaleMode.Nearest);
        SDL.RenderTexture(_sdlRenderer, _nesTexture, in src, in dest);
        EndRenderToTarget(toIntermediate);
    }

    // Overlay intermediate -> overlay filter shader -> targetTexture.
    private void RunOverlayPass(IntPtr targetTexture, bool applyColorMode)
    {
        // Callers only invoke this when DrawNesFrame's hasOverlay check already confirmed this
        // is valid — this guard is a defensive backstop against that invariant ever being
        // violated, not an expected path.
        if (_activeOverlayFilter is null || _overlayFilterPipeline is not { IsValid: true } pipeline) return;

        IntPtr sourceGpuTex = SdlGpuPipeline.GetGpuTexture(_overlayFilterTexture);
        if (sourceGpuTex == IntPtr.Zero) return;

        Span<SDL.GPUTextureSamplerBinding> bindings = stackalloc SDL.GPUTextureSamplerBinding[1];
        bindings[0] = new SDL.GPUTextureSamplerBinding
        {
            Texture = sourceGpuTex,
            Sampler = _activeOverlayFilter.UseLinearSampler ? _linearSampler : _nearestSampler,
        };

        // FilterUniformWriter.Write always uses _nesHeight — see its own doc comment. Overlay
        // filters (CrtScanlines/CrtPhosphor/CrtScreen) need the original NES scanline count
        // regardless of the (already-upscaled) overlay intermediate's actual pixel height.
        Span<float> uniforms = stackalloc float[UniformFloats];
        FilterUniformWriter.Write(uniforms, _activeOverlayFilter.WriteUniformData, _contentWidth, _nesHeight, _activeColorMode, applyColorMode);

        pipeline.Draw(_activeCommandBuffer, _quadVertexBuffer, targetTexture, bindings, uniforms);
    }

    // Motion-effect intermediate -> ME shader -> targetTexture. Always the last colour-aware
    // stage when active, so colour mode is never deferred here.
    private void RunMotionEffectShaderPass(IntPtr targetTexture)
    {
        if (_motionEffectPipeline is not { IsValid: true } pipeline) return;

        IntPtr sourceGpuTex = SdlGpuPipeline.GetGpuTexture(_motionEffectTexture);
        if (sourceGpuTex == IntPtr.Zero) return;

        bool useLinear = _activeMotionEffect is ISdlMotionEffect sdlEffect && sdlEffect.UseLinearSampler;
        Span<SDL.GPUTextureSamplerBinding> bindings = stackalloc SDL.GPUTextureSamplerBinding[1];
        bindings[0] = new SDL.GPUTextureSamplerBinding
        {
            Texture = sourceGpuTex,
            Sampler = useLinear ? _linearSampler : _nearestSampler,
        };

        Span<float> uniforms = stackalloc float[UniformFloats];
        _activeMotionEffect.WriteShaderParams(uniforms, _contentWidth, _contentHeight);
        uniforms[3] = (float)_activeColorMode;

        pipeline.Draw(_activeCommandBuffer, _quadVertexBuffer, targetTexture, bindings, uniforms);
    }

    // Temporal phosphor blend via its own real 2-sampler shader: writeTex = max(currentFrame,
    // historyFrame * decay), both bound simultaneously in one draw — see
    // PhosphorPersistenceMotionEffect's class doc comment for why this replaced the older
    // 2-draw GPU-blend-compositing workaround (SDL_CreateGPURenderState's one-sampler limit).
    // Composites the accumulated frame into targetTexture via a plain SDL_Renderer draw, then
    // swaps the ping-pong roles for next frame. Colour mode was already applied upstream (filter
    // or overlay pass) when this stage is active, since phosphor accumulation is colour-blind,
    // matching picture adjust.
    private void RunPhosphorPass(IntPtr targetTexture)
    {
        if (_phosphorPipeline is not { IsValid: true } pipeline) return;

        IntPtr historyTex = _phosphorUseA ? _phosphorTexA : _phosphorTexB;
        IntPtr writeTex   = _phosphorUseA ? _phosphorTexB : _phosphorTexA;

        IntPtr currentGpuTex = SdlGpuPipeline.GetGpuTexture(_motionEffectTexture);
        IntPtr historyGpuTex = SdlGpuPipeline.GetGpuTexture(historyTex);
        if (currentGpuTex == IntPtr.Zero || historyGpuTex == IntPtr.Zero) return;

        Span<SDL.GPUTextureSamplerBinding> bindings = stackalloc SDL.GPUTextureSamplerBinding[2];
        bindings[0] = new SDL.GPUTextureSamplerBinding { Texture = currentGpuTex, Sampler = _linearSampler };
        bindings[1] = new SDL.GPUTextureSamplerBinding { Texture = historyGpuTex, Sampler = _linearSampler };

        Span<float> uniforms = stackalloc float[UniformFloats];
        _activeMotionEffect.WriteShaderParams(uniforms, _contentWidth, _contentHeight);
        uniforms[3] = (float)_activeColorMode;

        pipeline.Draw(_activeCommandBuffer, _quadVertexBuffer, writeTex, bindings, uniforms);

        bool toIntermediate = BeginRenderToTarget(targetTexture);
        var fullRect = new SDL.FRect { X = 0, Y = 0, W = _viewportWidth, H = _viewportHeight };
        SDL.RenderTexture(_sdlRenderer, writeTex, in fullRect, in fullRect);
        EndRenderToTarget(toIntermediate);

        _phosphorUseA = !_phosphorUseA;
    }

    private void ApplyPictureAdjustPass(IntPtr sourceTexture, IntPtr targetTexture)
    {
        // Same defensive backstop as the other Run*Pass methods — DrawNesFrame's hasPa check
        // already confirmed this is valid before calling here.
        if (_pictureAdjustPipeline is not { IsValid: true } pipeline) return;

        IntPtr sourceGpuTex = SdlGpuPipeline.GetGpuTexture(sourceTexture);
        if (sourceGpuTex == IntPtr.Zero) return;

        Span<SDL.GPUTextureSamplerBinding> bindings = stackalloc SDL.GPUTextureSamplerBinding[1];
        bindings[0] = new SDL.GPUTextureSamplerBinding { Texture = sourceGpuTex, Sampler = _nearestSampler };

        Span<float> uniforms = stackalloc float[UniformFloats];
        uniforms[0] = _brightness;
        uniforms[1] = _contrast;
        uniforms[2] = _saturation;
        uniforms[3] = _hue;

        pipeline.Draw(_activeCommandBuffer, _quadVertexBuffer, targetTexture, bindings, uniforms);
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

    /// <summary>Pure geometry, unit-tested: the SDL-path equivalent of D3D11Renderer.ComputeCoverUV.</summary>
    internal static SDL.FRect ComputeCoverSrcFRect((int W, int H) texSize, float destW, float destH)
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
        SyncFinalStageOutputTexture();
        SyncOverlayFilterTexture();
        SyncPictureAdjustTexture();
        SyncMotionEffectTexture();
        SyncPhosphorTextures();
        _activeMotionEffect.NotifyLayout(_viewportWidth, _viewportHeight, _viewportHeight);
        // A resize is a plausible source of a transient present failure (swapchain recreation
        // race) — start the device-lost debounce fresh rather than carrying over failures that
        // may have been caused by the resize itself, not a real lost device.
        _consecutivePresentFailures = 0;
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

    // Steam's own overlay notification (SetAchievement + StoreStats) only renders when its
    // Vulkan layer can hook the swap chain, which requires the x11 driver. Achievements only
    // exist within Steam, so once that hook is reliable the custom banner is pure duplication —
    // it stays as a fallback only for the case Steam's popup can't render at all (x11 driver
    // selection failed and SDL fell back to Wayland). D3D11Renderer.ShowAchievementNotification
    // has no equivalent check — it's a straight no-op, since its overlay hook (Windows'
    // GameOverlayRenderer64.dll on IDXGISwapChain::Present) has no analogous driver-selection
    // failure mode to hedge against.
    public void ShowAchievementNotification(string name)
    {
        if (!PlatformDetector.IsX11VideoDriverActive)
            ShowToast(name);
    }

    public void SetPictureAdjust(int brightness, int contrast, int saturation, int hue)
    {
        _brightness = brightness * BrightnessScale;
        _contrast   = 1f + contrast   * PercentScale;
        _saturation = 1f + saturation * PercentScale;
        _hue        = hue * HueScale;
        _hasPictureAdjust = _brightness != 0f || _contrast != 1f || _saturation != 1f || _hue != 0f;
        SyncPictureAdjustRenderState();
    }

    public void SetOverscanMode(OverscanMode mode) => _overscanMode = mode;

    public void InitializeRenderingOptions(VideoFilterMode mode, OverscanMode overscan, VideoColorFilterMode colorMode)
    {
        _overscanMode    = overscan;
        _activeColorMode = colorMode;
        ApplyGpuFilter(SdlFilterFactory.Create(mode));
    }

    public void SetFilter(VideoFilterMode mode) => ApplyGpuFilter(SdlFilterFactory.Create(mode));

    public void SetOverlayFilter(VideoFilterMode? mode)
    {
        _overlayFilterPipeline?.Dispose();
        _overlayFilterPipeline = null;
        _activeOverlayFilter   = null;
        _hasOverlayFilter      = false;

        if (mode is { } m && _isGpuRenderer)
        {
            var filter = SdlFilterFactory.Create(m);
            _activeOverlayFilter = filter;
            _hasOverlayFilter    = true;
            if (filter.PixelShaderResourceName is { } resourceName)
            {
                _overlayFilterPipeline = new SdlGpuPipeline(
                    _gpuDevice, _gpuVertexShader, _gpuColorTargetFormat,
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
        _motionEffectPipeline?.Dispose();
        _motionEffectPipeline  = null;
        _phosphorPipeline?.Dispose();
        _phosphorPipeline       = null;
        _hasShaderMotionEffect  = false;
        _hasPhosphorPersistence = false;
        _activeMotionEffect     = SdlMotionEffectFactory.Create(mode);
        _activeMotionEffect.NotifyLayout(_viewportWidth, _viewportHeight, _viewportHeight);

        if (_isGpuRenderer
            && _activeMotionEffect.NeedsTemporalBuffer
            && _activeMotionEffect is ISdlMotionEffect phosphorEffect
            && phosphorEffect.SpvResourceName is { } phosphorSpvName)
        {
            _hasPhosphorPersistence = true;
            _phosphorPipeline = new SdlGpuPipeline(
                _gpuDevice, _gpuVertexShader, _gpuColorTargetFormat,
                phosphorSpvName,
                phosphorEffect.NumFragmentSamplers,
                phosphorEffect.NumFragmentUniformBuffers);
        }
        else if (_isGpuRenderer
            && _activeMotionEffect is ISdlMotionEffect sdlEffect
            && sdlEffect.SpvResourceName is { } spvName)
        {
            _hasShaderMotionEffect = true;
            _motionEffectPipeline = new SdlGpuPipeline(
                _gpuDevice, _gpuVertexShader, _gpuColorTargetFormat,
                spvName,
                sdlEffect.NumFragmentSamplers,
                sdlEffect.NumFragmentUniformBuffers);
        }

        SyncMotionEffectTexture();
        SyncPhosphorTextures();
    }

    public void Dispose()
    {
        _filterPipeline?.Dispose();
        _filterPipeline = null;
        _passthroughColorGradePipeline?.Dispose();
        _passthroughColorGradePipeline = null;
        _overlayFilterPipeline?.Dispose();
        _overlayFilterPipeline = null;
        if (_overlayFilterTexture != IntPtr.Zero) { SDL.DestroyTexture(_overlayFilterTexture); _overlayFilterTexture = IntPtr.Zero; }
        _motionEffectPipeline?.Dispose();
        _motionEffectPipeline = null;
        _phosphorPipeline?.Dispose();
        _phosphorPipeline = null;
        if (_motionEffectTexture  != IntPtr.Zero) { SDL.DestroyTexture(_motionEffectTexture);  _motionEffectTexture  = IntPtr.Zero; }
        if (_phosphorTexA != IntPtr.Zero) { SDL.DestroyTexture(_phosphorTexA); _phosphorTexA = IntPtr.Zero; }
        if (_phosphorTexB != IntPtr.Zero) { SDL.DestroyTexture(_phosphorTexB); _phosphorTexB = IntPtr.Zero; }
        _pictureAdjustPipeline?.Dispose();
        _pictureAdjustPipeline = null;
        if (_pictureAdjustTexture != IntPtr.Zero) { SDL.DestroyTexture(_pictureAdjustTexture); _pictureAdjustTexture = IntPtr.Zero; }
        if (_finalStageOutputTexture != IntPtr.Zero) { SDL.DestroyTexture(_finalStageOutputTexture); _finalStageOutputTexture = IntPtr.Zero; }
        if (_quadVertexBuffer != IntPtr.Zero) { SDL.ReleaseGPUBuffer(_gpuDevice, _quadVertexBuffer); _quadVertexBuffer = IntPtr.Zero; }
        if (_filterSourceVertexBuffer != IntPtr.Zero) { SDL.ReleaseGPUBuffer(_gpuDevice, _filterSourceVertexBuffer); _filterSourceVertexBuffer = IntPtr.Zero; }
        if (_filterSourceTransferBuffer != IntPtr.Zero) { SDL.ReleaseGPUTransferBuffer(_gpuDevice, _filterSourceTransferBuffer); _filterSourceTransferBuffer = IntPtr.Zero; }
        if (_nesGpuTexture != IntPtr.Zero) { SDL.ReleaseGPUTexture(_gpuDevice, _nesGpuTexture); _nesGpuTexture = IntPtr.Zero; }
        if (_nesUploadTransferBuffer != IntPtr.Zero) { SDL.ReleaseGPUTransferBuffer(_gpuDevice, _nesUploadTransferBuffer); _nesUploadTransferBuffer = IntPtr.Zero; }
        if (_nearestSampler != IntPtr.Zero) { SDL.ReleaseGPUSampler(_gpuDevice, _nearestSampler); _nearestSampler = IntPtr.Zero; }
        if (_linearSampler  != IntPtr.Zero) { SDL.ReleaseGPUSampler(_gpuDevice, _linearSampler);  _linearSampler  = IntPtr.Zero; }
        SdlGpuPipeline.ReleaseVertexShader(_gpuDevice, _gpuVertexShader);
        _gpuVertexShader = IntPtr.Zero;
        DisposeOverlayResources();
        DisposeSidebarTextures();
        if (_nesTexture   != IntPtr.Zero) { SDL.DestroyTexture(_nesTexture); _nesTexture = IntPtr.Zero; }
        SDL.DestroyRenderer(_sdlRenderer);
    }

    // ---- GPU filter helpers ------------------------------------------------------------

    private void ApplyGpuFilter(ISdlFilter filter)
    {
        _filterPipeline?.Dispose();
        _filterPipeline = null;
        _activeFilter = filter;
        if (!_isGpuRenderer || filter.PixelShaderResourceName is null) return;
        _filterPipeline = new SdlGpuPipeline(
            _gpuDevice, _gpuVertexShader, _gpuColorTargetFormat,
            filter.PixelShaderResourceName,
            filter.NumFragmentSamplers,
            filter.NumFragmentUniformBuffers);
    }

    private void SyncFinalStageOutputTexture()
    {
        if (_finalStageOutputTexture != IntPtr.Zero) { SDL.DestroyTexture(_finalStageOutputTexture); _finalStageOutputTexture = IntPtr.Zero; }
        if (!_isGpuRenderer) return;
        _finalStageOutputTexture = SDL.CreateTexture(_sdlRenderer, SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Target, _viewportWidth, _viewportHeight);
        if (_finalStageOutputTexture != IntPtr.Zero)
            SDL.SetTextureBlendMode(_finalStageOutputTexture, SDL.BlendMode.Blend);
        else
            Logger.Log($"[SDL3HwRenderer] Failed to create final-stage-output texture: {SDL.GetError()}");
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
        EndRenderToTarget(BeginRenderToTarget(tex));
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

        if (!_hasPictureAdjust) { _pictureAdjustPipeline?.Dispose(); _pictureAdjustPipeline = null; return; }
        if (_pictureAdjustPipeline is not null) return;
        _pictureAdjustPipeline = new SdlGpuPipeline(
            _gpuDevice, _gpuVertexShader, _gpuColorTargetFormat,
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
