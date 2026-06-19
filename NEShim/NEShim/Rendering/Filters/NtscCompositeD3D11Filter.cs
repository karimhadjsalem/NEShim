namespace NEShim.Rendering.Filters;

internal sealed class NtscCompositeD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;
    private const float ChromaStrength = 0.75f;

    private int _frameParity;

    public VideoFilterMode FilterMode       => VideoFilterMode.NtscComposite;
    public float           PixelAspectRatio => NesPixelAspect;

    public string? PixelShaderResourceName
        => "NEShim.Rendering.Shaders.NtscComposite.ps.cso";

    public void NotifyFrame(int frameCount) => _frameParity = frameCount & 1;

    public void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth  > 0 ? 1f / nesWidth  : 0f;
        buffer[1] = _frameParity;
        buffer[2] = ChromaStrength;
    }
}
