namespace NEShim.Rendering.Filters;

/// <summary>
/// Creates the <see cref="IGpuFilter"/> implementation for a given <see cref="VideoFilterMode"/>.
/// Mirrors <see cref="D3D11FilterFactory"/> for the SDL_GPU / Vulkan render path.
/// </summary>
internal static class GpuFilterFactory
{
    public static IGpuFilter Create(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.PixelPerfect  => new PixelPerfectVulkanFilter(),
        VideoFilterMode.Bilinear      => new BilinearVulkanFilter(),
        VideoFilterMode.CrtScanlines  => new CrtScanlinesVulkanFilter(),
        VideoFilterMode.CrtPhosphor   => new CrtPhosphorVulkanFilter(),
        VideoFilterMode.NtscComposite => new NtscCompositeVulkanFilter(),
        VideoFilterMode.CrtScreen     => new CrtScreenVulkanFilter(),
        VideoFilterMode.Xbr           => new XbrVulkanFilter(),
        _                             => new PixelPerfectVulkanFilter(),
    };
}
