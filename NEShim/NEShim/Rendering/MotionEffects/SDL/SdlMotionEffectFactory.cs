namespace NEShim.Rendering.MotionEffects;

internal static class SdlMotionEffectFactory
{
    public static IMotionEffect Create(VideoMotionEffectMode mode) => mode switch
    {
        VideoMotionEffectMode.CrtJitter           => new CrtJitterMotionEffect(),
        VideoMotionEffectMode.ScanlineBob         => new ScanlineBobMotionEffect(),
        VideoMotionEffectMode.MagneticDistortion  => new MagneticDistortionSdlMotionEffect(),
        // No SDL_GPU shader — SDL3HwRenderer detects NeedsTemporalBuffer and reproduces the
        // D3D11 shader's max(current, previous * decay) via blend compositing instead.
        VideoMotionEffectMode.PhosphorPersistence => new PhosphorPersistenceMotionEffect(),
        _                                         => new NoneMotionEffect(),
    };
}
