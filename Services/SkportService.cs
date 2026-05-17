using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SkportOneBot.Services;

public sealed class SkportService
{
    private const int MinAccountSignDelaySeconds = 10;
    private const int MaxAccountSignDelaySeconds = 20;

    private const string AppCode = "3dacefa138426cfe";
    private const string SkportAppCode = "6eb76d4e13aa36e6";
    private const string UserAgent = "Mozilla/5.0 (Linux; Android 12; SM-A5560 Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36; SKLand/1.52.1";
    
    private const string TokenByPasswordUrl = "https://as.gryphline.com/user/auth/v1/token_by_email_password";
    private const string GrantCodeUrl = "https://as.gryphline.com/user/oauth2/v2/grant";
    private const string CredCodeUrl = "https://zonai.skport.com/api/v1/user/auth/generate_cred_by_code";
    private const string BindingUrl = "https://zonai.skport.com/api/v1/game/player/binding";
    private const string EndfieldSignUrl = "https://zonai.skport.com/api/v1/game/endfield/attendance";

    private static readonly Dictionary<string, string> ItemTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Intermediate Combat Record", "中级作战记录" },
        { "Elementary Cognitive Carrier", "初级存相载体" },
        { "Advanced Combat Record", "高级作战记录" },
        { "Arms INSP Kit", "武器检查装置" },
        { "Arms INSP Set", "武器检查套件" },
        { "Protoprism", "协议棱柱" },
        { "Talosian Credit Notes|T-Creds", "折金票" },
        { "Oroberyl", "嵌晶玉" }
    };

    private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    })
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    public async Task<string> LoginByPasswordAsync(string account, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(account))
            throw new ArgumentException("请输入账号", nameof(account));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("请输入密码", nameof(password));

        using var request = CreateRequest(HttpMethod.Post, TokenByPasswordUrl);
        var payload = new Dictionary<string, string>
        {
            ["password"] = password
        };
        
        if (account.Contains('@'))
            payload["email"] = account.Trim();
        else
            payload["phone"] = account.Trim();

        request.Content = JsonContent(JsonSerializer.Serialize(payload));

        var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
        return ExtractLoginToken(root, "账号密码登录");
    }

    public async Task<SkportSession> LoginByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var grantRequest = CreateRequest(HttpMethod.Post, GrantCodeUrl);
        grantRequest.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["appCode"] = SkportAppCode,
            ["token"] = token,
            ["type"] = 0
        }));
        var grantRoot = await SendJsonAsync(grantRequest, cancellationToken).ConfigureAwait(false);
        EnsureAuthOk(grantRoot, "使用 token 获取授权码");
        var grantCode = grantRoot["data"]?["code"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(grantCode))
            throw new InvalidOperationException("使用 token 获取授权码失败：返回结果缺少 code。");

        using var credRequest = CreateRequest(HttpMethod.Post, CredCodeUrl);
        credRequest.Content = JsonContent(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["code"] = grantCode,
            ["kind"] = 1
        }));
        var credRoot = await SendJsonAsync(credRequest, cancellationToken).ConfigureAwait(false);
        EnsureApiOk(credRoot, "获取 cred");

        var data = credRoot["data"];
        var cred = data?["cred"]?.GetValue<string>() ?? "";
        var signToken = data?["token"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(cred) || string.IsNullOrWhiteSpace(signToken))
            throw new InvalidOperationException("获取 cred 失败：返回结果缺少 cred 或 token。");

        return new SkportSession(cred, signToken);
    }

    public async Task<List<SkportBinding>> GetBindingListAsync(SkportSession session, CancellationToken cancellationToken = default)
    {
        using var request = CreateSignedRequest(HttpMethod.Get, BindingUrl, null, session);
        var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureApiOk(root, "获取绑定角色列表");

        var result = new List<SkportBinding>();
        foreach (var game in root["data"]?["list"]?.AsArray() ?? new JsonArray())
        {
            var appCode = game?["appCode"]?.GetValue<string>() ?? "";
            if (appCode != "endfield") continue;

            foreach (var item in game?["bindingList"]?.AsArray() ?? new JsonArray())
            {
                if (item is not JsonObject obj) continue;
                result.Add(ParseBinding(appCode, obj));
            }
        }

        return result;
    }

    public async Task<string> SignEndfieldAsync(SkportBinding binding, SkportSession session, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        foreach (var role in binding.Roles)
        {
            using var request = CreateSignedRequest(HttpMethod.Post, EndfieldSignUrl, "", session);
            request.Content = JsonContent("");
            request.Headers.TryAddWithoutValidation("sk-game-role", $"3_{role.RoleId}_{role.ServerId}");
            request.Headers.TryAddWithoutValidation("referer", "https://game.skport.com/");
            request.Headers.TryAddWithoutValidation("origin", "https://game.skport.com/");

            var root = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
            var channelStr = string.IsNullOrWhiteSpace(binding.ChannelName) ? "" : $"({binding.ChannelName})";
            var title = $"[{binding.GameName}] 角色 {role.Nickname}{channelStr}";
            if (!IsApiOk(root))
            {
                var code = root["code"]?.GetValue<int?>() ?? -1;
                if (code == 10000) {
                    results.Add($"{title} 签到失败：Token已过期! 请重新绑定。");
                } else if (code == 10001) {
                    results.Add($"{title} 今日已签到");
                } else {
                    results.Add($"{title} 签到失败：{GetErrorMessage(root)}");
                }
                continue;
            }

            var awards = new List<string>();
            var infoMap = root["data"]?["resourceInfoMap"]?.AsObject();
            foreach (var award in root["data"]?["awardIds"]?.AsArray() ?? new JsonArray())
            {
                var id = award?["id"]?.GetValue<string>() ?? "";
                if (infoMap == null || string.IsNullOrEmpty(id) || infoMap[id] == null) continue;

                var info = infoMap[id];
                var name = info?["name"]?.GetValue<string>() ?? "";
                if (ItemTranslations.TryGetValue(name, out var zhName)) name = zhName;
                
                var count = info?["count"]?.GetValue<int?>() ?? 1;
                if (!string.IsNullOrWhiteSpace(name)) awards.Add($"{name}x{count}");
            }

            results.Add($"{title} 签到成功，获得 {string.Join("、", awards)}");
        }

        return string.Join(Environment.NewLine, results);
    }

    private HttpRequestMessage CreateSignedRequest(HttpMethod method, string url, string? body, SkportSession session)
    {
        var request = CreateRequest(method, url);
        request.Headers.TryAddWithoutValidation("cred", session.Cred);

        var uri = new Uri(url);
        var bodyOrQuery = method == HttpMethod.Get ? uri.Query.TrimStart('?') : body ?? "";
        var (sign, headers) = GenerateSignature(session.SignToken, uri.AbsolutePath, bodyOrQuery);
        request.Headers.TryAddWithoutValidation("sign", sign);
        foreach (var header in headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return request;
    }

    private static (string Sign, Dictionary<string, string> Headers) GenerateSignature(string token, string path, string? bodyOrQuery)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        var headers = new Dictionary<string, string>
        {
            ["platform"] = "3",
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
            ["dId"] = "",
            ["vName"] = "1.0.0"
        };

        var headerJson = $"{{\"platform\":\"3\",\"timestamp\":\"{headers["timestamp"]}\",\"dId\":\"\",\"vName\":\"1.0.0\"}}";
        var source = path + (bodyOrQuery ?? "") + headers["timestamp"] + headerJson;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(token));
        var hmacHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        var md5Hex = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(hmacHex))).ToLowerInvariant();
        return (md5Hex, headers);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
        request.Headers.TryAddWithoutValidation("Connection", "close");
        request.Headers.TryAddWithoutValidation("X-Requested-With", "com.gryphline.skport");
        return request;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<JsonNode> SendJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            return JsonNode.Parse(text) ?? new JsonObject();
        }
        catch
        {
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {text}");
            throw;
        }
    }

    private static void EnsureAuthOk(JsonNode root, string action)
    {
        var status = root["status"]?.GetValue<int?>() ?? root["code"]?.GetValue<int?>() ?? -1;
        if (status != 0)
            throw new InvalidOperationException($"{action}失败：{GetErrorMessage(root)}");
    }

    private static string ExtractLoginToken(JsonNode root, string action)
    {
        EnsureAuthOk(root, action);
        var token = root["data"]?["token"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException($"{action}失败：返回结果缺少 token。");
        return token;
    }

    private static void EnsureApiOk(JsonNode root, string action)
    {
        if (!IsApiOk(root))
            throw new InvalidOperationException($"{action}失败：{GetErrorMessage(root)}");
    }

    private static bool IsApiOk(JsonNode root)
    {
        return (root["code"]?.GetValue<int?>() ?? -1) == 0;
    }

    private static string GetErrorMessage(JsonNode root)
    {
        return root["message"]?.GetValue<string>()
            ?? root["msg"]?.GetValue<string>()
            ?? root.ToJsonString();
    }

    private static SkportBinding ParseBinding(string appCode, JsonObject obj)
    {
        var roles = new List<SkportRole>();
        foreach (var role in obj["roles"]?.AsArray() ?? new JsonArray())
        {
            roles.Add(new SkportRole(
                role?["roleId"]?.GetValue<string>() ?? "",
                role?["serverId"]?.GetValue<string>() ?? "",
                role?["nickname"]?.GetValue<string>() ?? ""));
        }

        return new SkportBinding(
            appCode,
            obj["gameName"]?.GetValue<string>() ?? appCode,
            obj["channelName"]?.GetValue<string>() ?? "",
            obj["nickName"]?.GetValue<string>() ?? "",
            obj["uid"]?.GetValue<string>() ?? "",
            obj["channelMasterId"]?.GetValue<string>() ?? "",
            roles);
    }
}

public sealed record SkportSession(string Cred, string SignToken);
public sealed record SkportBinding(string AppCode, string GameName, string ChannelName, string Nickname, string Uid, string ChannelMasterId, List<SkportRole> Roles);
public sealed record SkportRole(string RoleId, string ServerId, string Nickname);
