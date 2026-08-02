using System.Collections.Generic;

namespace NEShim.Config;

internal sealed class UserConfig
{
    // ── Window & display ──────────────────────────────────────────────────────
    public string? WindowMode          { get; set; }
    public string? MainMenuPosition    { get; set; }
    public bool?   ShowFps             { get; set; }
    public string? VideoFilter         { get; set; }
    public string? OverscanMode        { get; set; }
    public string? VideoMotionEffect   { get; set; }
    public string? VideoFilterOverlay  { get; set; }
    public string? VideoColorFilter    { get; set; }
    public int?    VideoBrightness     { get; set; }
    public int?    VideoContrast       { get; set; }
    public int?    VideoSaturation     { get; set; }
    public int?    VideoHue            { get; set; }
    public string? VideoPreset         { get; set; }

    // ── Audio ─────────────────────────────────────────────────────────────────
    public string? AudioDevice         { get; set; }
    public int?    Volume              { get; set; }
    public string? AudioFilter         { get; set; }
    public int?    AudioEqBass         { get; set; }
    public int?    AudioEqMid          { get; set; }
    public int?    AudioEqTreble       { get; set; }
    public bool?   MainMenuMusicEnabled { get; set; }
    public int?    MainMenuMusicVolume { get; set; }

    // ── Input ─────────────────────────────────────────────────────────────────
    public int?    GamepadDeadzone     { get; set; }
    public int?    ActiveSlot          { get; set; }
    public Dictionary<string, InputBinding>? InputMappings         { get; set; }
    public Dictionary<string, string>?       GamepadHotkeyMappings { get; set; }
    public Dictionary<string, string>?       HotkeyMappings        { get; set; }
    public string? AnalogStickMode     { get; set; }
    public bool?   GamepadDpadStickInterchangeable { get; set; }

    // ── Localization ──────────────────────────────────────────────────────────
    public string? Language            { get; set; }

    // Deprecated fields — kept for reading old user.json files; never written by FromConfig
    public bool?   GraphicsSmoothingEnabled { get; set; }
    public bool?   SoundScrubberEnabled     { get; set; }

    public void ApplyTo(AppConfig config)
    {
        if (WindowMode               is not null) config.WindowMode               = WindowMode;
        if (MainMenuPosition         is not null) config.MainMenuPosition         = MainMenuPosition;
        if (ShowFps                  is not null) config.ShowFps                  = ShowFps.Value;
        if (VideoFilter              is not null) config.VideoFilter              = VideoFilter;
        if (OverscanMode             is not null) config.OverscanMode             = OverscanMode;
        if (VideoMotionEffect        is not null) config.VideoMotionEffect        = VideoMotionEffect;
        if (VideoFilterOverlay       is not null) config.VideoFilterOverlay       = VideoFilterOverlay;
        if (VideoColorFilter         is not null) config.VideoColorFilter         = VideoColorFilter;
        if (VideoBrightness          is not null) config.VideoBrightness          = VideoBrightness.Value;
        if (VideoContrast            is not null) config.VideoContrast            = VideoContrast.Value;
        if (VideoSaturation          is not null) config.VideoSaturation          = VideoSaturation.Value;
        if (VideoHue                 is not null) config.VideoHue                 = VideoHue.Value;
        if (VideoPreset              is not null) config.VideoPreset              = VideoPreset;
        if (AudioDevice              is not null) config.AudioDevice              = AudioDevice;
        if (Volume                   is not null) config.Volume                   = Volume.Value;
        if (AudioFilter              is not null) config.AudioFilter              = AudioFilter;
        if (AudioEqBass              is not null) config.AudioEqBass              = AudioEqBass.Value;
        if (AudioEqMid               is not null) config.AudioEqMid               = AudioEqMid.Value;
        if (AudioEqTreble            is not null) config.AudioEqTreble            = AudioEqTreble.Value;
        if (MainMenuMusicEnabled     is not null) config.MainMenuMusicEnabled     = MainMenuMusicEnabled.Value;
        if (MainMenuMusicVolume      is not null) config.MainMenuMusicVolume      = MainMenuMusicVolume.Value;
        if (GamepadDeadzone          is not null) config.GamepadDeadzone          = GamepadDeadzone.Value;
        if (ActiveSlot               is not null) config.ActiveSlot               = ActiveSlot.Value;
        if (InputMappings            is not null) config.InputMappings            = InputMappings;
        if (GamepadHotkeyMappings    is not null) config.GamepadHotkeyMappings    = GamepadHotkeyMappings;
        if (HotkeyMappings           is not null) config.HotkeyMappings           = HotkeyMappings;
        if (AnalogStickMode          is not null) config.AnalogStickMode          = AnalogStickMode;
        if (GamepadDpadStickInterchangeable is not null) config.GamepadDpadStickInterchangeable = GamepadDpadStickInterchangeable.Value;
        if (Language                 is not null) config.Language                 = Language;
        if (GraphicsSmoothingEnabled is not null) config.GraphicsSmoothingEnabled = GraphicsSmoothingEnabled.Value;
        if (SoundScrubberEnabled     is not null) config.SoundScrubberEnabled     = SoundScrubberEnabled.Value;
    }

    public static UserConfig FromConfig(AppConfig config) => new()
    {
        WindowMode            = config.WindowMode,
        MainMenuPosition      = config.MainMenuPosition,
        ShowFps               = config.ShowFps,
        VideoFilter           = config.VideoFilter,
        OverscanMode          = config.OverscanMode,
        VideoMotionEffect     = config.VideoMotionEffect,
        VideoFilterOverlay    = config.VideoFilterOverlay,
        VideoColorFilter      = config.VideoColorFilter,
        VideoBrightness       = config.VideoBrightness,
        VideoContrast         = config.VideoContrast,
        VideoSaturation       = config.VideoSaturation,
        VideoHue              = config.VideoHue,
        VideoPreset           = config.VideoPreset,
        AudioDevice           = config.AudioDevice,
        Volume                = config.Volume,
        AudioFilter           = config.AudioFilter,
        AudioEqBass           = config.AudioEqBass,
        AudioEqMid            = config.AudioEqMid,
        AudioEqTreble         = config.AudioEqTreble,
        MainMenuMusicEnabled  = config.MainMenuMusicEnabled,
        MainMenuMusicVolume   = config.MainMenuMusicVolume,
        GamepadDeadzone       = config.GamepadDeadzone,
        ActiveSlot            = config.ActiveSlot,
        InputMappings         = config.InputMappings,
        GamepadHotkeyMappings = config.GamepadHotkeyMappings,
        HotkeyMappings        = config.HotkeyMappings,
        AnalogStickMode       = config.AnalogStickMode,
        GamepadDpadStickInterchangeable = config.GamepadDpadStickInterchangeable,
        Language              = config.Language,
    };
}
