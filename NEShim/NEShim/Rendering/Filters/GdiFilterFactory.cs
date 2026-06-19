namespace NEShim.Rendering.Filters;

/// <summary>
/// Creates the <see cref="IGdiFilter"/> implementation for a given <see cref="VideoFilterMode"/>.
/// Called by MainForm after filter validation to inject the correct behaviour into GdiRenderer.
/// </summary>
internal static class GdiFilterFactory
{
    public static IGdiFilter Create(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.Bilinear     => new BilinearGdiFilter(),
        VideoFilterMode.PixelPerfect => new PixelPerfectGdiFilter(),
        _                            => new PixelPerfectGdiFilter(),
    };
}
