using System.Text.Json.Serialization;

namespace SkportOneBot.Models;

public class OneBotConfig
{
    [JsonPropertyName("ws_url")]
    public string WsUrl { get; set; } = "ws://127.0.0.1:6700";

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
}

public class AppConfig
{
    [JsonPropertyName("onebot")]
    public OneBotConfig OneBot { get; set; } = new();

    [JsonPropertyName("bind_port")]
    public int BindPort { get; set; } = 7777;

    [JsonPropertyName("bind_url")]
    public string BindUrl { get; set; } = "http://127.0.0.1";

    [JsonPropertyName("cron_schedule")]
    public string CronSchedule { get; set; } = "0 10 * * *";

    [JsonPropertyName("allowed_groups")]
    public long[] AllowedGroups { get; set; } = System.Array.Empty<long>();
}
