using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AchievementBadges.Helpers;
using Jellyfin.Plugin.AchievementBadges.Models;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

/// <summary>XP per rarity, the 50*n*(n+1) level curve, French titles and seasons.</summary>
public class LevelHelperTests
{
    [Theory]
    [InlineData("Common", 10)]
    [InlineData("Uncommon", 25)]
    [InlineData("Rare", 50)]
    [InlineData("Epic", 100)]
    [InlineData("Legendary", 250)]
    [InlineData("Mythic", 500)]
    [InlineData("mythic", 500)]
    [InlineData("Unknown", 10)]
    [InlineData(null, 10)]
    public void Xp_per_rarity(string? rarity, int xp) => Assert.Equal(xp, LevelHelper.XpForRarity(rarity));

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(3, 600)]
    [InlineData(9, 4500)]
    public void Curve_is_cumulative(int n, long xp) => Assert.Equal(xp, LevelHelper.XpForLevel(n));

    [Theory]
    [InlineData(0, 1, 0, 100)]
    [InlineData(99, 1, 99, 100)]
    [InlineData(100, 2, 0, 200)]
    [InlineData(299, 2, 199, 200)]
    [InlineData(300, 3, 0, 300)]
    [InlineData(4499, 9, 899, 900)]
    [InlineData(4500, 10, 0, 1000)]
    public void Level_boundaries(long xp, int level, long into, long span)
    {
        var info = LevelHelper.FromXp(xp);
        Assert.Equal(level, info.Level);
        Assert.Equal(xp, info.Xp);
        Assert.Equal(into, info.XpIntoLevel);
        Assert.Equal(span, info.XpForNextLevel);
    }

    [Fact]
    public void Negative_xp_is_level_one() => Assert.Equal(1, LevelHelper.FromXp(-5).Level);

    [Theory]
    [InlineData(1, "Spectateur")]
    [InlineData(4, "Spectateur")]
    [InlineData(5, "Habitué")]
    [InlineData(9, "Habitué")]
    [InlineData(10, "Cinéphile")]
    [InlineData(19, "Cinéphile")]
    [InlineData(20, "Critique")]
    [InlineData(34, "Critique")]
    [InlineData(35, "Maître du binge")]
    [InlineData(49, "Maître du binge")]
    [InlineData(50, "Légende")]
    [InlineData(120, "Légende")]
    public void Titles(int level, string title) => Assert.Equal(title, LevelHelper.TitleFor(level));

    [Fact]
    public void Title_follows_level_from_xp()
    {
        // Level 50 starts at 50*49*50 = 122500 XP.
        Assert.Equal("Maître du binge", LevelHelper.FromXp(122499).Title);
        Assert.Equal("Légende", LevelHelper.FromXp(122500).Title);
    }

    [Fact]
    public void Only_unlocked_badges_count()
    {
        var badges = new List<AchievementBadge>
        {
            new() { Rarity = "Rare", Unlocked = true },
            new() { Rarity = "Mythic", Unlocked = true },
            new() { Rarity = "Legendary", Unlocked = false },
        };
        Assert.Equal(550, LevelHelper.TotalXp(badges));
        Assert.Equal(3, LevelHelper.FromBadges(badges).Level);
    }

    [Theory]
    [InlineData(2026, 10, 14, null, null)]
    [InlineData(2026, 10, 15, "Halloween", "2026-11-03")]
    [InlineData(2026, 11, 2, "Halloween", "2026-11-03")]
    [InlineData(2026, 11, 3, null, null)]
    [InlineData(2026, 12, 9, null, null)]
    [InlineData(2026, 12, 10, "Noël", "2027-01-03")]
    [InlineData(2027, 1, 2, "Noël", "2027-01-03")]
    [InlineData(2027, 1, 3, null, null)]
    [InlineData(2026, 6, 20, null, null)]
    [InlineData(2026, 6, 21, "Été", "2026-09-01")]
    [InlineData(2026, 8, 31, "Été", "2026-09-01")]
    [InlineData(2026, 9, 1, null, null)]
    public void Seasons(int y, int m, int d, string? name, string? endsAt)
    {
        var s = SeasonHelper.At(new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(name is not null, s.Active);
        Assert.Equal(name, s.Name);
        Assert.Equal(endsAt, s.EndsAt?.ToString("yyyy-MM-dd"));
    }
}
