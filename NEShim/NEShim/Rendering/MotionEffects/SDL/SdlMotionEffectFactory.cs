namespace NEShim.Rendering.MotionEffects;

internal static class SdlMotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.None                => new NoneMotionEffect(),
        VideoMotionEffectMode.CrtJitter            => new CrtJitterMotionEffect(),
        VideoMotionEffectMode.ScanlineBob          => new ScanlineBobMotionEffect(),
        VideoMotionEffectMode.MagneticDistortion   => new MagneticDistortionSdlMotionEffect(),
        VideoMotionEffectMode.PhosphorPersistence  => new PhosphorPersistenceSdlMotionEffect(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unmapped VideoMotionEffectMode — add a case to SdlMotionEffectFactory.Create."),
    };
}
