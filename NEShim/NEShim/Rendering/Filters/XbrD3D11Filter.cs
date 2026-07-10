namespace NEShim.Rendering.Filters;

/// <summary>
/// xBRZ edge-preserving upscaler.
/// Analyses a 5×5 pixel neighbourhood around each texel to classify edge direction
/// and strength, then blends colours across detected edges using two-stage interpolation.
/// Produces crisper diagonal edges than Scale2x/EPX without the colour bleed of HQx.
/// Point sampling is used so that neighbour reads snap to exact texel centres.
/// </summary>
internal sealed class XbrD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode       => VideoFilterMode.Xbr;
    public float           PixelAspectRatio => NesPixelAspect;
    public bool            UseLinearSampler => false;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.Dx11.Xbr.ps.cso";

    public void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
    }
}

