namespace NEShim.Rendering;

/// <summary>Which pipeline stage owns the per-frame color-grade application this frame.</summary>
internal enum RenderStage
{
    Filter,
    Overlay,
    MotionEffect,
}

/// <summary>
/// Pure, platform-agnostic decision shared by <see cref="D3D11Renderer"/> and
/// <see cref="SDL3HwRenderer"/>: exactly one stage in the up-to-4-stage pipeline (structural
/// filter → overlay → motion-effect shader → picture adjust) applies the active color grade each
/// frame — the <em>last</em> color-aware stage present. Phosphor accumulation and picture adjust
/// are color-blind post-processes and never apply it themselves, regardless of whether they run
/// after the color-applying stage.
///
/// Both renderers used to re-derive this rule independently, in different idioms (D3D11 via
/// nested if/else branch structure, SDL via explicit booleans) — the same shape of duplication
/// that caused a real, shipped bug once before (see <see cref="FilterUniformWriter"/>'s doc
/// comment for the NTSC/Xbr `contentHeight` divergence it was extracted to fix). Extracted here
/// so the rule exists in exactly one place, unit-tested without any GPU dependency.
/// </summary>
internal static class RenderPassPlanner
{
    /// <param name="hasOverlay">A second-pass overlay filter is active this frame.</param>
    /// <param name="hasMotionEffectShader">
    /// A shader-backed motion effect (not a CPU quad-offset effect, and not phosphor-persistence
    /// accumulation — see the class doc comment) is active this frame.
    /// </param>
    public static RenderStage ColorApplyingStage(bool hasOverlay, bool hasMotionEffectShader)
    {
        if (hasMotionEffectShader) return RenderStage.MotionEffect;
        if (hasOverlay) return RenderStage.Overlay;
        return RenderStage.Filter;
    }
}
