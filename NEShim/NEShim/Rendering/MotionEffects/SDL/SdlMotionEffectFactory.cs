namespace NEShim.Rendering.MotionEffects;

internal static class SdlMotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.CrtJitter           => new CrtJitterMotionEffect(),
        VideoMotionEffectMode.ScanlineBob         => new ScanlineBobMotionEffect(),
        VideoMotionEffectMode.MagneticDistortion  => new MagneticDistortionSdlMotionEffect(),
        VideoMotionEffectMode.PhosphorPersistence => new PhosphorPersistenceSdlMotionEffect(),
        _                                         => new NoneMotionEffect(),
    };
}
