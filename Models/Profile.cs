using System.Text.Json.Serialization;

namespace SkportOneBot.Models;

public class Profile
{
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    [JsonPropertyName("SK_OAUTH_CRED_KEY")]
    public string Cred { get; set; } = "";

    [JsonPropertyName("SK_TOKEN_CACHE_KEY")]
    public string Token { get; set; } = "";

    [JsonPropertyName("id")]
    public string RoleId { get; set; } = "";

    [JsonPropertyName("server")]
    public string ServerId { get; set; } = "";

    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = "";

    [JsonPropertyName("account")]
    public string Account { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("bind_message_type")]
    public string? BindMessageType { get; set; }

    [JsonPropertyName("bind_group_id")]
    public long? BindGroupId { get; set; }
}
