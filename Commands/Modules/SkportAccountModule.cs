using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkportOneBot.Models;
using SkportOneBot.Services;

namespace SkportOneBot.Commands.Modules;

public class SkportAccountModule
{
    private readonly ProfileManager _profileManager;
    private readonly ConfigManager _configManager;
    private readonly WebServer _webServer;
    private readonly OneBotClient _botClient;

    public SkportAccountModule(ProfileManager profileManager, ConfigManager configManager, WebServer webServer, OneBotClient botClient)
    {
        _profileManager = profileManager;
        _configManager = configManager;
        _webServer = webServer;
        _botClient = botClient;
    }

    public async Task BindAsync(MessageContext ctx)
    {
        var token = _webServer.CreateBindSession(ctx.UserId, ctx.MessageType, ctx.GroupId);
        var bindUrl = _configManager.Config.BindUrl;
        var baseUrl = string.IsNullOrWhiteSpace(bindUrl) ? $"http://127.0.0.1:{_configManager.Config.BindPort}" : bindUrl.TrimEnd('/');
        var url = $"{baseUrl}/bind?token={token}";
        var msg = $"[CQ:at,qq={ctx.UserId}] 请在 10 分钟内点击以下链接完成绑定：\n{url}";

        await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, msg);
    }

    public async Task ListBindAsync(MessageContext ctx)
    {
        var userProfiles = _profileManager.GetProfilesByUser(ctx.UserId);
        if (!userProfiles.Any())
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 您当前没有绑定任何终末地角色。");
            return;
        }

        var sb = new StringBuilder($"[CQ:at,qq={ctx.UserId}] 您的终末地绑定列表：\n");
        for (int i = 0; i < userProfiles.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {userProfiles[i].AccountName} (角色ID: {userProfiles[i].RoleId})");
        }
        sb.AppendLine("\n发送 删除绑定 [序号] 可以删除对应角色\n发送 删除全部绑定 可以清空您的所有角色");
        await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, sb.ToString().TrimEnd());
    }

    public async Task DeleteBindAsync(MessageContext ctx)
    {
        var isDeleteAll = ctx.Text == "删除全部绑定" || ctx.Text == "解绑全部";
        var userProfiles = _profileManager.GetProfilesByUser(ctx.UserId);

        if (!userProfiles.Any())
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 您当前没有绑定任何终末地角色。");
            return;
        }

        if (isDeleteAll)
        {
            _profileManager.RemoveProfilesByUser(ctx.UserId);
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 已成功删除您的所有终末地绑定记录。");
            return;
        }

        var parts = ctx.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out int index) || index < 1 || index > userProfiles.Count)
        {
            await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 格式错误或序号无效。请输入: 删除绑定 [序号]，可通过 绑定列表 查看。");
            return;
        }

        var profileToRemove = userProfiles[index - 1];
        var allProfiles = _profileManager.GetAllProfiles();
        allProfiles.RemoveAll(p => p.RoleId == profileToRemove.RoleId && p.ServerId == profileToRemove.ServerId);
        _profileManager.ReplaceAllProfiles(allProfiles);

        await _botClient.ReplyAsync(ctx.MessageType, ctx.UserId, ctx.GroupId, $"[CQ:at,qq={ctx.UserId}] 已删除角色：{profileToRemove.AccountName}");
    }
}
