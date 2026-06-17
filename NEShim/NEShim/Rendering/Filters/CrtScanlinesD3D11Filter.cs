namespace NEShim.Rendering.Filters;

internal sealed class CrtScanlinesD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect    = 8f / 7f;
    private const float ScanlineIntensity = 0.70f;

    public VideoFilterMode FilterMode       => VideoFilterMode.CrtScanlines;
    public float           PixelAspectRatio => NesPixelAspect;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.CrtScanlines.ps.cso";

    public void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
        buffer[2] = ScanlineIntensity;
    }
}
