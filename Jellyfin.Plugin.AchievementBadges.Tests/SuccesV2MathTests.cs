using System;
using System.IO;
using Jellyfin.Plugin.AchievementBadges.Helpers;
using Jellyfin.Plugin.AchievementBadges.Models;
using Jellyfin.Plugin.AchievementBadges.Services;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

public class SuccesV2MathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(33, 1)]
    [InlineData(34, 2)]
    [InlineData(66, 2)]
    [InlineData(67, 3)]
    [InlineData(100, 3)]
    public void HintLevel_Thresholds(int percent, int expected)
        => Assert.Equal(expected, SuccesV2Math.HintLevel(percent));

    [Fact]
    public void Percent_FloorsAndClamps()
    {
        Assert.Equal(33, SuccesV2Math.Percent(1, 3));
        Assert.Equal(66, SuccesV2Math.Percent(2, 3));
        Assert.Equal(100, SuccesV2Math.Percent(9, 3));
        Assert.Equal(0, SuccesV2Math.Percent(-1, 3));
        Assert.Equal(0, SuccesV2Math.Percent(0, 0));
    }

    [Fact]
    public void HintText_GetsMorePreciseAndOnlyLastShowsTarget()
    {
        var l1 = SuccesV2Math.HintText(AchievementMetric.LateNightSessions, 25, 1);
        var l2 = SuccesV2Math.HintText(AchievementMetric.LateNightSessions, 25, 2);
        var l3 = SuccesV2Math.HintText(AchievementMetric.LateNightSessions, 25, 3);
        Assert.DoesNotContain("25", l1);
        Assert.DoesNotContain("25", l2);
        Assert.Contains("25", l3);
        Assert.NotEqual(l1, l2);
        Assert.DoesNotContain("{n}", SuccesV2Math.HintText(AchievementMetric.WatchedOnHalloween, 1, 3));
        foreach (AchievementMetric m in Enum.GetValues(typeof(AchievementMetric)))
        {
            for (var l = 0; l <= 3; l++)
            {
                var t = SuccesV2Math.HintText(m, 10, l);
                Assert.False(string.IsNullOrWhiteSpace(t));
                Assert.DoesNotContain("{n}", t);
            }
        }
    }

    [Theory]
    [InlineData(2026, 11, 26, "2026-automne", "Automne", 2026, 9, 2026, 12)]
    [InlineData(2026, 12, 1, "2026-hiver", "Hiver", 2026, 12, 2027, 3)]
    [InlineData(2027, 2, 28, "2026-hiver", "Hiver", 2026, 12, 2027, 3)]
    [InlineData(2028, 2, 29, "2027-hiver", "Hiver", 2027, 12, 2028, 3)]
    [InlineData(2027, 3, 1, "2027-printemps", "Printemps", 2027, 3, 2027, 6)]
    [InlineData(2027, 8, 31, "2027-ete", "Été", 2027, 6, 2027, 9)]
    public void SeasonAt_IdAndWindow(int y, int m, int d, string id, string name, int sy, int sm, int ey, int em)
    {
        var s = SuccesV2Math.SeasonAt(new DateTime(y, m, d, 23, 59, 0, DateTimeKind.Utc));
        Assert.Equal(id, s.Id);
        Assert.Equal(name, s.Name);
        Assert.Equal(new DateTime(sy, sm, 1, 0, 0, 0, DateTimeKind.Utc), s.Start);
        Assert.Equal(new DateTime(ey, em, 1, 0, 0, 0, DateTimeKind.Utc), s.End);
    }

    [Fact]
    public void InSeason_StartInclusiveEndExclusive()
    {
        var s = SuccesV2Math.SeasonAt(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(SuccesV2Math.InSeason(s, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(SuccesV2Math.InSeason(s, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(SuccesV2Math.InSeason(s, new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(199, 0)]
    [InlineData(200, 1)]
    [InlineData(5999, 29)]
    [InlineData(6000, 30)]
    [InlineData(100000, 30)]
    [InlineData(-5, 0)]
    public void TierForXp(long xp, int tier) => Assert.Equal(tier, SuccesV2Math.TierForXp(xp));

    [Fact]
    public void TierRewards_TitleEvery5_FrameOtherwise()
    {
        var s = SuccesV2Math.SeasonAt(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(200, SuccesV2Math.XpRequiredForTier(1));
        Assert.Equal(6000, SuccesV2Math.XpRequiredForTier(30));
        var t5 = SuccesV2Math.TierReward(s, 5);
        Assert.Equal("title", t5.Type);
        Assert.Equal("2026-automne-titre-5", t5.Id);
        Assert.Equal("Explorateur de l'automne 2026", t5.Label);
        var t7 = SuccesV2Math.TierReward(s, 7);
        Assert.Equal("frame", t7.Type);
        Assert.Equal("2026-automne-cadre-7", t7.Id);
        Assert.Contains("du printemps", SuccesV2Math.TierReward(SuccesV2Math.SeasonAt(new DateTime(2027, 4, 1)), 10).Label);
    }

    [Theory]
    [InlineData("2026-11-26", 2026, 11, 26)]
    [InlineData("2026-W48", 2026, 11, 23)]
    [InlineData("2027-W01", 2027, 1, 4)]
    public void QuestPeriodStart_Parses(string period, int y, int m, int d)
        => Assert.Equal(new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc), SuccesV2Math.QuestPeriodStart(period));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-W99")]
    [InlineData("garbage")]
    public void QuestPeriodStart_RejectsBad(string? period) => Assert.Null(SuccesV2Math.QuestPeriodStart(period));

    [Fact]
    public void LevelHelper_ExtraXpAddsToLevel()
    {
        var none = LevelHelper.FromBadges(Array.Empty<AchievementBadge>(), 0);
        var bonus = LevelHelper.FromBadges(Array.Empty<AchievementBadge>(), 150);
        Assert.Equal(0, none.Xp);
        Assert.Equal(150, bonus.Xp);
        Assert.Equal(2, bonus.Level);
    }

    [Fact]
    public void Store_PersistsAcrossInstances()
    {
        var dir = Path.Combine(Path.GetTempPath(), "succesv2-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "succes-v2.json");
        try
        {
            var uid = Guid.NewGuid();
            new SuccesV2Store(path).Update(uid.ToString(), s => { s.EquippedFrame = "cadre-niveau-5"; return (0, true); });
            var frame = new SuccesV2Store(path).Update(uid.ToString("N"), s => (s.EquippedFrame, false));
            Assert.Equal("cadre-niveau-5", frame);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
