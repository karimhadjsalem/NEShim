namespace NEShim.Rendering;

internal sealed record VideoPreset(
    string                Name,
    VideoFilterMode       Filter,
    VideoFilterMode?      Overlay,
    VideoColorFilterMode  ColorFilter,
    VideoMotionEffectMode MotionEffect,
    OverscanMode          Overscan,
    int                   Brightness,
    int                   Contrast,
    int                   Saturation);

internal static class VideoPresetRegistry
{
    public static readonly VideoPreset LivingRoom = new(
        Name:        "LivingRoom",
        Filter:      VideoFilterMode.CrtScreen,
        Overlay:     VideoFilterMode.CrtScanlines,
        ColorFilter: VideoColorFilterMode.NesColorCorrection,
        MotionEffect: VideoMotionEffectMode.CrtJitter,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0);

    public static readonly VideoPreset Arcade = new(
        Name:        "Arcade",
        Filter:      VideoFilterMode.CrtPhosphor,
        Overlay:     null,
        ColorFilter: VideoColorFilterMode.Cool,
        MotionEffect: VideoMotionEffectMode.CrtJitter,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0);

    public static readonly VideoPreset Sharp = new(
        Name:        "Sharp",
        Filter:      VideoFilterMode.Xbr,
        Overlay:     null,
        ColorFilter: VideoColorFilterMode.NesColorCorrection,
        MotionEffect: VideoMotionEffectMode.None,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0);

    public static readonly VideoPreset Phosphor = new(
        Name:        "Phosphor",
        Filter:      VideoFilterMode.CrtScreen,
        Overlay:     VideoFilterMode.CrtPhosphor,
        ColorFilter: VideoColorFilterMode.PhosphorAmber,
        MotionEffect: VideoMotionEffectMode.PhosphorPersistence,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0);

    public static readonly VideoPreset[] All = [LivingRoom, Arcade, Sharp, Phosphor];
}
