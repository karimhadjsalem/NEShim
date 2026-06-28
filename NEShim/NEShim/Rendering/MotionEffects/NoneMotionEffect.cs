namespace NEShim.Rendering.MotionEffects;

internal sealed class NoneMotionEffect : IMotionEffect
{
    public VideoMotionEffectMode EffectMode => VideoMotionEffectMode.None;

    public (float Dx, float Dy) GetFrameOffset(long frameCount) => (0f, 0f);
}
