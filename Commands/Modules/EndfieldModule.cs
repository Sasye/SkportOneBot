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

    public async Task GachaAnalyzeAsync(MessageContext ctx, string args = "")
    {
        var allowedGroups = _configManager.Config.AllowedGroups;
        var isGroupAllowed = allowedGroups == null || allowedGroups.Length == 0 || (ctx.GroupId.HasValue && allowedGroups.Contains(ctx.GroupId.Value));

        if (ctx.MessageType == "group" && ctx.GroupId.HasValue && !isGroupAllowed) return;

        var userProfiles = _profileManager.GetProfilesByUser(ctx.UserId).ToList();
        if (!userProfiles.Any())
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 您还未绑定 SKPORT 账号，请发送 终末地绑定 进行绑定。");
            return;
        }

        Profile? profile = null;
        if (string.IsNullOrWhiteSpace(args))
        {
            profile = userProfiles.FirstOrDefault();
        }
        else
        {
            if (int.TryParse(args, out int index) && index > 0 && index <= userProfiles.Count)
            {
                profile = userProfiles[index - 1];
            }
            else
            {
                profile = userProfiles.FirstOrDefault(p => 
                    string.Equals(p.AccountName, args, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(p.Account, args, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (profile == null)
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 找不到指定的账号。您可以发送 绑定列表 查看已绑定的账号序号或名字。");
            return;
        }

        await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 正在拉取抽卡记录，这可能需要一些时间，请稍候...");

        try
        {
            var serverId = !string.IsNullOrEmpty(profile.ServerId) ? profile.ServerId : "1";
            
            var (charStats, weaponStats, uid) = await _authManager.ExecuteWithSessionAsync(profile, async session =>
            {
                // We need the base token to get u8 token
                // If we don't have base token, we need to login again
                var authResult = await _skportService.LoginByPasswordAsync(profile.Account, profile.Password);
                
                // Exchange base token for Endfield specific oauth token (grant)
                var gachaAuthToken = await _skportService.GetGachaAuthAsync(authResult.token, "gryphline");
                
                // Fetch the actual account UID from binding_list API using oauth token
                var accountUid = await _skportService.GetBindingListUidAsync(gachaAuthToken, "gryphline");
                
                // Get the final u8 token
                var u8Token = await _skportService.GetU8TokenAsync(gachaAuthToken, accountUid, "gryphline");
                
                var charStatsList = new List<GachaStatistics>();
                foreach (var poolType in new[] { "E_CharacterGachaPoolType_Special", "E_CharacterGachaPoolType_Joint", "E_CharacterGachaPoolType_Standard", "E_CharacterGachaPoolType_Beginner" })
                {
                    var records = await _skportService.GetCharRecordsAsync(u8Token, serverId, poolType, "gryphline");
                    if (records.Any())
                    {
                        charStatsList.Add(GachaCalculator.AnalyzeCharPoolData(poolType, records));
                    }
                }

                var weaponStatsList = new List<GachaStatistics>();
                var weaponPools = await _skportService.GetWeaponPoolsAsync(u8Token, serverId, "gryphline");
                foreach (var pool in weaponPools)
                {
                    var records = await _skportService.GetWeaponRecordsAsync(u8Token, serverId, pool.PoolId, "gryphline");
                    if (records.Any())
                    {
                        weaponStatsList.Add(GachaCalculator.AnalyzeWeaponPoolData(pool.PoolId, pool.PoolName, records));
                    }
                }

                return (charStatsList, weaponStatsList, accountUid);
            });

            var renderer = new GachaImageRenderer();
            var imagePath = renderer.RenderAnalysisImage(uid, profile.AccountName, charStats, weaponStats);
            
            // Format path for CQ code (absolute path needs proper formatting)
            var uriPath = new Uri(imagePath).AbsoluteUri;
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] \n[CQ:image,file={uriPath}]");
        }
        catch (Exception ex)
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 抽卡分析失败：{ex.Message}");
        }
    }
}
