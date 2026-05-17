using System;
using System.Threading.Tasks;
using SkportOneBot.Commands;
using SkportOneBot.Commands.Modules;
using SkportOneBot.Services;

namespace SkportOneBot;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Skport OneBot Auto-Sign (C# Version) ===");

        var configManager = new ConfigManager();
        var profileManager = new ProfileManager();
        var skportService = new SkportService();
        var authManager = new SkportAuthManager(skportService, profileManager);

        var webServer = new WebServer(configManager.Config.BindPort, skportService, profileManager);

        var oneBotClient = new OneBotClient(configManager.Config.OneBot.WsUrl, configManager.Config.OneBot.AccessToken);

        var accountModule = new SkportAccountModule(profileManager, configManager, webServer, oneBotClient);
        var endfieldModule = new EndfieldModule(profileManager, configManager, authManager, skportService, oneBotClient);

        var router = new CommandRouter(accountModule, endfieldModule);
        oneBotClient.Router = router;

        var cronScheduler = new CronScheduler(configManager, profileManager, endfieldModule, oneBotClient);

        webServer.Start();
        Console.WriteLine($"[Web] Web服务已启动，监听端口: {configManager.Config.BindPort}");

        cronScheduler.Start();

        await oneBotClient.StartAsync();

        await Task.Delay(-1);
    }
}
