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
using SkportOneBot.Commands;

namespace SkportOneBot.Services;

public class OneBotClient
{
    private readonly string _wsUrl;
    private readonly string? _accessToken;
    private ClientWebSocket _ws = new();
    private bool _isRunning;

    public CommandRouter Router { get; set; } = null!;

    public OneBotClient(string wsUrl, string? accessToken)
    {
        _wsUrl = wsUrl;
        _accessToken = accessToken;
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        _ = Task.Run(HeartbeatLoopAsync);

        while (_isRunning)
        {
            try
            {
                if (_ws.State != WebSocketState.Open)
                {
                    Console.WriteLine($"[OneBot] 正在连接 WebSocket: {_wsUrl}...");
                    _ws = new ClientWebSocket();
                    if (!string.IsNullOrEmpty(_accessToken))
                    {
                        _ws.Options.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
                    }
                    await _ws.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);
                    Console.WriteLine("[OneBot] WebSocket 连接成功！");
                }

                var buffer = new byte[8192];
                var sb = new StringBuilder();

                while (_ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage)
                    {
                        var message = sb.ToString();
                        sb.Clear();
                        _ = HandleMessageAsync(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OneBot] WebSocket 连接断开或发生错误: {ex.Message}");
                await Task.Delay(3000);
            }
        }
    }

    private async Task HeartbeatLoopAsync()
    {
        while (_isRunning)
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    var ping = new { action = "get_status", @params = new { } };
                    await SendPayloadAsync(ping);
                }
            }
            catch { }
            await Task.Delay(30000);
        }
    }

    private async Task HandleMessageAsync(string message)
    {
        try
        {
            var root = JsonNode.Parse(message);
            if (root == null) return;

            var postType = root["post_type"]?.GetValue<string>();
            if (postType == "message")
            {
                var rawMessage = root["raw_message"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(rawMessage)) return;

                var text = rawMessage.Trim();
                var userId = root["user_id"]?.GetValue<long>() ?? 0;
                var messageType = root["message_type"]?.GetValue<string>();
                var groupId = root["group_id"]?.GetValue<long?>();

                var ctx = new MessageContext(text, messageType, userId, groupId);
                
                if (Router != null)
                {
                    await Router.RouteAsync(ctx);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OneBot] 消息解析/处理错误: {ex.Message}");
        }
    }

    public async Task SendPayloadAsync(object payload)
    {
        if (_ws.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task ReplyAsync(string? messageType, long userId, long? groupId, string msg)
    {
        if (messageType == "group" && groupId.HasValue)
        {
            await SendPayloadAsync(new
            {
                action = "send_group_msg",
                @params = new { group_id = groupId.Value, message = msg }
            });
        }
        else
        {
            await SendPayloadAsync(new
            {
                action = "send_private_msg",
                @params = new { user_id = userId, message = msg }
            });
        }
    }
}
