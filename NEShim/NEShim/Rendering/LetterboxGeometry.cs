namespace NEShim.Rendering;

/// <summary>
/// Pure-math helper for computing the D3D11 clip-space letterbox rectangle.
/// Extracted from D3D11Renderer so the geometry calculation can be unit tested
/// independently of the Direct3D device.
/// </summary>
internal static class LetterboxGeometry
{
    internal const float UnderscanScale = 0.88f;

    internal readonly record struct Result(
        float X0, float X1,
        float Y0, float Y1,
        int   PixelW, int PixelH);

    /// <summary>
    /// Computes D3D11 clip-space edges and pixel dimensions of the letterbox rect.
    /// </summary>
    /// <param name="contentWidth">NES frame pixel width.</param>
    /// <param name="displayHeight">Effective NES display height (may exclude overscan rows).</param>
    /// <param name="pixelAspectRatio">Filter PAR (e.g. 8/7 for standard NES).</param>
    /// <param name="viewportWidth">Swap chain / window width in pixels.</param>
    /// <param name="viewportHeight">Swap chain / window height in pixels.</param>
    /// <param name="underscan">When true, shrinks the dest rect by <see cref="UnderscanScale"/>.</param>
    internal static Result Compute(
        int   contentWidth,
        int   displayHeight,
        float pixelAspectRatio,
        int   viewportWidth,
        int   viewportHeight,
        bool  underscan)
    {
        float displayAspect = contentWidth * pixelAspectRatio / displayHeight;
        float windowAspect  = (float)viewportWidth / viewportHeight;

        float destW, destH;
        if (windowAspect > displayAspect)
        {
            destH = viewportHeight;
            destW = destH * displayAspect;
        }
        else
        {
            destW = viewportWidth;
            destH = destW / displayAspect;
        }

        if (underscan)
        {
            destW *= UnderscanScale;
            destH *= UnderscanScale;
        }

        float destX = (viewportWidth  - destW) / 2f;
        float destY = (viewportHeight - destH) / 2f;

        // D3D clip space: x ∈ [-1,1], y=+1 at top, y=-1 at bottom.
        float x0 = (destX / viewportWidth)           * 2f - 1f;
        float x1 = ((destX + destW) / viewportWidth) * 2f - 1f;
        float y0 = 1f - (destY / viewportHeight) * 2f;
        float y1 = 1f - ((destY + destH) / viewportHeight) * 2f;

        return new Result(x0, x1, y0, y1,
            Math.Max(1, (int)destW),
            Math.Max(1, (int)destH));
    }
}
