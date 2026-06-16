namespace NEShim.Rendering.Filters;

/// <summary>
/// GDI+ pixel-perfect filter. Applies the NES 8:7 pixel aspect ratio and uses a
/// point (nearest-neighbour) scaler, preserving hard pixel edges at any scale factor.
/// </summary>
internal sealed class PixelPerfectGdiFilter : IGdiFilter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode       => VideoFilterMode.PixelPerfect;
    public float           PixelAspectRatio => NesPixelAspect;
    public IGraphicsScaler CreateScaler()   => new NearestNeighborScaler();
}
