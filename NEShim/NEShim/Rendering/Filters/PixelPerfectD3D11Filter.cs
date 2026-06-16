namespace NEShim.Rendering.Filters;

/// <summary>
/// D3D11 pixel-perfect filter. Uses the NES 8:7 pixel aspect ratio and the
/// point-clamp sampler already set up in D3D11Renderer (no extra shader work required).
/// This is the only D3D11 filter in the 2.0 release; CRT and NTSC shader filters
/// are implemented in subsequent milestones.
/// </summary>
internal sealed class PixelPerfectD3D11Filter : ID3D11Filter
{
    private const float NesPixelAspect = 8f / 7f;

    public VideoFilterMode FilterMode       => VideoFilterMode.PixelPerfect;
    public float           PixelAspectRatio => NesPixelAspect;
}
