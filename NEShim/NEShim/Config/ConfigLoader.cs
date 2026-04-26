using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NEShim.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load() => LoadFrom(ConfigPath);
    public static void Save(AppConfig config) => SaveTo(config, ConfigPath);

    internal static AppConfig LoadFrom(string configPath)
    {
        if (!File.Exists(configPath))
        {
            Logger.Log($"[Config] config.json not found — writing defaults to {configPath}");
            var defaults = new AppConfig();
            SaveTo(defaults, configPath);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            var config  = JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
            Logger.Log($"[Config] Loaded from {configPath}");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Config] Parse error — using defaults: {ex.Message}");
            return new AppConfig();
        }
    }

    internal static void SaveTo(AppConfig config, string configPath)
    {
        string json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(configPath, json);
        Logger.Log($"[Config] Saved to {configPath}");
    }
}
