using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NEShim.Platform;

namespace NEShim.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly JsonSerializerOptions _userOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Single source of truth for InputMappings' default entries — read once from a fresh
    // AppConfig() rather than duplicating the literal binding table here. Used by
    // BackfillMissingInputMappingDefaults below.
    private static readonly Dictionary<string, InputBinding> _defaultInputMappings = new AppConfig().InputMappings;

    private static string PublisherConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>
    /// Single-game scheme (ctx null, unchanged): %APPDATA%\&lt;windowTitle&gt;\user.json.
    /// Multi-game scheme (ctx set): %APPDATA%\NEShim\Games\&lt;gameId&gt;\user.json — keyed by the
    /// stable GameId rather than the mutable/dual-purpose WindowTitle (which is also literally
    /// the OS window title text). The two schemes cannot collide: one is a file directly under
    /// %APPDATA%\&lt;windowTitle&gt;\, the other is nested under %APPDATA%\NEShim\Games\.
    /// </summary>
    private static string BuildUserConfigPath(string windowTitle, GameContext? ctx = null) =>
        ctx is null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                windowTitle,
                "user.json")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NEShim", "Games", ctx.GameId,
                "user.json");

    // ── Public API ────────────────────────────────────────────────────────────

    public static AppConfig Load(GameContext? ctx = null)
    {
        string publisherPath = ctx is null ? PublisherConfigPath : Path.Combine(ctx.RootDirectory, "config.json");
        var config = LoadFrom(publisherPath);
        ApplyUserConfig(config, BuildUserConfigPath(config.WindowTitle, ctx));
        return config;
    }

    public static void Save(AppConfig config, GameContext? ctx = null) =>
        SaveUserTo(config, BuildUserConfigPath(config.WindowTitle, ctx));

    // ── Internal (integration tests) ─────────────────────────────────────────

    /// <summary>Loads the publisher config file only, without overlaying user.json.</summary>
    internal static AppConfig LoadFrom(string publisherConfigPath)
    {
        if (!File.Exists(publisherConfigPath))
        {
            Logger.Log($"[Config] config.json not found — writing defaults to {publisherConfigPath}");
            var defaults = new AppConfig();
            if (PlatformDetector.IsSteamDeck)
                defaults.AudioFilter = "Saturation";
            SaveTo(defaults, publisherConfigPath);
            return defaults;
        }

        TryParseFrom(publisherConfigPath, out var config);
        return config;
    }

    /// <summary>
    /// Parses an existing config.json, without the missing-file bootstrap branch <see
    /// cref="LoadFrom"/> has. Returns false (with <paramref name="config"/> set to defaults)
    /// when the file exists but fails to parse, so callers like <see cref="GameScanner"/> can
    /// distinguish "malformed" from "successfully loaded" rather than silently treating a
    /// corrupt file as a validly-configured game.
    /// </summary>
    internal static bool TryParseFrom(string publisherConfigPath, out AppConfig config)
    {
        try
        {
            string json = File.ReadAllText(publisherConfigPath);
            config = JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
            MigrateDeprecatedFields(config);
            Logger.Log($"[Config] Loaded from {publisherConfigPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Config] Parse error — using defaults: {ex.Message}");
            config = new AppConfig();
            return false;
        }
    }

    /// <summary>Writes all AppConfig fields to the publisher config path.</summary>
    internal static void SaveTo(AppConfig config, string publisherConfigPath)
    {
        string json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(publisherConfigPath, json);
        Logger.Log($"[Config] Saved to {publisherConfigPath}");
    }

    /// <summary>Loads publisher config then overlays user config — testable two-file path.</summary>
    internal static AppConfig Load(string publisherConfigPath, string userConfigPath)
    {
        var config = LoadFrom(publisherConfigPath);
        ApplyUserConfig(config, userConfigPath);
        return config;
    }

    /// <summary>Writes user-settable fields only to the given path — testable user-save path.</summary>
    internal static void SaveUserTo(AppConfig config, string userConfigPath)
    {
        var userConfig = UserConfig.FromConfig(config);
        string? userConfigDir = Path.GetDirectoryName(userConfigPath);
        if (!string.IsNullOrEmpty(userConfigDir)) Directory.CreateDirectory(userConfigDir);
        string json = JsonSerializer.Serialize(userConfig, _userOptions);
        File.WriteAllText(userConfigPath, json);
        Logger.Log($"[Config] User config saved to {userConfigPath}");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void ApplyUserConfig(AppConfig config, string userConfigPath)
    {
        if (!File.Exists(userConfigPath))
        {
            Logger.Log($"[Config] No user.json found — bootstrapping from config.json at {userConfigPath}");
            SaveUserTo(config, userConfigPath);
            return;
        }

        try
        {
            string json    = File.ReadAllText(userConfigPath);
            var userConfig = JsonSerializer.Deserialize<UserConfig>(json, _userOptions);
            if (userConfig is null) return;
            userConfig.ApplyTo(config);
            MigrateDeprecatedFields(config);
            Logger.Log($"[Config] User config applied from {userConfigPath}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[Config] User config parse error — ignoring: {ex.Message}");
        }
    }

    private static void MigrateDeprecatedFields(AppConfig config)
    {
        // Publisher-only, never present in user.json — clamping here (called after both the
        // publisher-JSON parse and any user-JSON overlay) is a no-op safety net on the user path,
        // not redundant work, and guarantees a hand-edited user.json can never smuggle an
        // out-of-range value in.
        config.PlayerCount = Math.Clamp(config.PlayerCount, 1, 4);

        if (config.SoundScrubberEnabled && config.AudioFilter == "Default")
            config.AudioFilter = "Warm";

        if (config.GraphicsSmoothingEnabled && config.VideoFilter == "NearestNeighbour")
            config.VideoFilter = "Bilinear";

        if (config.VideoFilter == "NearestNeighbour")
            config.VideoFilter = "PixelPerfect";

        config.OverscanMode = config.OverscanMode switch
        {
            "None"           => "Normal",
            "NTSC" or "Auto" => "Overscan",
            _                => config.OverscanMode,
        };

        // Renamed from "ToggleWindow" to "ToggleWindowCarousel" so gamepad Y (also the default
        // P1 Start button) only toggles fullscreen while the carousel is showing, not during
        // gameplay — see AppConfig.GamepadHotkeyMappings's doc comment. GamepadHotkeyMappings is
        // overlaid from user.json as a whole-dictionary replace (UserConfig.ApplyTo), so any
        // user.json bootstrapped before this rename still carries the old key and would silently
        // keep the pre-fix behavior forever otherwise — a stale "ToggleWindow" entry is never
        // read by anything anymore, but a missing "ToggleWindowCarousel" entry is.
        if (config.GamepadHotkeyMappings.Remove("ToggleWindow")
            && !config.GamepadHotkeyMappings.ContainsKey("ToggleWindowCarousel"))
        {
            config.GamepadHotkeyMappings["ToggleWindowCarousel"] = "Y";
        }

        BackfillMissingInputMappingDefaults(config);
    }

    /// <summary>
    /// InputMappings is overlaid from config.json/user.json as a whole-dictionary replace, exactly
    /// like GamepadHotkeyMappings above — deserializing a JSON "inputMappings" object replaces the
    /// entire C#-initialized dictionary rather than merging into it, even when the JSON only
    /// defines a subset of keys. Any config.json or user.json saved before the P2-P4 multiplayer
    /// defaults existed (i.e. every file saved by a pre-multiplayer NEShim build) therefore
    /// permanently carries only the original "P1 …" keys once loaded — turning on <c>PlayerCount</c>
    /// on an existing install left every extra player's gamepad bindings silently blank instead of
    /// falling back to AppConfig's defaults, since those defaults never survive the JSON round trip.
    /// Backfills any default entry missing from the dictionary after either overlay, so upgrading
    /// an existing single-player install to local multiplayer picks up sensible starting bindings
    /// for players 2-4 the same way a genuinely fresh install does.
    /// </summary>
    private static void BackfillMissingInputMappingDefaults(AppConfig config)
    {
        foreach (var (key, defaultBinding) in _defaultInputMappings)
        {
            if (config.InputMappings.ContainsKey(key)) continue;
            config.InputMappings[key] = new InputBinding(defaultBinding.Key, defaultBinding.GamepadButton)
            {
                GamepadButton2 = defaultBinding.GamepadButton2,
            };
        }
    }
}
