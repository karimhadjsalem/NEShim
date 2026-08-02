namespace NEShim.Rendering;

/// <summary>
/// Writes the fixed 4-float cbuffer/uniform-buffer layout shared by both render backends
/// (D3D11's b0 cbuffer and SDL_GPU's FilterParams uniform buffer use the identical layout — see
/// CLAUDE.md's "Cbuffer layout fixed" rule): [0..2] filter-specific structural params, written by
/// the active filter's own callback; [3] color mode, always owned by the renderer, never the filter.
///
/// Single source of truth for exactly which width/height value feeds a filter's structural
/// params: <c>nesHeight</c> must always be the renderer's fixed, allocated NES texture height,
/// never a frame-varying content height (BizHawk's per-frame buffer height, which can be smaller
/// than the allocated texture — e.g. 224 vs 240 visible NES lines). A filter's own neighbour-
/// sampling math sizes itself against this value, and must match the real texel grid it actually
/// samples from. This function existing is a direct response to a real bug: SDL3HwRenderer once
/// passed the frame-varying content height here while D3D11Renderer correctly used the fixed
/// texture height, desyncing the Xbr filter's texelSize from its real texture and visibly
/// dropping/misaligning rows — Linux only, since D3D11 never had the chance to diverge. Both
/// renderers now call this same method instead of writing the 4 floats independently, so that
/// mistake can no longer recur silently in just one of the two backends.
/// </summary>
internal static class FilterUniformWriter
{
    /// <summary>Matches both <c>ID3D11Filter.WriteBaseParams</c> and <c>ISdlFilter.WriteUniformData</c>'s signature.</summary>
    internal delegate void ParamWriter(Span<float> buffer, int nesWidth, int nesHeight);

    /// <param name="applyColorMode">
    /// <c>false</c> defers color grading to a later stage in the pass chain (overlay, shader
    /// motion effect) that will apply it instead — writes 0 (colorMode "None") so this pass's
    /// output stays ungraded. <c>true</c> writes <paramref name="colorMode"/> as-is.
    /// </param>
    internal static void Write(
        Span<float> buffer, ParamWriter writeStructuralParams,
        int contentWidth, int nesHeight,
        VideoColorFilterMode colorMode, bool applyColorMode)
    {
        writeStructuralParams(buffer, contentWidth, nesHeight);
        buffer[3] = applyColorMode ? (float)colorMode : 0f;
    }
}
