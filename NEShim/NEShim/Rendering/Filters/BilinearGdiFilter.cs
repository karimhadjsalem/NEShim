namespace NEShim.Rendering.Filters;

/// <summary>
/// GDI+ bilinear ("smooth") filter. Uses square 1:1 pixels and a bilinear scaler,
/// matching the appearance of a low-quality CRT or LCD upscale with soft edges.
/// </summary>
internal sealed class BilinearGdiFilter : IGdiFilter
{
    public VideoFilterMode FilterMode       => VideoFilterMode.Bilinear;
    public float           PixelAspectRatio => 1f;
    public IGraphicsScaler CreateScaler()   => new BilinearScaler();
}
