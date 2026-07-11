namespace NEShim.Rendering;

/// <summary>
/// Strategy interface for NES frame rendering. Implemented by D3D11Renderer and SDL3HwRenderer.
/// All methods are called on the main thread unless noted otherwise.
/// </summary>
internal interface IFrameRenderer : IDisposable
{
    /// <summary>
    /// Uploads one frame's pixel data. Marshalled to the main thread from the emulation thread
    /// via MarshalToMainThread. <paramref name="contentWidth"/> and <paramref name="contentHeight"/>
    /// are the active NES output dimensions (e.g. 256×224 for NTSC with default overscan).
    /// The pixel buffer may be larger than contentWidth×contentHeight; only the first
    /// contentHeight rows contain valid video data.
    /// </summary>
    void UploadFrame(ReadOnlySpan<int> pixels, int contentWidth, int contentHeight);

    /// <summary>
    /// Presents the last uploaded frame and keeps the Steam overlay heartbeat alive.
    /// During gameplay, called immediately after <see cref="UploadFrame"/> in the same
    /// MarshalToMainThread batch. When the emulation loop is paused, called from the SDL
    /// OnIdle callback (~60 Hz) with <paramref name="vsync"/> false to keep the overlay hook
    /// fed without blocking.
    /// </summary>
    void Tick(bool vsync);

    /// <summary>Recreates size-dependent resources after a window resize.</summary>
    void Resize(int width, int height);

    /// <summary>Updates the FPS overlay state. May be called from the emulation thread (volatile write).</summary>
    void UpdateFpsOverlay(bool show, float fps);

    /// <summary>Sets or clears sidebar SDL surfaces drawn in letterbox bars. Renderer takes no ownership of the surfaces.</summary>
    void SetSidebars(IntPtr left, IntPtr right);

    /// <summary>Shows a brief toast notification.</summary>
    void ShowToast(string text);

    /// <summary>Shows an achievement-unlocked notification.</summary>
    void ShowAchievementNotification(string name);

    /// <summary>Fired when the GPU device is lost. Only D3D11Renderer fires this; SDL3HwRenderer never does.</summary>
    event EventHandler? DeviceLost;

    /// <summary>
    /// True when this renderer owns the swap chain surface and paints frames directly.
    /// Always true for D3D11Renderer and SDL3HwRenderer.
    /// </summary>
    bool OwnsFrameSurface { get; }

    /// <summary>Registers the provider supplying per-frame menu/logo paint callbacks.</summary>
    void SetMenuSceneProvider(IMenuSceneProvider? provider);

    /// <summary>
    /// Marks the overlay texture as needing a repaint on the next <see cref="Tick"/>.
    /// Called when menu state changes (navigation, screen transition).
    /// </summary>
    void MarkOverlayDirty();

    /// <summary>
    /// Applies Brightness / Contrast / Saturation / Hue picture adjustments.
    /// Values are integers in the range -100..100; 0 = neutral for all four.
    /// Routes through a dedicated post-process pass on both D3D11 and SDL_GPU paths.
    /// </summary>
    void SetPictureAdjust(int brightness, int contrast, int saturation, int hue);

    /// <summary>
    /// Applies an overscan mode change immediately. Safe to call mid-game — takes effect
    /// on the next rendered frame.
    /// </summary>
    void SetOverscanMode(OverscanMode overscan);

    /// <summary>
    /// Applies filter and overscan settings at startup before the first frame.
    /// SDL3HwRenderer overrides this to translate D3D11 filters to their ISdlFilter equivalents.
    /// D3D11Renderer override configures DXBC shaders directly.
    /// </summary>
    void InitializeRenderingOptions(
        Filters.ID3D11Filter filter,
        OverscanMode         overscan,
        VideoColorFilterMode colorMode = VideoColorFilterMode.None) { }

    /// <summary>
    /// Changes the structural video filter. D3D11Renderer applies DXBC; SDL3HwRenderer
    /// translates via SdlFilterFactory to apply SPIR-V via SDL_GPURenderState.
    /// </summary>
    void SetFilter(Filters.ID3D11Filter filter) { }

    /// <summary>
    /// Sets or clears the two-pass overlay filter (D3D11 only).
    /// SDL3HwRenderer's override is a no-op — overlay is not supported on SDL_GPU.
    /// </summary>
    void SetOverlayFilter(Filters.ID3D11Filter? overlay) { }

    /// <summary>
    /// Sets the colour-grade mode applied after structural filtering.
    /// Supported on both D3D11 and SDL_GPU (shared ColorGrade.hlsli).
    /// </summary>
    void SetColorFilter(VideoColorFilterMode mode) { }

    /// <summary>
    /// Sets the motion effect. CrtJitter, ScanlineBob, and MagneticDistortion are supported
    /// on both D3D11 and SDL_GPU. PhosphorPersistence is D3D11 only; SDL3HwRenderer's
    /// SdlMotionEffectFactory demotes it to None.
    /// </summary>
    void SetMotionEffect(VideoMotionEffectMode mode) { }
}
