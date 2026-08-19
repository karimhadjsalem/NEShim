namespace NEShim.Rendering.Filters;

internal sealed class XbrSdlFilter : ISdlFilter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode              => VideoFilterMode.Xbr;
    public float           PixelAspectRatio        => NesPixelAspect;
    public string?         PixelShaderResourceName => "NEShim.Rendering.Shaders.Vulkan.Xbr.ps.spv";

    public void WriteUniformData(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
    }
}
