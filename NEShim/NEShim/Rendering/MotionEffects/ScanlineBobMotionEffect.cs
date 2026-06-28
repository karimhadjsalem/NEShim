namespace NEShim.Rendering.MotionEffects;

internal sealed class ScanlineBobMotionEffect : IMotionEffect
{
    // Clip-space amplitude approximating half a NES output scanline at 1080p.
    // At 1080p with a ~75% height NES area: 1 NES line ≈ 810/240 ≈ 3.4 px → half ≈ 0.003 clip units.
    private const float BobAmplitude = 0.003f;

    public VideoMotionEffectMode EffectMode => VideoMotionEffectMode.ScanlineBob;

    public (float Dx, float Dy) GetFrameOffset(long frameCount)
    {
        float dy = (frameCount % 2 == 0) ? BobAmplitude : -BobAmplitude;
        return (0f, dy);
    }
}
