using System.Drawing;

namespace NEShim.Rendering;

/// <summary>
/// Strategy interface for NES frame rendering. Implemented by D3D11Renderer and GdiRenderer.
/// All methods are called on the UI thread unless noted otherwise.
/// </summary>
internal interface IFrameRenderer : IDisposable
{
    /// <summary>
    /// Uploads one frame's pixel data. Called via BeginInvoke from the emulation thread.
    /// <paramref name="contentWidth"/> and <paramref name="contentHeight"/> are the active NES
    /// output dimensions (e.g. 256×224 for NTSC with default overscan settings).
    /// The pixel buffer may be larger than contentWidth×contentHeight; only the first
    /// contentHeight rows contain valid video data.
    /// </summary>
    void UploadFrame(ReadOnlySpan<int> pixels, int contentWidth, int contentHeight);

    /// <summary>
    /// Presents the last uploaded frame and keeps the Steam overlay heartbeat alive.
    /// During gameplay, called immediately after <see cref="UploadFrame"/> in the same
    /// BeginInvoke batch. When the emulation loop is paused, called from steamTimer (~60 Hz)
    /// with <paramref name="vsync"/> false to keep the overlay hook fed without blocking.
    /// </summary>
    void Tick(bool vsync);

    /// <summary>Recreates size-dependent resources after a window resize.</summary>
    void Resize(int width, int height);

    /// <summary>Updates the FPS overlay state. May be called from the emulation thread (volatile write).</summary>
    void UpdateFpsOverlay(bool show, float fps);

    /// <summary>Sets or clears sidebar images drawn in letterbox bars. Renderer takes no ownership of the bitmaps.</summary>
    void SetSidebars(Bitmap? left, Bitmap? right);

    /// <summary>Shows a brief toast notification.</summary>
    void ShowToast(string text);

    /// <summary>Shows an achievement-unlocked notification.</summary>
    void ShowAchievementNotification(string name);

    /// <summary>Fired when the GPU device is lost. Only D3D11Renderer fires this; GdiRenderer never does.</summary>
    event EventHandler? DeviceLost;

    /// <summary>
    /// True when this renderer owns the game frame surface independently of GamePanel's GDI+ paint.
    /// MainForm hides GamePanel during gameplay when true so the swap chain is visible.
    /// </summary>
    bool OwnsFrameSurface { get; }

    /// <summary>
    /// Registers the provider supplying per-frame menu/logo paint callbacks.
    /// Only consumed by D3D11Renderer; GdiRenderer delegates scene rendering to GamePanel.OnPaint.
    /// </summary>
    void SetMenuSceneProvider(IMenuSceneProvider? provider);

    /// <summary>
    /// Marks the overlay texture as needing a repaint on the next <see cref="Tick"/>.
    /// Called when menu state changes (navigation, screen transition). No-op in GdiRenderer.
    /// </summary>
    void MarkOverlayDirty();
}
