namespace NEShim.Rendering.Filters;

internal sealed class PixelPerfectSdlFilter : ISdlFilter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode              => VideoFilterMode.PixelPerfect;
    public float           PixelAspectRatio        => NesPixelAspect;
    public string?         PixelShaderResourceName => null;
    public uint            NumFragmentSamplers     => 1;
    public uint            NumFragmentUniformBuffers => 0;
}
