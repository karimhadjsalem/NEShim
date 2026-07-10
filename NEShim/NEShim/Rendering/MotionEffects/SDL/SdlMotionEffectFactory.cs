namespace NEShim.Rendering.MotionEffects;

internal static class SdlMotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode)
    {
        if (mode == VideoMotionEffectMode.PhosphorPersistence)
        {
            Logger.Log("[SdlMotionEffectFactory] PhosphorPersistence requires two texture samplers " +
                       "and cannot be expressed via SDL_GPURenderState — demoting to None.");
            return new NoneMotionEffect();
        }

        return mode switch
        {
            VideoMotionEffectMode.CrtJitter          => new CrtJitterMotionEffect(),
            VideoMotionEffectMode.ScanlineBob        => new ScanlineBobMotionEffect(),
            VideoMotionEffectMode.MagneticDistortion => new MagneticDistortionSdlMotionEffect(),
            _                                        => new NoneMotionEffect(),
        };
    }
}
