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

    private static string PublisherConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    private static string BuildUserConfigPath(string windowTitle) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            windowTitle,
            "user.json");

    // ── Public API ────────────────────────────────────────────────────────────

    public static AppConfig Load()
    {
        var config = LoadFrom(PublisherConfigPath);
        ApplyUserConfig(config, BuildUserConfigPath(config.WindowTitle));
        return config;
    }

    public static void Save(AppConfig config) =>
        SaveUserTo(config, BuildUserConfigPath(config.WindowTitle));

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

        try
        {
            string json = File.ReadAllText(publisherConfigPath);
            var config  = JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
            MigrateDeprecatedFields(config);
            Logger.Log($"[Config] Loaded from {publisherConfigPath}");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Config] Parse error — using defaults: {ex.Message}");
            return new AppConfig();
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
        Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath)!);
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
    }
}
