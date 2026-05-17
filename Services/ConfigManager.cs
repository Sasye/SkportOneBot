using System;
using System.IO;
using System.Text.Json;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public class ConfigManager
{
    private readonly string _configPath;
    public AppConfig Config { get; private set; } = new AppConfig();

    public ConfigManager(string configPath = "config.json")
    {
        _configPath = configPath;
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                Console.WriteLine("未找到 config.json，将生成默认配置。");
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载 config.json 失败: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
