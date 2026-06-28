namespace NEShim.Rendering.Filters;

internal sealed class BilinearD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode             => VideoFilterMode.Bilinear;
    public float           PixelAspectRatio       => NesPixelAspect;
    public bool            UseLinearSampler       => true;
    public string          PixelShaderResourceName => "NEShim.Rendering.Shaders.Jinc2.ps.cso";

    public void WriteBaseParams(Span<float> p, int contentWidth, int contentHeight)
    {
        p[0] = contentWidth;
        p[1] = contentHeight;
        p[2] = 0f;
    }
}
