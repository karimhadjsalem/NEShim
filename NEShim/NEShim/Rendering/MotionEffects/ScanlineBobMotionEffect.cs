namespace NEShim.Rendering.MotionEffects;

internal sealed class ScanlineBobMotionEffect : IMotionEffect
{
    // Clip-space amplitude at the 1080p reference resolution. Scaling keeps the pixel
    // displacement constant across resolutions: amplitude = Reference * 1080 / viewportH.
    private const float ReferenceAmplitude  = 0.00099f;
    private const float ReferenceViewportH  = 1080f;

    private float _bobAmplitude;

    public VideoMotionEffectMode EffectMode => VideoMotionEffectMode.ScanlineBob;

    public void NotifyLayout(int viewportHeight, int letterboxHeight)
    {
        if (viewportHeight <= 0)
            return;
        _bobAmplitude = ReferenceAmplitude * ReferenceViewportH / viewportHeight;
    }

    public (float Dx, float Dy) GetFrameOffset(long frameCount)
    {
        float dy = (frameCount % 2 == 0) ? _bobAmplitude : -_bobAmplitude;
        return (0f, dy);
    }
}
