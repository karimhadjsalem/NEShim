namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// Extends <see cref="IMotionEffect"/> with SDL_GPU / SPIR-V shader support.
/// Mirrors the relationship between <see cref="Filters.ISdlFilter"/> and
/// <see cref="Filters.ID3D11Filter"/> — the SPIR-V resource name and sampler
/// counts replace the D3D11 .cso-based equivalents.
/// </summary>
internal interface ISdlMotionEffect : IMotionEffect
{
    /// <summary>
    /// Embedded resource name of the SPIR-V fragment shader (.spv).
    /// <c>null</c> means no shader pass — the effect is CPU-only (quad offset via
    /// <see cref="IMotionEffect.GetFrameOffset"/>).
    /// </summary>
    string? SpvResourceName => null;

    new bool UseLinearSampler         => false;
    uint     NumFragmentSamplers      => 1;
    uint     NumFragmentUniformBuffers => 1;
}
