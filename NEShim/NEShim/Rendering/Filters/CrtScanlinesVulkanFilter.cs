namespace NEShim.Rendering.Filters;

internal sealed class CrtScanlinesVulkanFilter : IGpuFilter
{
    private const float NesPixelAspect    = 8f / 7f;
    private const float ScanlineIntensity = 0.45f;

    public VideoFilterMode FilterMode              => VideoFilterMode.CrtScanlines;
    public float           PixelAspectRatio        => NesPixelAspect;
    public string?         PixelShaderResourceName => "NEShim.Rendering.Shaders.Vulkan.CrtScanlines.ps.spv";

    public void WriteUniformData(Span<float> buffer, int nesWidth, int nesHeight)
    {
        buffer[0] = nesWidth;
        buffer[1] = nesHeight;
        buffer[2] = ScanlineIntensity;
    }
}
