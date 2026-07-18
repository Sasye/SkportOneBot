using System.Collections.Generic;
using System.Linq;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public static class GachaCalculator
{
    public static readonly Dictionary<string, string> PoolNameMap = new()
    {
        { "E_CharacterGachaPoolType_Special", "特许寻访" },
        { "E_CharacterGachaPoolType_Joint", "辉光庆典" },
        { "E_CharacterGachaPoolType_Standard", "基础寻访" },
        { "E_CharacterGachaPoolType_Beginner", "启程寻访" }
    };

    public static GachaStatistics AnalyzeCharPoolData(string poolKey, List<EndFieldCharInfo> rawData)
    {
        var data = rawData.ToList();
        data.Reverse();

        int count6 = 0, count5 = 0, count4 = 0, pityCount = 0;
        int paidPulls = 0, freePulls = 0;
        var historyRecords = new List<GachaHistoryRecord>();

        foreach (var item in data)
        {
            var isFree = item.IsFree ?? false;
            if (isFree) freePulls++;
            else
            {
                paidPulls++;
                pityCount++;
            }

            if (item.Rarity == 6)
            {
                count6++;
                historyRecords.Add(new GachaHistoryRecord
                {
                    Name = item.CharName,
                    Pity = isFree ? 0 : pityCount,
                    IsNew = item.IsNew,
                    IsFree = isFree,
                    PoolId = item.PoolId,
                    PoolName = string.IsNullOrEmpty(item.PoolName) ? poolKey : item.PoolName,
                    GachaTs = item.GachaTs,
                    SeqId = item.SeqId
                });

                if (!isFree) pityCount = 0;
            }
            else if (item.Rarity == 5) count5++;
            else if (item.Rarity == 4) count4++;
        }

        historyRecords.Reverse();

        return new GachaStatistics
        {
            PoolType = poolKey,
            PoolName = PoolNameMap.TryGetValue(poolKey, out var name) ? name : poolKey,
            TotalPulls = data.Count,
            PaidPulls = paidPulls,
            FreePulls = freePulls,
            PityCount = pityCount,
            Count6 = count6,
            Count5 = count5,
            Count4 = count4,
            History6 = historyRecords
        };
    }

    public static GachaStatistics AnalyzeWeaponPoolData(string poolId, string poolName, List<EndFieldWeaponInfo> rawData)
    {
        var data = rawData.ToList();
        data.Reverse();

        int count6 = 0, count5 = 0, count4 = 0, pityCount = 0;
        var historyRecords = new List<GachaHistoryRecord>();

        foreach (var item in data)
        {
            pityCount++;
            if (item.Rarity == 6)
            {
                count6++;
                historyRecords.Add(new GachaHistoryRecord
                {
                    Name = item.WeaponName,
                    Pity = pityCount,
                    IsNew = item.IsNew,
                    PoolId = item.PoolId,
                    PoolName = string.IsNullOrEmpty(item.PoolName) ? poolId : item.PoolName,
                    GachaTs = item.GachaTs,
                    SeqId = item.SeqId
                });
                pityCount = 0;
            }
            else if (item.Rarity == 5) count5++;
            else if (item.Rarity == 4) count4++;
        }

        historyRecords.Reverse();

        return new GachaStatistics
        {
            PoolId = poolId,
            PoolName = string.IsNullOrEmpty(poolName) ? poolId : poolName,
            TotalPulls = data.Count,
            PityCount = pityCount,
            Count6 = count6,
            Count5 = count5,
            Count4 = count4,
            History6 = historyRecords
        };
    }
}
