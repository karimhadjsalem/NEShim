namespace NEShim.Rendering.Filters;

/// <summary>
/// Scale2x / EPX edge-preserving upscaler.
/// Each NES texel is conceptually expanded to a 2×2 block; for each output pixel the
/// quadrant rule decides whether to use the centre texel or one of its cardinal
/// neighbours, sharpening edges while leaving diagonals and flat regions unchanged.
/// Point sampling is used so that the neighbour reads snap to exact texel centres.
/// </summary>
internal sealed class XbrD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode       => VideoFilterMode.Xbr;
    public float           PixelAspectRatio => NesPixelAspect;
    public bool            UseLinearSampler => false;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.Xbr.ps.cso";

    public void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
    }
}
