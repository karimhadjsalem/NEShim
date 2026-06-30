namespace NEShim.Rendering.MotionEffects;

internal static class MotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.CrtJitter          => new CrtJitterMotionEffect(),
        VideoMotionEffectMode.ScanlineBob        => new ScanlineBobMotionEffect(),
        VideoMotionEffectMode.MagneticDistortion => new MagneticDistortionMotionEffect(),
        _                                        => new NoneMotionEffect(),
    };
}
