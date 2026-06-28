namespace NEShim.Rendering.MotionEffects;

/// <summary>
/// Simulates the subtle hold instability of an old CRT TV by applying a small per-frame
/// clip-space offset to the NES frame quad. The drift is driven by the product of two
/// sinusoids at irrational-ratio frequencies, producing a bounded non-repeating signal
/// that reads as organic drift without a noise table or RNG.
/// </summary>
internal sealed class CrtJitterMotionEffect : IMotionEffect
{
    // Clip-space amplitudes at the 1920×1080 reference resolution.
    // X scales with viewport width, Y scales with viewport height independently.
    private const float RefMaxDx          = 0.0004f;
    private const float RefMaxDy          = 0.00005f;
    private const float ReferenceViewportW = 1920f;
    private const float ReferenceViewportH = 1080f;

    // Frequencies in radians-per-frame. At 60 fps the horizontal signal changes sign
    // every ~3-6 frames, reading as nervous micro-jitter rather than slow sway.
    // Using irrational-ratio pairs keeps the pattern non-repeating.
    private const float DxFreq1 = 0.51f;   // ~12-frame period
    private const float DxFreq2 = 1.06f;   // ~6-frame period
    private const float DyFreq1 = 0.35f;
    private const float DyFreq2 = 1.18f;

    private float _maxDx = RefMaxDx;
    private float _maxDy = RefMaxDy;

    public VideoMotionEffectMode EffectMode => VideoMotionEffectMode.CrtJitter;

    public void NotifyLayout(int viewportWidth, int viewportHeight, int letterboxHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;
        _maxDx = RefMaxDx * ReferenceViewportW / viewportWidth;
        _maxDy = RefMaxDy * ReferenceViewportH / viewportHeight;
    }

    public (float Dx, float Dy) GetFrameOffset(long frameCount)
    {
        float t  = frameCount;
        float dx = MathF.Sin(t * DxFreq1) * MathF.Sin(t * DxFreq2) * _maxDx;
        float dy = MathF.Cos(t * DyFreq1) * MathF.Cos(t * DyFreq2) * _maxDy;
        return (dx, dy);
    }
}
