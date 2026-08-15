using NEShim.Audio;
using NEShim.Rendering;

namespace NEShim;

/// <summary>
/// The subset of <see cref="UI.InGameMenu"/>'s and <see cref="UI.MainMenuScreen"/>'s constructor
/// callbacks whose bodies are identical between the two menus — both apply the same setting to
/// the same engine subsystem (renderer/audio/config) regardless of which menu triggered the
/// change. Built once by <see cref="NEShimApp"/> (the only constructor) and handed to both menu
/// constructors, so there is exactly one copy of each callback body instead of two independently
/// maintained ones.
/// </summary>
internal sealed record MenuConfigCallbacks(
    Action<bool>                 OnWindowModeToggle,
    Action                       OnConfigSaved,
    Action<int>                  OnVolumeChanged,
    Action<AudioFilterMode>      OnFilterChanged,
    Action<VideoFilterMode>      OnVideoFilterChanged,
    Action<VideoFilterMode?>     OnVideoFilterOverlayChanged,
    Action<VideoColorFilterMode> OnVideoColorFilterChanged,
    Action<VideoMotionEffectMode> OnVideoMotionEffectChanged,
    Action<OverscanMode>         OnOverscanModeChanged,
    Action<string>               OnLanguageChanged,
    Action<int, int, int, int>   OnPictureAdjustChanged,
    Action<int, int, int>        OnAudioEqChanged);
