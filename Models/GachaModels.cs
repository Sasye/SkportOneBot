using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SkportOneBot.Models;

public class EndFieldCharInfo
{
    [JsonPropertyName("gachaTs")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long GachaTs { get; set; }

    [JsonPropertyName("charId")]
    public string CharId { get; set; } = "";

    [JsonPropertyName("charName")]
    public string CharName { get; set; } = "";

    [JsonPropertyName("rarity")]
    public int Rarity { get; set; }

    [JsonPropertyName("isNew")]
    public bool IsNew { get; set; }

    [JsonPropertyName("seqId")]
    public string SeqId { get; set; } = "";

    [JsonPropertyName("poolId")]
    public string PoolId { get; set; } = "";

    [JsonPropertyName("poolName")]
    public string PoolName { get; set; } = "";
    
    [JsonPropertyName("isFree")]
    public bool? IsFree { get; set; }
}

public class EndFieldWeaponInfo
{
    [JsonPropertyName("gachaTs")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long GachaTs { get; set; }

    [JsonPropertyName("weaponId")]
    public string WeaponId { get; set; } = "";

    [JsonPropertyName("weaponName")]
    public string WeaponName { get; set; } = "";

    [JsonPropertyName("rarity")]
    public int Rarity { get; set; }

    [JsonPropertyName("isNew")]
    public bool IsNew { get; set; }

    [JsonPropertyName("seqId")]
    public string SeqId { get; set; } = "";

    [JsonPropertyName("poolId")]
    public string PoolId { get; set; } = "";

    [JsonPropertyName("poolName")]
    public string PoolName { get; set; } = "";
}

public class GachaHistoryRecord
{
    public string Name { get; set; } = "";
    public int Pity { get; set; }
    public bool IsNew { get; set; }
    public bool IsFree { get; set; }
    public bool IsUp { get; set; }
    public string PoolId { get; set; } = "";
    public string PoolName { get; set; } = "";
    public string? Up6Id { get; set; }
    public long GachaTs { get; set; }
    public string SeqId { get; set; } = "";
}

public class GachaStatistics
{
    public string PoolType { get; set; } = "";
    public string PoolId { get; set; } = "";
    public string PoolName { get; set; } = "";
    public bool IsCurrentPool { get; set; }
    public int TotalPulls { get; set; }
    public int PaidPulls { get; set; }
    public int FreePulls { get; set; }
    public int PityCount { get; set; }
    public int BigPityMax { get; set; }
    public int BigPityCount { get; set; }
    public int BigPityRemaining { get; set; }
    public string? Up6Id { get; set; }
    public bool GotUp6 { get; set; }
    public int Count6 { get; set; }
    public int Count5 { get; set; }
    public int Count4 { get; set; }
    public List<GachaHistoryRecord> History6 { get; set; } = new();
}
