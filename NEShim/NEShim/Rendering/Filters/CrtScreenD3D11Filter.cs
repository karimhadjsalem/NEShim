namespace NEShim.Rendering.Filters;

internal sealed class CrtScreenD3D11Filter : ID3D11Filter
{
    private const float BarrelStrength   = 0.12f;
    private const float ChromaStrength   = 0.006f;
    private const float VignetteStrength = 0.35f;

    public VideoFilterMode FilterMode       => VideoFilterMode.CrtScreen;
    public float           PixelAspectRatio => 8f / 7f;
    public bool            UseLinearSampler => true;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.CrtScreen.ps.cso";

    public void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = BarrelStrength;
        buffer[1] = ChromaStrength;
        buffer[2] = VignetteStrength;
    }
}
