namespace NEShim.Rendering.Filters;

internal sealed class BilinearD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode       => VideoFilterMode.Bilinear;
    public float           PixelAspectRatio => NesPixelAspect;
    public bool            UseLinearSampler => true;
    // PixelShaderResourceName: null → passthrough shader (color grade only, no structural effect)
}
