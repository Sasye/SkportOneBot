using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkportOneBot.Models;

namespace SkportOneBot.Services;

public class GachaImageRenderer
{
    private readonly FontFamily _fontFamily;

    public GachaImageRenderer()
    {
        var sysFonts = SystemFonts.Collection.Families.ToList();
        _fontFamily = sysFonts.FirstOrDefault(f => f.Name.Contains("YaHei") || f.Name.Contains("Hei"));
        if (_fontFamily == default) _fontFamily = sysFonts.FirstOrDefault();
    }

    public string RenderAnalysisImage(string uid, string accountName, List<GachaStatistics> charStats, List<GachaStatistics> weaponStats)
    {
        int width = 800;
        int currentY = 20;

        var totalCharPulls = charStats.Sum(x => x.TotalPulls);
        var totalChar6 = charStats.Sum(x => x.Count6);
        var avgCharPulls = totalChar6 > 0 ? (double)totalCharPulls / totalChar6 : 0;

        var totalWeaponPulls = weaponStats.Sum(x => x.TotalPulls);
        var totalWeapon6 = weaponStats.Sum(x => x.Count6);
        var avgWeaponPulls = totalWeapon6 > 0 ? (double)totalWeaponPulls / totalWeapon6 : 0;

        // Calculate height dynamically
        int height = 250; // Base height for header
        
        foreach (var stat in charStats)
        {
            if (stat.TotalPulls == 0) continue;
            int rows = Math.Max(1, (stat.History6.Count + 5) / 6);
            height += 90 + (rows * 125) + 20; // Title/subtitle (90) + Grid + Padding (20)
        }

        foreach (var stat in weaponStats)
        {
            if (stat.TotalPulls == 0) continue;
            int rows = Math.Max(1, (stat.History6.Count + 5) / 6);
            height += 90 + (rows * 125) + 20;
        }

        height += 50; // Bottom padding

        using var image = new Image<Rgba32>(width, height);
        
        // Background
        image.Mutate(x => x.Fill(Color.ParseHex("#121212")));

        var fontTitle = _fontFamily.CreateFont(36, FontStyle.Bold);
        var fontSubtitle = _fontFamily.CreateFont(24, FontStyle.Bold);
        var fontNormal = _fontFamily.CreateFont(20, FontStyle.Regular);
        var fontSmall = _fontFamily.CreateFont(14, FontStyle.Regular);

        image.Mutate(ctx =>
        {
            // Header
            ctx.DrawText($"UID: {uid}", fontNormal, Color.White, new PointF(20, currentY));
            currentY += 30;
            
            ctx.DrawText(GetTitle(avgCharPulls, totalChar6), fontTitle, Color.ParseHex("#FFD700"), new PointF(20, currentY));
            currentY += 60;

            // Summary Text
            ctx.DrawText($"角色总抽: {totalCharPulls} 抽 (平均每红: {avgCharPulls:F1} 抽 | 六星总数: {totalChar6} 红)", fontNormal, Color.White, new PointF(20, currentY));
            currentY += 30;
            ctx.DrawText($"武器总抽: {totalWeaponPulls} 抽 (平均每金: {avgWeaponPulls:F1} 抽 | 六星总数: {totalWeapon6} 金)", fontNormal, Color.ParseHex("#00BFFF"), new PointF(20, currentY));
            currentY += 50;

            // Draw Section Helper
            void DrawPoolSection(GachaStatistics stat, Color titleColor)
            {
                if (stat.TotalPulls == 0) return;

                int rows = Math.Max(1, (stat.History6.Count + 5) / 6);
                int sectionHeight = 90 + (rows * 125);

                // Section background
                ctx.Fill(Color.ParseHex("#1E1E1E"), new RectangleF(10, currentY, width - 20, sectionHeight));
                currentY += 20;

                // Title
                ctx.DrawText(stat.PoolName, fontSubtitle, titleColor, new PointF(20, currentY));
                currentY += 35;

                // Subtitle
                ctx.DrawText($"共计 {stat.TotalPulls} 抽   已垫 {stat.PityCount} 抽", fontNormal, Color.ParseHex("#CCCCCC"), new PointF(20, currentY));
                currentY += 35;

                int startX = 20;
                int boxWidth = 110;
                int boxHeight = 110;
                int padding = 15;
                int row = 0, col = 0;

                if (stat.History6.Count == 0)
                {
                    ctx.DrawText("暂无六星记录", fontNormal, Color.Gray, new PointF(startX, currentY + 30));
                    currentY += boxHeight + padding + 10;
                    return;
                }

                foreach (var item in stat.History6)
                {
                    int x = startX + col * (boxWidth + padding);
                    int y = currentY + row * (boxHeight + padding);

                    ctx.Fill(Color.ParseHex("#2A2A2A"), new RectangleF(x, y, boxWidth, boxHeight));
                    
                    var nameSize = TextMeasurer.MeasureBounds(item.Name, new TextOptions(fontSmall));
                    ctx.DrawText(item.Name, fontSmall, Color.White, new PointF(x + boxWidth / 2 - nameSize.Width / 2, y + boxHeight / 2 - 15 - nameSize.Height / 2));

                    var pityColor = item.Pity <= 30 ? Color.ParseHex("#FFD700") : 
                                    item.Pity >= 70 ? Color.ParseHex("#FF4500") : Color.ParseHex("#333333");
                    
                    ctx.Fill(Color.White, new RectangleF(x, y + boxHeight - 30, boxWidth, 30));
                    
                    var pityText = item.Pity.ToString();
                    var pitySize = TextMeasurer.MeasureBounds(pityText, new TextOptions(fontNormal));
                    ctx.DrawText(pityText, fontNormal, pityColor, new PointF(x + boxWidth / 2 - pitySize.Width / 2, y + boxHeight - 15 - pitySize.Height / 2));

                    col++;
                    if (col >= 6)
                    {
                        col = 0;
                        row++;
                    }
                }
                currentY += (rows * (boxHeight + padding)) + 10;
            }

            foreach (var stat in charStats)
            {
                DrawPoolSection(stat, Color.ParseHex("#FFD700"));
            }

            foreach (var stat in weaponStats)
            {
                DrawPoolSection(stat, Color.ParseHex("#00BFFF"));
            }
        });

        var filePath = Path.Combine(Path.GetTempPath(), $"gacha_{uid}_{DateTime.Now.Ticks}.png");
        image.SaveAsPng(filePath);
        return filePath;
    }

    private string GetTitle(double avg, int total)
    {
        if (total == 0) return "纯净无暇 (0红)";
        if (avg <= 35) return "欧气满满大欧皇";
        if (avg <= 50) return "小有运气的欧皇";
        if (avg <= 65) return "普普通通一般人";
        return "非酋本酋";
    }
}
