using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkportOneBot.Models;
using SkportOneBot.Services;

namespace SkportOneBot.Commands.Modules;

public class EndfieldModule
{
    private readonly ProfileManager _profileManager;
    private readonly ConfigManager _configManager;
    private readonly SkportAuthManager _authManager;
    private readonly SkportService _skportService;
    private readonly OneBotClient _botClient;

    public EndfieldModule(ProfileManager profileManager, ConfigManager configManager, SkportAuthManager authManager, SkportService skportService, OneBotClient botClient)
    {
        _profileManager = profileManager;
        _configManager = configManager;
        _authManager = authManager;
        _skportService = skportService;
        _botClient = botClient;
    }

    public async Task SignAsync(MessageContext ctx)
    {
        var allowedGroups = _configManager.Config.AllowedGroups;
        var isGroupAllowed = allowedGroups == null || allowedGroups.Length == 0 || (ctx.GroupId.HasValue && allowedGroups.Contains(ctx.GroupId.Value));

        if (ctx.MessageType == "group" && ctx.GroupId.HasValue && !isGroupAllowed) return;

        var userProfiles = _profileManager.GetProfilesByUser(ctx.UserId);
        if (userProfiles.Any())
        {
            var results = new List<string>();
            foreach (var profile in userProfiles)
            {
                var res = await PerformSignAsync(profile);
                results.Add(res);
                await Task.Delay(1000);
            }
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] [手动触发]\n{string.Join("\n", results)}");
        }
        else
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 您还未绑定 SKPORT 账号，请发送 终末地绑定 进行绑定。");
        }
    }

    public async Task<string> PerformSignAsync(Profile profile)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            try
            {
                var binding = new SkportBinding("endfield", "终末地", "", profile.AccountName, "", "", new List<SkportRole>
                {
                    new SkportRole(profile.RoleId, profile.ServerId, profile.AccountName)
                });

                return await _authManager.ExecuteWithSessionAsync(profile, async session => 
                {
                    return await _skportService.SignEndfieldAsync(binding, session);
                });
            }
            catch (Exception ex)
            {
                if (retry == 2)
                    return $"Endfield 签到请求失败 ({profile.AccountName}): {ex.Message}";
                
                Console.WriteLine($"[Sign] {profile.AccountName} 请求失败 ({ex.Message})，将在 3 秒后重试 ({retry + 1}/3)...");
                await Task.Delay(3000);
            }
        }
        return "";
    }
}
