namespace NEShim.Rendering.MotionEffects;

internal static class MotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.CrtJitter => new CrtJitterMotionEffect(),
        _                               => new NoneMotionEffect(),
    };
}
