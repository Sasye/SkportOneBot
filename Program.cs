using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkportOneBot.Models;
using SkportOneBot.Services;

namespace SkportOneBot;

class Program
{
    private static AppConfig _config = new();
    private static List<Profile> _profiles = new();
    private static readonly string _configPath = Path.Combine(Environment.CurrentDirectory, "config.json");
    private static readonly string _profilesPath = Path.Combine(Environment.CurrentDirectory, "profiles.json");

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Skport OneBot Auto-Sign (C# Version) ===");

        LoadConfig();
        LoadProfiles();

        var skportService = new SkportService();
        OneBotClient oneBotClient = null!;

        var webServer = new WebServer(_config.BindPort, skportService, profile =>
        {
            _profiles.Add(profile);
            SaveProfiles();
        });

        oneBotClient = new OneBotClient(_config, webServer, skportService, () => _profiles, profiles => 
        {
            _profiles = profiles;
            SaveProfiles();
        });

        var cronScheduler = new CronScheduler(_config, () => _profiles, oneBotClient);

        webServer.Start();
        Console.WriteLine($"[Web] Web服务已启动，监听端口: {_config.BindPort}");

        cronScheduler.Start();

        await oneBotClient.StartAsync();

        // Prevent application from exiting
        await Task.Delay(-1);
    }

    private static void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                Console.WriteLine("未找到 config.json，将使用默认配置并创建文件。");
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载 config.json 失败: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void LoadProfiles()
    {
        try
        {
            if (File.Exists(_profilesPath))
            {
                var json = File.ReadAllText(_profilesPath);
                _profiles = JsonSerializer.Deserialize<List<Profile>>(json) ?? new List<Profile>();
            }
            else
            {
                Console.WriteLine("未找到 profiles.json，将以空配置启动。");
                File.WriteAllText(_profilesPath, "[]");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载 profiles.json 失败: {ex.Message}");
        }
    }

    private static void SaveProfiles()
    {
        try
        {
            var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_profilesPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存 profiles.json 失败: {ex.Message}");
        }
    }
}
