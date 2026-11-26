using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AchievementBadges.Models;

namespace Jellyfin.Plugin.AchievementBadges.Helpers;

/// <summary>Level and XP state derived from unlocked badges.</summary>
public sealed record NflixLevelState(int Level, long Xp, long XpIntoLevel, long XpForNextLevel, string Title);

/// <summary>
/// XP per unlocked badge by rarity, and the level curve: the cumulative XP
/// needed to finish level n (reach n + 1) is 50 * n * (n + 1). Level 1 starts
/// at 0 XP, level 2 at 100, level 3 at 300, level 4 at 600...
/// </summary>
public static class LevelHelper
{
    private static readonly Dictionary<string, int> RarityXp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Common"] = 10,
        ["Uncommon"] = 25,
        ["Rare"] = 50,
        ["Epic"] = 100,
        ["Legendary"] = 250,
        ["Mythic"] = 500,
    };

    /// <summary>XP granted by one unlocked badge of the given rarity (unknown rarity counts as Common).</summary>
    public static int XpForRarity(string? rarity)
        => rarity is not null && RarityXp.TryGetValue(rarity, out var xp) ? xp : RarityXp["Common"];

    /// <summary>Cumulative XP needed to finish level n: 50 * n * (n + 1).</summary>
    public static long XpForLevel(int n) => n <= 0 ? 0 : 50L * n * (n + 1);

    /// <summary>Total XP of the unlocked badges in the list.</summary>
    public static long TotalXp(IEnumerable<AchievementBadge> badges)
    {
        long xp = 0;
        foreach (var b in badges)
        {
            if (b is not null && b.Unlocked) xp += XpForRarity(b.Rarity);
        }

        return xp;
    }

    /// <summary>French title for a level.</summary>
    public static string TitleFor(int level) => level switch
    {
        >= 50 => "Légende",
        >= 35 => "Maître du binge",
        >= 20 => "Critique",
        >= 10 => "Cinéphile",
        >= 5 => "Habitué",
        _ => "Spectateur",
    };

    /// <summary>Level state for a total XP amount.</summary>
    public static NflixLevelState FromXp(long xp)
    {
        if (xp < 0) xp = 0;
        var level = 1;
        while (XpForLevel(level) <= xp) level++;
        var floor = XpForLevel(level - 1);
        var ceil = XpForLevel(level);
        return new NflixLevelState(level, xp, xp - floor, ceil - floor, TitleFor(level));
    }

    /// <summary>Level state for a badge list.</summary>
    public static NflixLevelState FromBadges(IEnumerable<AchievementBadge> badges) => FromXp(TotalXp(badges));
}
