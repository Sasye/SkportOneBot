using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public class WebServer
{
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, BindSession> _bindSessions = new();
    private readonly SkportService _skportService;
    private readonly ProfileManager _profileManager;

    public WebServer(int port, SkportService skportService, ProfileManager profileManager)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{port}/");
        _skportService = skportService;
        _profileManager = profileManager;
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(HandleIncomingConnections);
    }

    public string CreateBindSession(long userId, string? messageType, long? groupId)
    {
        var token = Guid.NewGuid().ToString("N");
        _bindSessions[token] = new BindSession(userId, DateTime.UtcNow.AddMinutes(10), messageType, groupId);
        return token;
    }

    private async Task HandleIncomingConnections()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context));
            }
            catch (HttpListenerException)
            {
                // Listener stopped
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var req = context.Request;
            var res = context.Response;

            var path = req.Url?.AbsolutePath;
            if (req.HttpMethod == "GET" && path == "/bind")
            {
                var token = req.QueryString["token"];
                if (string.IsNullOrEmpty(token) || !_bindSessions.TryGetValue(token, out var session) || session.ExpireAt < DateTime.UtcNow)
                {
                    res.StatusCode = 400;
                    res.ContentType = "text/html; charset=utf-8";
                    await WriteResponseAsync(res, "<h1>链接已失效或无效的 token。</h1>");
                    return;
                }

                res.StatusCode = 200;
                res.ContentType = "text/html; charset=utf-8";
                await WriteResponseAsync(res, GetHtmlTemplate());
            }
            else if (req.HttpMethod == "POST" && path == "/api/bind")
            {
                var token = req.QueryString["token"];
                if (string.IsNullOrEmpty(token) || !_bindSessions.TryGetValue(token, out var session) || session.ExpireAt < DateTime.UtcNow)
                {
                    await WriteJsonResponseAsync(res, 400, new { success = false, message = "Session expired or invalid token" });
                    return;
                }

                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<BindRequest>(body);

                if (data == null)
                {
                    await WriteJsonResponseAsync(res, 400, new { success = false, message = "Invalid JSON" });
                    return;
                }

                string cred = data.Cred ?? "";
                string skToken = data.Token ?? "";

                try
                {
                    if (data.Mode == "auto")
                    {
                        var baseToken = await _skportService.LoginByPasswordAsync(data.Account ?? "", data.Password ?? "");
                        var skSession = await _skportService.LoginByTokenAsync(baseToken);
                        cred = skSession.Cred;
                        skToken = skSession.SignToken;
                    }

                    var roles = await _skportService.GetBindingListAsync(new SkportSession(cred, skToken));
                    if (roles.Count == 0 || roles[0].Roles.Count == 0)
                    {
                        await WriteJsonResponseAsync(res, 400, new { success = false, message = "账号下未找到终末地角色" });
                        return;
                    }

                    var addedRoles = new System.Collections.Generic.List<object>();
                    foreach (var role in roles[0].Roles)
                    {
                        var newProfile = new Profile
                        {
                            UserId = session.UserId,
                            Cred = cred,
                            Token = skToken,
                            RoleId = role.RoleId,
                            ServerId = role.ServerId,
                            AccountName = role.Nickname,
                            Account = data.Account ?? "",
                            Password = data.Password ?? "",
                            BindMessageType = session.MessageType,
                            BindGroupId = session.GroupId
                        };
                        _profileManager.AddOrUpdateProfile(newProfile);
                        addedRoles.Add(new { nickname = role.Nickname, channelName = roles[0].ChannelName });
                    }

                    _bindSessions.TryRemove(token, out _); // clean up

                    await WriteJsonResponseAsync(res, 200, new { success = true, roles = addedRoles });
                }
                catch (Exception ex)
                {
                    await WriteJsonResponseAsync(res, 400, new { success = false, message = ex.Message });
                }
            }
            else
            {
                res.StatusCode = 404;
                await WriteResponseAsync(res, "Not Found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebServer Error] {ex.Message}");
        }
    }

    private static async Task WriteResponseAsync(HttpListenerResponse res, string text)
    {
        var buffer = Encoding.UTF8.GetBytes(text);
        res.ContentLength64 = buffer.Length;
        await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        res.Close();
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse res, int statusCode, object data)
    {
        res.StatusCode = statusCode;
        res.ContentType = "application/json; charset=utf-8";
        var text = JsonSerializer.Serialize(data);
        await WriteResponseAsync(res, text);
    }

    private string GetHtmlTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>SKPORT 账号绑定</title>
  <style>
    body { font-family: system-ui, sans-serif; max-width: 500px; margin: 2rem auto; padding: 0 1rem; }
    .tabs { display: flex; margin-bottom: 1rem; border-bottom: 1px solid #ccc; }
    .tab { padding: 0.5rem 1rem; cursor: pointer; border: 1px solid transparent; margin-bottom: -1px; }
    .tab.active { border: 1px solid #ccc; border-bottom-color: white; border-radius: 4px 4px 0 0; background: #fff; }
    .panel { display: none; }
    .panel.active { display: block; }
    .form-group { margin-bottom: 1rem; }
    label { display: block; margin-bottom: 0.5rem; font-weight: bold; }
    input[type=""text""], input[type=""password""] { width: 100%; padding: 0.5rem; box-sizing: border-box; }
    button { padding: 0.5rem 1rem; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; width: 100%; }
    button:hover { background: #0056b3; }
    .loading { display: none; margin-top: 1rem; color: #666; text-align: center; }
  </style>
</head>
<body>
  <h2>绑定终末地签到</h2>
  <div class=""tabs"">
    <div class=""tab active"" onclick=""switchTab('auto')"">账号密码全自动</div>
    <div class=""tab"" onclick=""switchTab('manual')"">手动填入凭据</div>
  </div>

  <div id=""auto-panel"" class=""panel active"">
    <p style=""font-size: 0.9em; color: #666;"">使用鹰角前线通行证账号(或邮箱)和密码登录，自动拉取终末地角色并完成绑定。</p>
    <form id=""auto-form"">
      <div class=""form-group"">
        <label>账号 (或邮箱)</label>
        <input type=""text"" name=""account"" required>
      </div>
      <div class=""form-group"">
        <label>密码</label>
        <input type=""password"" name=""password"" required>
      </div>
      <button type=""submit"">一键获取并绑定</button>
    </form>
  </div>

  <div id=""manual-panel"" class=""panel"">
    <p style=""font-size: 0.9em; color: #666;"">对于第三方登录用户，请在网页手动提取 Token 后填入。机器人会自动拉取角色绑定。</p>
    <form id=""manual-form"">
      <div class=""form-group"">
        <label>SK_OAUTH_CRED_KEY</label>
        <input type=""text"" name=""cred"" required>
      </div>
      <div class=""form-group"">
        <label>SK_TOKEN_CACHE_KEY</label>
        <input type=""text"" name=""token"" required>
      </div>
      <button type=""submit"">使用 Token 绑定</button>
    </form>
  </div>

  <div id=""loading"" class=""loading"">正在处理，请稍候...</div>
  <div id=""message"" style=""margin-top: 1rem; color: red; text-align: center;""></div>

  <script>
    function switchTab(mode) {
      document.querySelectorAll('.tab').forEach(el => el.classList.remove('active'));
      document.querySelectorAll('.panel').forEach(el => el.classList.remove('active'));
      document.querySelector('.tab[onclick=""switchTab(\'' + mode + '\')""]').classList.add('active');
      document.getElementById(mode + '-panel').classList.add('active');
    }

    async function submitForm(e, mode) {
      e.preventDefault();
      document.getElementById('loading').style.display = 'block';
      document.getElementById('message').innerText = '';
      
      const formData = new FormData(e.target);
      const data = Object.fromEntries(formData.entries());
      data.mode = mode;

      try {
        const res = await fetch('/api/bind?token=' + encodeURIComponent(new URLSearchParams(window.location.search).get('token')), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(data)
        });
        
        const responseText = await res.text();
        let result;
        try {
          result = JSON.parse(responseText);
        } catch(e) {
          throw new Error('服务器端返回异常: ' + responseText.substring(0, 100));
        }

        if (result.success) {
          document.body.innerHTML = '<h2>绑定成功！</h2><p>已成功绑定的角色：</p><ul>' + 
            result.roles.map(r => '<li>' + r.nickname + ' (' + r.channelName + ')</li>').join('') + 
            '</ul><p>你可以关闭此页面并回到 QQ 试用自动签到了。</p>';
        } else {
          document.getElementById('message').innerText = result.message || '绑定失败';
        }
      } catch (err) {
        document.getElementById('message').innerText = '请求失败：' + err.message;
      } finally {
        document.getElementById('loading').style.display = 'none';
      }
    }

    document.getElementById('auto-form').addEventListener('submit', (e) => submitForm(e, 'auto'));
    document.getElementById('manual-form').addEventListener('submit', (e) => submitForm(e, 'manual'));
  </script>
</body>
</html>";
    }
}

public record BindSession(long UserId, DateTime ExpireAt, string? MessageType, long? GroupId);

public class BindRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("mode")]
    public string? Mode { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("account")]
    public string? Account { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("password")]
    public string? Password { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("cred")]
    public string? Cred { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("token")]
    public string? Token { get; set; }
}
