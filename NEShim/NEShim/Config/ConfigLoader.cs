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
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, _options) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        string json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(ConfigPath, json);
    }
}
