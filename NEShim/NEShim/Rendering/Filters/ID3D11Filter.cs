namespace NEShim.Rendering.Filters;

/// <summary>
/// Encapsulates the rendering behaviour for a single D3D11 video filter mode.
/// Injected into D3D11Renderer so that adding a shader-based filter requires only
/// a new class — no switch statements inside the renderer.
/// </summary>
internal interface ID3D11Filter
{
    VideoFilterMode FilterMode       { get; }

    /// <summary>
    /// Horizontal stretch factor for the destination rect aspect-ratio calculation.
    /// 8/7 for the standard NES pixel-aspect ratio.
    /// </summary>
    float PixelAspectRatio { get; }

    /// <summary>
    /// Embedded resource name for the pixel shader (.cso).
    /// <c>null</c> means use the passthrough shader (Passthrough.ps.cso).
    /// </summary>
    string? PixelShaderResourceName => null;

    /// <summary>
    /// Fills <paramref name="buffer"/>[0..2] with structural shader parameters.
    /// The renderer always writes the active colour mode into [3].
    /// Default is a no-op (all zeros) — suitable for PixelPerfect.
    /// </summary>
    void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight) { }
}
