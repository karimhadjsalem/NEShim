namespace NEShim.Rendering.Filters;

/// <summary>
/// Encapsulates the rendering behaviour for a single GDI+ video filter mode.
/// Injected into GamePanel and GdiRenderer so that adding a new GDI+ filter
/// requires only a new class — no switch statements inside the renderers.
/// </summary>
internal interface IGdiFilter
{
    VideoFilterMode FilterMode { get; }

    /// <summary>
    /// Horizontal stretch factor relative to the source pixel count.
    /// 1.0 = square pixels (bilinear); 8/7 = NES pixel-aspect ratio (pixel-perfect).
    /// </summary>
    float PixelAspectRatio { get; }

    /// <summary>Creates the GDI+ interpolation scaler appropriate for this filter.</summary>
    IGraphicsScaler CreateScaler();
}
