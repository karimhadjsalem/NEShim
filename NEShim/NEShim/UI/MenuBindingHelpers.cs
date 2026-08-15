using System.Collections.Generic;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;

namespace NEShim.UI;

/// <summary>
/// Static helpers shared by <see cref="InGameMenu"/> and <see cref="MainMenuScreen"/>
/// for building binding-action tables and applying key/button assignments to config.
/// </summary>
internal static class MenuBindingHelpers
{
    private static readonly HashSet<string> AnalogIdentifiers =
        new() { "AnalogUp", "AnalogDown", "AnalogLeft", "AnalogRight" };

    /// <summary>
    /// Returns the standard NES-button binding action table, localised via
    /// <paramref name="localization"/>. The last entry has an empty ConfigKey and
    /// represents the Back row.
    /// </summary>
    public static (string Label, string ConfigKey)[] BuildBindingActions(LocalizationData localization) =>
        new (string, string)[]
        {
            (localization.BindUp,     "P1 Up"),
            (localization.BindDown,   "P1 Down"),
            (localization.BindLeft,   "P1 Left"),
            (localization.BindRight,  "P1 Right"),
            (localization.BindA,      "P1 A"),
            (localization.BindB,      "P1 B"),
            (localization.BindStart,  "P1 Start"),
            (localization.BindSelect, "P1 Select"),
            (localization.Back,       ""),
        };

    /// <summary>
    /// Returns the gamepad binding table. When <c>OverrideStartBindingProtection</c> is
    /// enabled the table appends an OpenMenu row before the final Back row.
    /// </summary>
    public static (string Label, string ConfigKey)[] BuildGamepadBindingActions(
        LocalizationData localization,
        AppConfig config,
        (string Label, string ConfigKey)[] bindingActions)
    {
        return config.OverrideStartBindingProtection
            ? bindingActions[..^1]
                .Append((localization.BindOpenMenu, "OpenMenu"))
                .Append((localization.Back, ""))
                .ToArray()
            : bindingActions;
    }

    /// <summary>
    /// Assigns <paramref name="keyName"/> to <paramref name="action"/> in
    /// <paramref name="config"/> and clears the same key from any other action
    /// to prevent duplicate keyboard bindings.
    /// </summary>
    public static void SetBinding(AppConfig config, string action, string keyName)
    {
        foreach (var kvp in config.InputMappings)
        {
            if (kvp.Key != action && kvp.Value.Key == keyName)
                kvp.Value.Key = null;
        }

        if (config.InputMappings.TryGetValue(action, out var binding))
            binding.Key = keyName;
        else
            config.InputMappings[action] = new InputBinding(keyName, null);
    }

    /// <summary>
    /// Assigns <paramref name="buttonName"/> to <paramref name="action"/> in
    /// <paramref name="config"/> and clears the same button from any other action
    /// to prevent duplicate gamepad bindings.
    /// </summary>
    public static void SetGamepadBinding(AppConfig config, string action, string buttonName)
    {
        foreach (var kvp in config.InputMappings)
        {
            if (kvp.Key != action)
            {
                if (kvp.Value.GamepadButton  == buttonName) kvp.Value.GamepadButton  = null;
                if (kvp.Value.GamepadButton2 == buttonName) kvp.Value.GamepadButton2 = null;
            }
        }

        if (config.InputMappings.TryGetValue(action, out var binding))
        {
            binding.GamepadButton = buttonName;
            // If the user explicitly binds an analog identifier as the primary button,
            // clear the secondary slot on the same action. Without this, an action whose
            // default GamepadButton2 was "AnalogDown" would fire for both AnalogUp
            // (GamepadButton) and AnalogDown (GamepadButton2) simultaneously.
            if (AnalogIdentifiers.Contains(buttonName))
                binding.GamepadButton2 = null;
        }
        else
        {
            config.InputMappings[action] = new InputBinding(null, buttonName);
        }
    }

    /// <summary>
    /// Localizes a raw gamepad identifier for the Gamepad Bindings value column. Forwards to
    /// <see cref="NEShim.Input.GamepadButtonLocalizer"/>, which lives in the Input layer so
    /// <see cref="NEShim.Input.Sources.TextGlyphSource"/> can share the same logic without
    /// depending back into UI.
    /// </summary>
    public static string LocalizeGamepadButton(string? identifier, LocalizationData localization)
        => NEShim.Input.GamepadButtonLocalizer.Localize(identifier, localization);

    // ── Localized display names ─────────────────────────────────────────────────
    // Shared by InGameMenuHandlers/ and MainMenuHandlers/ — previously each directory hand-wrote
    // its own copy of every one of these switches (independently, per handler), so a filter/mode
    // display name could silently drift between the two menus. One copy each, here.

    public static string VideoFilterDisplayName(VideoFilterMode mode, LocalizationData localization) => mode switch
    {
        VideoFilterMode.Bilinear      => localization.VideoFilterSmooth,
        VideoFilterMode.PixelPerfect  => localization.VideoFilterPixelPerfect,
        VideoFilterMode.CrtScanlines  => localization.VideoFilterCrtScanlines,
        VideoFilterMode.CrtPhosphor   => localization.VideoFilterCrtPhosphor,
        VideoFilterMode.NtscComposite => localization.VideoFilterNtscComposite,
        VideoFilterMode.CrtScreen     => localization.VideoFilterCrtScreen,
        VideoFilterMode.Xbr           => localization.VideoFilterXbr,
        _                             => mode.ToString(),
    };

    /// <summary>The Video Overlay cycle item's display name — <c>null</c> means "no overlay".</summary>
    public static string VideoOverlayDisplayName(VideoFilterMode? mode, LocalizationData localization) => mode switch
    {
        null                         => localization.VideoColorFilterNone,
        VideoFilterMode.CrtScanlines => localization.VideoFilterCrtScanlines,
        VideoFilterMode.CrtPhosphor  => localization.VideoFilterCrtPhosphor,
        VideoFilterMode.CrtScreen    => localization.VideoFilterCrtScreen,
        _                            => mode.ToString()!,
    };

    public static string VideoMotionEffectDisplayName(VideoMotionEffectMode mode, LocalizationData localization) => mode switch
    {
        VideoMotionEffectMode.None                 => localization.VideoMotionEffectNone,
        VideoMotionEffectMode.CrtJitter             => localization.VideoMotionEffectCrtJitter,
        VideoMotionEffectMode.ScanlineBob           => localization.VideoMotionEffectScanlineBob,
        VideoMotionEffectMode.MagneticDistortion    => localization.VideoMotionEffectMagneticDistortion,
        VideoMotionEffectMode.PhosphorPersistence   => localization.VideoMotionEffectPhosphorPersistence,
        _                                           => mode.ToString(),
    };

    public static string VideoColorFilterDisplayName(VideoColorFilterMode mode, LocalizationData localization) => mode switch
    {
        VideoColorFilterMode.None               => localization.VideoColorFilterNone,
        VideoColorFilterMode.Warm               => localization.VideoColorFilterWarm,
        VideoColorFilterMode.Greyscale          => localization.VideoColorFilterGreyscale,
        VideoColorFilterMode.NesColorCorrection => localization.VideoColorFilterNesColors,
        VideoColorFilterMode.Cool               => localization.VideoColorFilterCool,
        VideoColorFilterMode.PhosphorAmber      => localization.VideoColorFilterPhosphorAmber,
        VideoColorFilterMode.PhosphorGreen      => localization.VideoColorFilterPhosphorGreen,
        _                                       => mode.ToString(),
    };

    public static string OverscanDisplayName(OverscanMode mode, LocalizationData localization) => mode switch
    {
        OverscanMode.Overscan  => localization.OverscanOverscan,
        OverscanMode.Normal    => localization.OverscanNormal,
        OverscanMode.Underscan => localization.OverscanUnderscan,
        _                      => mode.ToString(),
    };

    public static string AudioFilterDisplayName(AudioFilterMode mode, LocalizationData localization) => mode switch
    {
        AudioFilterMode.Default       => localization.AudioFilterDefault,
        AudioFilterMode.Warm          => localization.AudioFilterWarm,
        AudioFilterMode.PseudoStereo  => localization.AudioFilterPseudoStereo,
        AudioFilterMode.WarmStereo    => localization.AudioFilterWarmStereo,
        AudioFilterMode.Compression   => localization.AudioFilterCompression,
        AudioFilterMode.BassBoost     => localization.AudioFilterBassBoost,
        AudioFilterMode.Saturation    => localization.AudioFilterSaturation,
        AudioFilterMode.DmcStabilizer => localization.AudioFilterDmcStabilizer,
        _                             => mode.ToString(),
    };

    /// <summary>The active preset label shown on the Video screen — <paramref name="presetName"/>
    /// is <c>AppConfig.VideoPreset</c> ("None", "NoFilters", "LivingRoom", etc.).</summary>
    public static string VideoPresetDisplayName(string presetName, LocalizationData localization) => presetName switch
    {
        "NoFilters"  => localization.VideoPresetNoFilters,
        "LivingRoom" => localization.VideoPresetLivingRoom,
        "Arcade"     => localization.VideoPresetArcade,
        "Sharp"      => localization.VideoPresetSharp,
        "Phosphor"   => localization.VideoPresetPhosphor,
        _            => localization.VideoPresetNoPreset,
    };
}
