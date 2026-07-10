namespace NEShim.Rendering.Filters;

internal sealed class CrtScreenVulkanFilter : IGpuFilter
{
    private const float BarrelStrength   = 0.12f;
    private const float ChromaStrength   = 0.006f;
    private const float VignetteStrength = 0.35f;
    private const float NesPixelAspect   = 8f / 7f;

    public VideoFilterMode FilterMode              => VideoFilterMode.CrtScreen;
    public float           PixelAspectRatio        => NesPixelAspect;
    public bool            UseLinearSampler        => true;
    public string?         PixelShaderResourceName => "NEShim.Rendering.Shaders.Vulkan.CrtScreen.ps.spv";

    public void WriteUniformData(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = BarrelStrength;
        buffer[1] = ChromaStrength;
        buffer[2] = VignetteStrength;
    }
}
