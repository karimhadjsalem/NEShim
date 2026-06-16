namespace NEShim.Rendering.Filters;

/// <summary>
/// Creates the <see cref="ID3D11Filter"/> implementation for a given <see cref="VideoFilterMode"/>.
/// Called by MainForm after filter validation to inject the correct behaviour into D3D11Renderer.
/// As D3D11 shader filters are added, this factory gains new cases.
/// </summary>
internal static class D3D11FilterFactory
{
    public static ID3D11Filter Create(VideoFilterMode mode) => mode switch
    {
        VideoFilterMode.PixelPerfect => new PixelPerfectD3D11Filter(),
        _                            => new PixelPerfectD3D11Filter(),
    };
}
