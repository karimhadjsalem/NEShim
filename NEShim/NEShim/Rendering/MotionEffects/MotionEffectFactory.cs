namespace NEShim.Rendering.MotionEffects;

internal static class MotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.None                => new NoneMotionEffect(),
        VideoMotionEffectMode.CrtJitter            => new CrtJitterMotionEffect(),
        VideoMotionEffectMode.ScanlineBob          => new ScanlineBobMotionEffect(),
        VideoMotionEffectMode.MagneticDistortion   => new MagneticDistortionMotionEffect(),
        VideoMotionEffectMode.PhosphorPersistence  => new PhosphorPersistenceMotionEffect(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped VideoMotionEffectMode — add a case to MotionEffectFactory.Create."),
    };
}
