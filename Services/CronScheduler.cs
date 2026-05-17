using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using SkportOneBot.Models;
using SkportOneBot.Commands.Modules;

namespace SkportOneBot.Services;

public class CronScheduler
{
    private readonly ConfigManager _configManager;
    private readonly ProfileManager _profileManager;
    private readonly EndfieldModule _endfieldModule;
    private readonly OneBotClient _oneBotClient;
    private CancellationTokenSource _cts = new();

    public CronScheduler(ConfigManager configManager, ProfileManager profileManager, EndfieldModule endfieldModule, OneBotClient oneBotClient)
    {
        _configManager = configManager;
        _profileManager = profileManager;
        _endfieldModule = endfieldModule;
        _oneBotClient = oneBotClient;
    }

    public void Start()
    {
        if (string.IsNullOrWhiteSpace(_configManager.Config.CronSchedule))
        {
            Console.WriteLine("[Cron] 未设置定时任务 (cron_schedule 为空)");
            return;
        }

        try
        {
            var expression = CronExpression.Parse(_configManager.Config.CronSchedule, CronFormat.Standard);
            Console.WriteLine($"[Cron] 已设置定时任务: {_configManager.Config.CronSchedule}");
            _ = Task.Run(() => ScheduleNextRun(expression));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cron] 定时任务解析失败: {ex.Message}");
        }
    }

    private async Task ScheduleNextRun(CronExpression expression)
    {
        while (!_cts.IsCancellationRequested)
        {
            var next = expression.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
            if (!next.HasValue) break;

            var delay = next.Value - DateTimeOffset.UtcNow;
            if (delay.TotalMilliseconds > 0)
            {
                await Task.Delay(delay, _cts.Token);
            }

            if (!_cts.IsCancellationRequested)
            {
                _ = Task.Run(ExecuteSignaturesAsync);
            }
        }
    }

    private async Task ExecuteSignaturesAsync()
    {
        Console.WriteLine($"[Cron] 开始执行定时签到任务...");
        var profiles = _profileManager.GetAllProfiles().Where(p => !string.IsNullOrEmpty(p.Cred)).ToList();

        foreach (var profile in profiles)
        {
            Console.WriteLine($"[Sign] 正在签到账号: {profile.AccountName}");
            var result = await _endfieldModule.PerformSignAsync(profile);

            Console.WriteLine($"[Sign Result] {result}");

            if (profile.UserId != 0)
            {
                if (profile.BindMessageType == "group" && profile.BindGroupId.HasValue && profile.BindGroupId.Value != 0)
                {
                    await _oneBotClient.ReplyAsync("group", profile.UserId, profile.BindGroupId.Value, $"[CQ:at,qq={profile.UserId}] {result}");
                }
                else
                {
                    await _oneBotClient.ReplyAsync("private", profile.UserId, null, result);
                }
            }

            await Task.Delay(2000); // 延迟2秒防止风控
        }
    }
}
