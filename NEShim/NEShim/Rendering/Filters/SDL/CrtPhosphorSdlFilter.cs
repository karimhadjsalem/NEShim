namespace NEShim.Rendering.Filters;

internal sealed class CrtPhosphorSdlFilter : ISdlFilter
{
    private const float NesPixelAspect    = 8f / 7f;
    private const float ScanlineIntensity = 0.45f;

    public VideoFilterMode FilterMode              => VideoFilterMode.CrtPhosphor;
    public float           PixelAspectRatio        => NesPixelAspect;
    public string?         PixelShaderResourceName => "NEShim.Rendering.Shaders.Vulkan.CrtPhosphor.ps.spv";

    public void WriteUniformData(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
        buffer[2] = ScanlineIntensity;
    }
}
