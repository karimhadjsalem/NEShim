namespace NEShim.Rendering.Filters;

/// <summary>
/// Encapsulates the rendering behaviour for a single SDL_GPU / Vulkan video filter mode.
/// Mirrors <see cref="ID3D11Filter"/> for the non-D3D11 render path.
/// </summary>
internal interface IGpuFilter
{
    VideoFilterMode FilterMode       { get; }

    /// <summary>Horizontal stretch factor for the NES pixel-aspect ratio (8/7 for standard NES).</summary>
    float PixelAspectRatio { get; }

    /// <summary>
    /// Embedded resource name for the SPIR-V fragment shader (.spv).
    /// <c>null</c> means use SDL's default passthrough path (no render state applied).
    /// </summary>
    string? PixelShaderResourceName => null;

    bool UseLinearSampler => false;

    /// <summary>Number of fragment samplers declared in the SPIR-V shader (usually 1).</summary>
    uint NumFragmentSamplers => 1;

    /// <summary>Number of fragment uniform buffers declared in the SPIR-V shader (0 for passthrough).</summary>
    uint NumFragmentUniformBuffers => 1;

    /// <summary>
    /// Fills <paramref name="buffer"/>[0..2] with filter-specific shader parameters.
    /// Slot [3] is reserved for the colour mode and is written by the renderer.
    /// Default is a no-op — suitable for filters with no structural parameters.
    /// </summary>
    void WriteUniformData(Span<float> buffer, int nesWidth, int nesHeight) { }

    void NotifyFrame(int frameCount) { }
}
