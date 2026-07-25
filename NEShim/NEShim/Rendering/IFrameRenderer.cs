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
    /// MarshalToMainThread batch, with <paramref name="vsync"/> true (RenderCoordinator).
    /// When the emulation loop is paused or hasn't started yet (logo/menu/carousel), called
    /// from the SDL OnIdle callback and the gamepad-nav handlers, also with vsync true — a
    /// no-vsync present here used to leave SDL3HwRenderer's Vulkan swapchain uncomposited on
    /// Linux/KWin until something else (e.g. an alt-tab focus change) forced a recomposite,
    /// even though Tick/Present were both still running every idle iteration underneath
    /// (reproduced on real Kubuntu, July 2026). D3D11's flip-model swap chain never had this
    /// problem, so it stayed invisible on Windows.
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

    /// <summary>
    /// Fired when the GPU device is lost. Both renderers implement this, but detect loss
    /// differently, reflecting what each platform's API actually exposes: D3D11Renderer checks
    /// the swap chain Present call's HRESULT for the structured DXGI_ERROR_DEVICE_REMOVED/RESET
    /// codes and fires immediately; SDL3HwRenderer has no such structured signal (SDL.RenderPresent
    /// returns only a bool plus a free-form error string) and requires several consecutive
    /// present failures before firing, to avoid mistaking a transient, self-recovering hiccup
    /// (e.g. a resize-driven swapchain race) for a genuine device loss.
    /// </summary>
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
    /// Evicts the overlay paint context's cached GPU texture for <paramref name="surface"/>, if
    /// one exists. Callers that own an SDL surface previously drawn via a stateless renderer's
    /// <c>BlitSurface</c>/<c>BlitSurfaceAlpha</c> (e.g. GameCarouselScreen's thumbnails and
    /// animated background frames) MUST call this before destroying that surface — otherwise a
    /// later, unrelated surface allocated at the same (recycled) address could incorrectly reuse
    /// the stale cached texture. No-op if the overlay paint context doesn't currently exist
    /// (device loss/resize) or never cached a texture for this surface.
    /// </summary>
    void InvalidateSurfaceTexture(IntPtr surface);

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
    /// Applies filter and overscan settings at startup before the first frame. Takes the
    /// platform-neutral <see cref="VideoFilterMode"/> rather than a concrete filter object —
    /// each renderer resolves it through its own factory (D3D11Renderer via D3D11FilterFactory,
    /// SDL3HwRenderer via SdlFilterFactory) so callers never need to know which platform is
    /// active to pick a filter.
    /// </summary>
    void InitializeRenderingOptions(
        VideoFilterMode       mode,
        OverscanMode          overscan,
        VideoColorFilterMode  colorMode = VideoColorFilterMode.None) { }

    /// <summary>
    /// Changes the structural video filter. D3D11Renderer resolves <paramref name="mode"/> via
    /// D3D11FilterFactory and applies DXBC; SDL3HwRenderer resolves it via SdlFilterFactory and
    /// applies SPIR-V via SDL_GPURenderState.
    /// </summary>
    void SetFilter(VideoFilterMode mode) { }

    /// <summary>
    /// Sets or clears the two-pass overlay filter (null clears it). Supported on both D3D11 and
    /// SDL_GPU, each resolving <paramref name="mode"/> through its own factory as in
    /// <see cref="SetFilter"/>.
    /// </summary>
    void SetOverlayFilter(VideoFilterMode? mode) { }

    /// <summary>
    /// Sets the colour-grade mode applied after structural filtering.
    /// Supported on both D3D11 and SDL_GPU (shared ColorGrade.hlsli).
    /// </summary>
    void SetColorFilter(VideoColorFilterMode mode) { }

    /// <summary>
    /// Sets the motion effect. All four modes (CrtJitter, ScanlineBob, MagneticDistortion,
    /// PhosphorPersistence) are supported on both D3D11 and SDL_GPU. PhosphorPersistence has
    /// no SPIR-V shader on the SDL_GPU path — SDL3HwRenderer reproduces it via GPU blend
    /// compositing instead (see SDL3HwRenderer's class doc comment).
    /// </summary>
    void SetMotionEffect(VideoMotionEffectMode mode) { }
}
