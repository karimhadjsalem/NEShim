namespace NEShim.Rendering.Filters;

internal sealed class BilinearSdlFilter : ISdlFilter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode              => VideoFilterMode.Bilinear;
    public float           PixelAspectRatio        => NesPixelAspect;
    public bool            UseLinearSampler        => true;
    public string?         PixelShaderResourceName => "NEShim.Rendering.Shaders.Vulkan.Jinc2.ps.spv";

    public void WriteUniformData(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
        buffer[2] = 0f;
    }
}
