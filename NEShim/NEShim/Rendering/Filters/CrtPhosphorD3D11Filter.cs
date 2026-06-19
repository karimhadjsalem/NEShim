namespace NEShim.Rendering.Filters;

internal sealed class CrtPhosphorD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect    = 8f / 7f;
    private const float ScanlineIntensity = 0.45f;

    public VideoFilterMode FilterMode       => VideoFilterMode.CrtPhosphor;
    public float           PixelAspectRatio => NesPixelAspect;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.CrtPhosphor.ps.cso";

    public void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
        buffer[2] = ScanlineIntensity;
    }
}
