using System.Collections.Generic;
using NEShim.Audio;
using NEShim.Config;
using NEShim.Input;
using NEShim.Localization;
using NEShim.Rendering;
using NEShim.Steam;

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
    /// Parses the 1-based player number off the leading "P{n} " token of a binding config key
    /// (e.g. "P2 Up" → 2). Keys with no player prefix ("OpenMenu", "", or null) default to
    /// player 1, matching the P1-only scope of hotkeys/menu-nav/glyph-cache-invalidation
    /// elsewhere in the input stack. Used to route a binding row's glyph lookup and gamepad
    /// rebind-capture to the correct player's device without threading a separate player
    /// parameter through every call site — the config key already carries that information.
    /// </summary>
    public static int PlayerFromConfigKey(string? configKey)
    {
        if (configKey != null && configKey.Length >= 2 && configKey[0] == 'P' && char.IsDigit(configKey[1]))
            return configKey[1] - '0';
        return 1;
    }

    /// <summary>1-based player a binding Screen belongs to (defaults to 1 for every non-binding
    /// screen too, harmlessly — callers only consult this while <c>ShowsControllerDiagram</c> is
    /// true, which is only ever true for the binding screens listed here).</summary>
    public static int PlayerForBindingScreen(Screen screen) => screen switch
    {
        Screen.GamepadBindingsP2 or Screen.KeyboardBindingsP2 => 2,
        Screen.GamepadBindingsP3 or Screen.KeyboardBindingsP3 => 3,
        Screen.GamepadBindingsP4 or Screen.KeyboardBindingsP4 => 4,
        _ => 1,
    };

    /// <summary>The controller-diagram column's header label — "NES Controller" for player 1
    /// (unchanged), "Player {n} NES Controller" for players 2-4 so the diagram is unambiguous
    /// about whose bindings/physical gamepad it reflects.</summary>
    public static string ControllerDiagramLabel(LocalizationData localization, int player) =>
        player == 1
            ? localization.NesControllerLabel
            : $"{string.Format(localization.PlayerLabel, player)} {localization.NesControllerLabel}";

    private static readonly string[] NesButtonSuffixes =
        { "Up", "Down", "Left", "Right", "A", "B", "Start", "Select" };

    /// <summary>
    /// True when <paramref name="key"/> is a real per-player NES button config key
    /// ("P{n} &lt;Suffix&gt;", any player 1-9) rather than a placeholder row ("", "OpenMenu") or
    /// an unrelated config key. Player-agnostic generalization of what used to be a literal
    /// 8-way "P1 Up" or "P1 Down" or ... switch, needed once binding screens exist for more than
    /// one player.
    /// </summary>
    public static bool IsNesButtonKey(string key)
    {
        int space = key.IndexOf(' ');
        if (space < 2 || key[0] != 'P' || !char.IsDigit(key[1])) return false;
        return Array.IndexOf(NesButtonSuffixes, key[(space + 1)..]) >= 0;
    }

    /// <summary>
    /// Returns the standard NES-button binding action table for <paramref name="player"/>
    /// (1-based, defaults to player 1), localised via <paramref name="localization"/>. The last
    /// entry has an empty ConfigKey and represents the Back row.
    /// </summary>
    public static (string Label, string ConfigKey)[] BuildBindingActions(LocalizationData localization, int player = 1) =>
        new (string, string)[]
        {
            (localization.BindUp,     $"P{player} Up"),
            (localization.BindDown,   $"P{player} Down"),
            (localization.BindLeft,   $"P{player} Left"),
            (localization.BindRight,  $"P{player} Right"),
            (localization.BindA,      $"P{player} A"),
            (localization.BindB,      $"P{player} B"),
            (localization.BindStart,  $"P{player} Start"),
            (localization.BindSelect, $"P{player} Select"),
            (localization.Back,       ""),
        };

    /// <summary>
    /// Returns the gamepad binding table for <paramref name="player"/> (1-based, defaults to
    /// player 1). When <paramref name="player"/> is 1 and <c>OverrideStartBindingProtection</c>
    /// is enabled, the table appends an OpenMenu row before the final Back row — OpenMenu is
    /// deliberately never offered on a non-player-1 screen: it is a session-level hotkey that
    /// only ever reads player 1's gamepad (see <c>InputManager</c>'s hotkey-scope doc comment),
    /// so showing it on another player's screen would be misleading.
    /// </summary>
    public static (string Label, string ConfigKey)[] BuildGamepadBindingActions(
        LocalizationData localization, AppConfig config, int player = 1)
    {
        var baseRows = BuildBindingActions(localization, player);
        return player == 1 && config.OverrideStartBindingProtection
            ? baseRows[..^1]
                .Append((localization.BindOpenMenu, "OpenMenu"))
                .Append((localization.Back, ""))
                .ToArray()
            : baseRows;
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
    /// <paramref name="config"/> and clears the same button from any other action <em>belonging
    /// to the same player</em> to prevent duplicate gamepad bindings. Scoped to <paramref
    /// name="action"/>'s own "P{n} " prefix — required, not cosmetic: different players have
    /// different physical gamepads, so the same raw identifier string (e.g. "DPadUp") legitimately
    /// binds the same NES direction for two different players at once; clearing across players
    /// would silently wipe another player's identical default binding.
    /// </summary>
    public static void SetGamepadBinding(AppConfig config, string action, string buttonName)
    {
        int    space  = action.IndexOf(' ');
        string prefix = space > 0 ? action[..(space + 1)] : "";

        foreach (var kvp in config.InputMappings)
        {
            if (kvp.Key == action) continue;
            if (prefix.Length > 0 && !kvp.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;

            if (kvp.Value.GamepadButton  == buttonName) kvp.Value.GamepadButton  = null;
            if (kvp.Value.GamepadButton2 == buttonName) kvp.Value.GamepadButton2 = null;
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

    // ── Binding row labels/glyphs ────────────────────────────────────────────────
    // Shared by InGameMenu and MainMenuScreen — previously each menu hand-wrote its own copy
    // (byte-identical) of all three of these.

    public static string KeyboardLabel(string configKey, AppConfig config, LocalizationData localization)
        => config.InputMappings.TryGetValue(configKey, out var b) ? b.Key ?? localization.BindNone : localization.BindNone;

    public static string GetGamepadLabel(string configKey, AppConfig config, LocalizationData localization)
    {
        if (configKey == "OpenMenu")
            return LocalizeGamepadButton(
                config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder"), localization);

        int player          = PlayerFromConfigKey(configKey);
        int controllerIndex = player - 1;
        if (SteamInputManager.IsUsingNativeActions(controllerIndex)
            && SteamInputManager.ActionFor(configKey) is { } actionName)
            return SteamInputManager.GetNativeLabel(actionName, controllerIndex);

        return LocalizeGamepadButton(
            config.InputMappings.TryGetValue(configKey, out var b) ? b.GamepadButton : null, localization);
    }

    /// <summary>Glyph for the binding row's value column — see <see cref="GetGamepadLabel"/> for the
    /// parallel text-label logic. Native Steam Input mode (genuine trackpad/gyro usage) shows text
    /// only, matching its existing display; the glyph chain only applies to the SDL path.</summary>
    public static IntPtr GetGamepadGlyph(string configKey, AppConfig config, LocalizationData localization, IGamepadGlyphResolver glyphResolver)
    {
        if (configKey == "OpenMenu")
            return glyphResolver.Resolve(
                config.GamepadHotkeyMappings.GetValueOrDefault("OpenMenu", "LeftShoulder"), localization).Glyph;

        int player          = PlayerFromConfigKey(configKey);
        int controllerIndex = player - 1;
        if (SteamInputManager.IsUsingNativeActions(controllerIndex) && SteamInputManager.ActionFor(configKey) != null)
            return IntPtr.Zero;

        return glyphResolver.Resolve(
            config.InputMappings.TryGetValue(configKey, out var b) ? b.GamepadButton : null, localization).Glyph;
    }

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
