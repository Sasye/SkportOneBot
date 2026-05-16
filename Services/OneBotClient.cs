using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public class OneBotClient
{
    private readonly AppConfig _config;
    private readonly WebServer _webServer;
    private readonly SkportService _skportService;
    private readonly Func<List<Profile>> _getProfiles;
    private readonly Action<List<Profile>> _saveProfiles;
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts = new();

    public OneBotClient(AppConfig config, WebServer webServer, SkportService skportService, Func<List<Profile>> getProfiles, Action<List<Profile>> saveProfiles)
    {
        _config = config;
        _webServer = webServer;
        _skportService = skportService;
        _getProfiles = getProfiles;
        _saveProfiles = saveProfiles;
        _ws = new ClientWebSocket();
    }

    public async Task StartAsync()
    {
        _ = Task.Run(ConnectLoopAsync);
    }

    private async Task ConnectLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();
                if (!string.IsNullOrEmpty(_config.OneBot.AccessToken))
                {
                    _ws.Options.SetRequestHeader("Authorization", $"Bearer {_config.OneBot.AccessToken}");
                }

                Console.WriteLine($"[OneBot] 正在连接到正向 WebSocket: {_config.OneBot.WsUrl} ...");
                await _ws.ConnectAsync(new Uri(_config.OneBot.WsUrl), _cts.Token);
                Console.WriteLine($"[OneBot] 已连接到正向 WebSocket");

                await ReceiveLoopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OneBot] WebSocket 错误: {ex.Message}");
            }

            if (!_cts.IsCancellationRequested)
            {
                Console.WriteLine("[OneBot] 正向 WebSocket 连接已断开，5秒后尝试重连...");
                await Task.Delay(5000);
            }
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[1024 * 64];
        while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _ = Task.Run(() => HandleMessageAsync(json));
            }
        }
    }

    private async Task HandleMessageAsync(string json)
    {
        try
        {
            var root = JsonNode.Parse(json);
            if (root == null) return;

            var postType = root["post_type"]?.GetValue<string>();
            if (postType == "message")
            {
                var rawMessage = root["raw_message"]?.GetValue<string>() ?? "";
                var text = rawMessage.Trim();
                var userId = root["user_id"]?.GetValue<long>() ?? 0;
                var messageType = root["message_type"]?.GetValue<string>();
                var groupId = root["group_id"]?.GetValue<long?>();

                if (text == "终末地绑定" || text == "zmdbd" || text == "skport绑定")
                {
                    var token = _webServer.CreateBindSession(userId, messageType, groupId);
                    var baseUrl = string.IsNullOrWhiteSpace(_config.BindUrl) ? $"http://127.0.0.1:{_config.BindPort}" : _config.BindUrl.TrimEnd('/');
                    var url = $"{baseUrl}/bind?token={token}";
                    var msg = $"[CQ:at,qq={userId}] 请在 10 分钟内点击以下链接完成绑定：\n{url}";

                    await ReplyAsync(messageType, userId, groupId, msg);
                    return;
                }

                if (text == "终末地签到" || text == "zmdqd" || text == "skport签到")
                {
                    var isGroupAllowed = _config.AllowedGroups == null || _config.AllowedGroups.Length == 0 || (groupId.HasValue && _config.AllowedGroups.Contains(groupId.Value));

                    if (messageType == "group" && groupId.HasValue && !isGroupAllowed) return;

                    var userProfiles = _getProfiles().Where(p => p.UserId == userId).ToList();
                    if (userProfiles.Any())
                    {
                        var results = new List<string>();
                        foreach (var profile in userProfiles)
                        {
                            var res = await SignProfileAsync(profile);
                            results.Add(res);
                            await Task.Delay(1000);
                        }
                        await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] [手动触发]\n{string.Join("\n", results)}");
                    }
                    else
                    {
                        await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] 您还未绑定 SKPORT 账号，请发送 终末地绑定 进行绑定。");
                    }
                    return;
                }

                if (text == "绑定列表" || text == "终末地绑定列表" || text == "zmdbdlb")
                {
                    var userProfiles = _getProfiles().Where(p => p.UserId == userId).ToList();
                    if (!userProfiles.Any())
                    {
                        await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] 您当前没有绑定任何终末地角色。");
                    }
                    else
                    {
                        var sb = new StringBuilder($"[CQ:at,qq={userId}] 您的终末地绑定列表：\n");
                        for (int i = 0; i < userProfiles.Count; i++)
                        {
                            sb.AppendLine($"{i + 1}. {userProfiles[i].AccountName} (角色ID: {userProfiles[i].RoleId})");
                        }
                        sb.AppendLine("\n发送 删除绑定 [序号] 可以删除对应角色\n发送 删除全部绑定 可以清空您的所有角色");
                        await ReplyAsync(messageType, userId, groupId, sb.ToString().TrimEnd());
                    }
                    return;
                }

                if (text.StartsWith("删除绑定") || text.StartsWith("解绑") || text == "删除全部绑定")
                {
                    var isDeleteAll = text == "删除全部绑定" || text == "解绑全部";
                    var userProfiles = _getProfiles().Where(p => p.UserId == userId).ToList();

                    if (!userProfiles.Any())
                    {
                        await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] 您当前没有绑定任何终末地角色。");
                        return;
                    }

                    if (isDeleteAll)
                    {
                        var allProfiles = _getProfiles();
                        allProfiles.RemoveAll(p => p.UserId == userId);
                        _saveProfiles(allProfiles);
                        await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] 已成功删除您的所有终末地绑定记录。");
                        return;
                    }

                    var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int index) || index < 1 || index > userProfiles.Count)
                    {
                        await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] 格式错误或序号无效。请输入: 删除绑定 [序号]，可通过 绑定列表 查看。");
                        return;
                    }

                    var profileToDelete = userProfiles[index - 1];
                    var profiles = _getProfiles();
                    profiles.RemoveAll(p => p.UserId == userId && p.RoleId == profileToDelete.RoleId && p.ServerId == profileToDelete.ServerId);
                    _saveProfiles(profiles);

                    await ReplyAsync(messageType, userId, groupId, $"[CQ:at,qq={userId}] 已成功删除角色绑定: {profileToDelete.AccountName}");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OneBot] 消息解析/处理错误: {ex.Message}");
        }
    }

    public async Task<string> SignProfileAsync(Profile profile)
    {
        try
        {
            var binding = new SkportBinding("endfield", "终末地", "", profile.AccountName, "", "", new List<SkportRole>
            {
                new SkportRole(profile.RoleId, profile.ServerId, profile.AccountName)
            });

            var session = new SkportSession(profile.Cred, profile.Token);
            var result = await _skportService.SignEndfieldAsync(binding, session);

            if (result.Contains("Token已过期") && !string.IsNullOrEmpty(profile.Account) && !string.IsNullOrEmpty(profile.Password))
            {
                Console.WriteLine($"[Sign] Token过期，正在尝试自动刷新 {profile.AccountName}...");
                try
                {
                    var baseToken = await _skportService.LoginByPasswordAsync(profile.Account, profile.Password);
                    var skSession = await _skportService.LoginByTokenAsync(baseToken);
                    profile.Cred = skSession.Cred;
                    profile.Token = skSession.SignToken;

                    var profiles = _getProfiles();
                    var existing = profiles.FirstOrDefault(p => p.RoleId == profile.RoleId && p.ServerId == profile.ServerId);
                    if (existing != null)
                    {
                        existing.Cred = profile.Cred;
                        existing.Token = profile.Token;
                        _saveProfiles(profiles);
                    }

                    session = new SkportSession(profile.Cred, profile.Token);
                    result = await _skportService.SignEndfieldAsync(binding, session);
                }
                catch (Exception e)
                {
                    result = $"[{profile.AccountName}] 自动刷新 Token 失败，请重新绑定！({e.Message})";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Endfield 签到请求失败 ({profile.AccountName}): {ex.Message}";
        }
    }

    public async Task SendPayloadAsync(object payload)
    {
        if (_ws.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(payload);
        var buffer = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task ReplyAsync(string? messageType, long userId, long? groupId, string message)
    {
        await SendPayloadAsync(new
        {
            action = "send_msg",
            @params = new { message_type = messageType ?? "private", user_id = userId, group_id = groupId ?? 0, message }
        });
    }

    public async Task SendPrivateMsgAsync(long userId, string message)
    {
        await SendPayloadAsync(new
        {
            action = "send_private_msg",
            @params = new { user_id = userId, message }
        });
    }
}
