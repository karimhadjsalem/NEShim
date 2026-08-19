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
    int                   Saturation,
    int                   Hue);

internal static class VideoPresetRegistry
{
    // Resets every video setting to its engine default: sharp integer scaling, no overlay,
    // no color grade, no motion effect. Distinct from "No Preset" (VideoPreset == "None"),
    // which leaves whatever settings are currently active untouched.
    public static readonly VideoPreset NoFilters = new(
        Name:        "NoFilters",
        Filter:      VideoFilterMode.PixelPerfect,
        Overlay:     null,
        ColorFilter: VideoColorFilterMode.None,
        MotionEffect: VideoMotionEffectMode.None,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0, Hue: 0);

    public static readonly VideoPreset LivingRoom = new(
        Name:        "LivingRoom",
        Filter:      VideoFilterMode.CrtScreen,
        Overlay:     VideoFilterMode.CrtScanlines,
        ColorFilter: VideoColorFilterMode.NesColorCorrection,
        MotionEffect: VideoMotionEffectMode.CrtJitter,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0, Hue: 0);

    public static readonly VideoPreset Arcade = new(
        Name:        "Arcade",
        Filter:      VideoFilterMode.CrtPhosphor,
        Overlay:     null,
        ColorFilter: VideoColorFilterMode.Cool,
        MotionEffect: VideoMotionEffectMode.CrtJitter,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0, Hue: 0);

    public static readonly VideoPreset Sharp = new(
        Name:        "Sharp",
        Filter:      VideoFilterMode.Xbr,
        Overlay:     null,
        ColorFilter: VideoColorFilterMode.NesColorCorrection,
        MotionEffect: VideoMotionEffectMode.None,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0, Hue: 0);

    public static readonly VideoPreset Phosphor = new(
        Name:        "Phosphor",
        Filter:      VideoFilterMode.CrtScreen,
        Overlay:     VideoFilterMode.CrtPhosphor,
        ColorFilter: VideoColorFilterMode.PhosphorAmber,
        MotionEffect: VideoMotionEffectMode.PhosphorPersistence,
        Overscan:    OverscanMode.Normal,
        Brightness: 0, Contrast: 0, Saturation: 0, Hue: 0);

    public static readonly VideoPreset[] All = [NoFilters, LivingRoom, Arcade, Sharp, Phosphor];
}
