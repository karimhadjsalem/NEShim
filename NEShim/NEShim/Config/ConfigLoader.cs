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

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Logger.Log($"[Config] config.json not found — writing defaults to {ConfigPath}");
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            var config  = JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
            ApplySteamActionDefaults(config);
            Logger.Log($"[Config] Loaded from {ConfigPath}");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Log($"[Config] Parse error — using defaults: {ex.Message}");
            return new AppConfig();
        }
    }

    private static void ApplySteamActionDefaults(AppConfig config)
    {
        var defaults = new AppConfig().InputMappings;
        foreach (var (key, defaultBinding) in defaults)
        {
            if (config.InputMappings.TryGetValue(key, out var existing)
                && existing.SteamAction is null
                && defaultBinding.SteamAction is not null)
            {
                existing.SteamAction = defaultBinding.SteamAction;
            }
        }
    }

    public static void Save(AppConfig config)
    {
        string json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(ConfigPath, json);
        Logger.Log($"[Config] Saved to {ConfigPath}");
    }
}
