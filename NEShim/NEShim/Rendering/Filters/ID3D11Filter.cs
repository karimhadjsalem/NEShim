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
    /// When <c>true</c> the renderer binds a linear-clamp sampler instead of point-clamp.
    /// Default is <c>false</c> — suitable for all pixel-art filters.
    /// </summary>
    bool UseLinearSampler => false;

    /// <summary>
    /// Fills <paramref name="buffer"/>[0..2] with filter-specific shader parameters.
    /// Slot [3] is reserved for the colour mode and is written by the renderer — do not touch it here.
    /// Default is a no-op (all zeros) — suitable for filters with no structural parameters.
    /// </summary>
    /// <remarks>
    /// The b0 cbuffer is fixed at exactly 4 floats. No filter may use a second constant buffer.
    /// If a filter genuinely needs more than 3 configuration floats, update the design rule in
    /// CLAUDE.md and <c>D3D11Renderer</c> deliberately rather than working around it silently.
    /// </remarks>
    void WriteBaseParams(Span<float> buffer, int nesWidth, int nesHeight) { }
}
