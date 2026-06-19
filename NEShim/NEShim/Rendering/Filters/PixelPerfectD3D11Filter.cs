namespace NEShim.Rendering.Filters;

internal sealed class PixelPerfectD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode       => VideoFilterMode.PixelPerfect;
    public float           PixelAspectRatio => NesPixelAspect;
}
