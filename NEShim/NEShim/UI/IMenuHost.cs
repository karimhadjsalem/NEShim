using NEShim.Audio;
using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;

namespace NEShim.UI;

/// <summary>
/// What <see cref="SharedScreenHandler"/>-derived handlers need from whichever menu owns them —
/// <see cref="InGameMenu"/> and <see cref="MainMenuScreen"/> both implement this explicitly
/// (<c>AppConfig IMenuHost.Config => _config;</c> etc.), so implementing it adds zero public
/// surface to either class beyond what already existed. Deliberately narrow: only exposes what the
/// ~11 handlers whose logic is identical between the two menus actually touch — screen-specific
/// handlers (RootHandler/MainHandler, ConfirmHandler, SoundHandler, ...) stay nested against the
/// concrete menu type and keep using its full private surface directly, unaffected by this interface.
/// </summary>
internal interface IMenuHost
{
    AppConfig Config { get; }
    LocalizationData Localization { get; }

    /// <summary>Where a screen's "Back" item lands when there's no more specific parent —
    /// <see cref="Screen.Root"/> for <see cref="InGameMenu"/>, <see cref="Screen.Main"/> for
    /// <see cref="MainMenuScreen"/>.</summary>
    Screen RootScreen { get; }

    (string Label, string ConfigKey)[] BindingActions { get; }
    (string Label, string ConfigKey)[] GamepadBindingActions { get; }
    string? RebindingAction { get; set; }
    string? GamepadRebindingAction { get; set; }

    void NavigateTo(Screen screen);
    void ClearPreset();
    void ApplyPreset(VideoPreset preset);
    void ResetPicture();
    void ResetEq();
    string  GetGamepadLabel(string configKey);
    IntPtr  GetGamepadGlyph(string configKey);
    string  KeyboardLabel(string configKey);

    // Config-change callbacks whose bodies are already deduplicated via MenuConfigCallbacks (#28) —
    // exposed individually here (rather than as one Callbacks property) purely because that's the
    // shape the migrated handlers already call them in; no behavior difference either way.
    Action<bool>                 OnWindowModeToggle { get; }
    Action                       OnConfigSaved { get; }
    Action<AudioFilterMode>      OnFilterChanged { get; }
    Action<VideoFilterMode>      OnVideoFilterChanged { get; }
    Action<VideoFilterMode?>     OnVideoFilterOverlayChanged { get; }
    Action<VideoColorFilterMode> OnVideoColorFilterChanged { get; }
    Action<VideoMotionEffectMode> OnVideoMotionEffectChanged { get; }
    Action<OverscanMode>         OnOverscanModeChanged { get; }
    Action<string>               OnLanguageChanged { get; }
}
