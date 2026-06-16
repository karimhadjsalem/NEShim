namespace NEShim.Rendering.Filters;

/// <summary>
/// Encapsulates the rendering behaviour for a single D3D11 video filter mode.
/// Injected into D3D11Renderer so that adding a shader-based filter (CRT, NTSC, etc.)
/// requires only a new class — no switch statements inside the renderer.
///
/// Future implementations will expose shader resource setup and constant-buffer data;
/// PixelPerfectD3D11Filter (the current default) requires only the pixel aspect ratio.
/// </summary>
internal interface ID3D11Filter
{
    VideoFilterMode FilterMode { get; }

    /// <summary>
    /// Horizontal stretch factor used in the destination rect aspect-ratio calculation.
    /// 8/7 for the standard NES pixel-aspect ratio.
    /// </summary>
    float PixelAspectRatio { get; }
}
