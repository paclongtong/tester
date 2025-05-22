using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using friction_tester;

public static class ConfigManager
{
    private static readonly string ConfigPath = "config.json";

    public static AppConfig Config { get; private set; }

    public static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json);

                // Ensure Axes list is not empty
                if (Config.Axes == null || Config.Axes.Count == 0)
                {
                    Config.Axes = new List<AxisConfig> { new AxisConfig() };
                }

            }
            else
            {
                Config = new AppConfig { Axes = new List<AxisConfig> { new AxisConfig() } };
                SaveConfig(); // Save default config
            }
            //Config = new AppConfig { Axes = new List<AxisConfig> { new AxisConfig() } }; // Default config
        }
        catch (Exception e)
        {
            Logger.Log($"加载配置失败: {e.Message}");
            Config = new AppConfig();     // fall back to default configuration
        }

        //return Config;
    }

    public static void SaveConfig(AppConfig config)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception e)
        {
            Logger.Log($"保存配置失败: {e.Message}");
        }
    }

    public static void SaveConfig()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Config, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to save configuration: {e.Message}");
        }
    }

}

