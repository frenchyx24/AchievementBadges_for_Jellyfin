using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AchievementBadges.Services;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

public class SuccesV3MathTests
{
    [Theory]
    [InlineData(5, 2, 5, false, "won")]
    [InlineData(2, 5, 5, false, "lost")]
    [InlineData(5, 6, 5, false, "draw")]
    [InlineData(2, 3, 5, false, "active")]
    [InlineData(3, 2, 5, true, "won")]
    [InlineData(1, 4, 5, true, "lost")]
    [InlineData(0, 0, 5, true, "draw")]
    public void DuelOutcome_Math(long mine, long theirs, int target, bool ended, string expected)
        => Assert.Equal(expected, SuccesV3Math.DuelOutcome(mine, theirs, target, ended));

    [Fact]
    public void DuelStatus_PerViewer()
    {
        var now = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
        var d = new SuccesV3Duel { ChallengerId = "a", OpponentId = "b", CreatedAt = now.AddHours(-1) };
        Assert.Equal("pending", SuccesV3Math.DuelStatus(d, "a", now));
        Assert.True(SuccesV3Math.IsOpen(d, now));
        Assert.Equal("expired", SuccesV3Math.DuelStatus(d, "a", now.AddHours(80)));
        Assert.False(SuccesV3Math.IsOpen(d, now.AddHours(80)));
        d.StartsAt = now;
        d.EndsAt = now.AddDays(7);
        Assert.Equal("active", SuccesV3Math.DuelStatus(d, "b", now));
        d.Resolved = true;
        d.WinnerId = "a";
        Assert.Equal("won", SuccesV3Math.DuelStatus(d, "a", now));
        Assert.Equal("lost", SuccesV3Math.DuelStatus(d, "b", now));
        d.WinnerId = string.Empty;
        Assert.Equal("draw", SuccesV3Math.DuelStatus(d, "b", now));
        Assert.False(SuccesV3Math.IsOpen(d, now));
        Assert.Equal("declined", SuccesV3Math.DuelStatus(new SuccesV3Duel { Declined = true, CreatedAt = now }, "a", now));
    }

    [Fact]
    public void Progress_HoursFromMinutes_NeverNegative()
    {
        Assert.Equal(2, SuccesV3Math.Progress("hours", 150));
        Assert.Equal(3, SuccesV3Math.Progress("movies", 3));
        Assert.Equal(0, SuccesV3Math.Progress("episodes", -4));
    }

    [Fact]
    public void DuelLabel_French()
    {
        Assert.Equal("5 films en 7 jours", SuccesV3Math.DuelLabel("movies", 5, 7));
        Assert.Equal("1 épisode en 1 jour", SuccesV3Math.DuelLabel("episodes", 1, 1));
        Assert.Equal("10 heures en 14 jours", SuccesV3Math.DuelLabel("hours", 10, 14));
    }

    [Theory]
    [InlineData("2026-11-26T15:30:00", "2026-11-23T00:00:00")] // Thursday
    [InlineData("2026-11-23T00:00:00", "2026-11-23T00:00:00")] // Monday midnight
    [InlineData("2026-11-29T23:59:59", "2026-11-23T00:00:00")] // Sunday night
    [InlineData("2026-11-30T00:00:01", "2026-11-30T00:00:00")] // next Monday
    public void WeekStart_IsMondayMidnight(string now, string expected)
        => Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), SuccesV3Math.WeekStart(DateTime.Parse(now, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ToggleReaction_AddsRemovesAndRejects()
    {
        var map = new Dictionary<string, List<string>>();
        Assert.True(SuccesV3Math.ToggleReaction(map, "🔥", "u1"));
        Assert.True(SuccesV3Math.ToggleReaction(map, "🔥", "u2"));
        Assert.Equal(2, map["🔥"].Count);
        Assert.True(SuccesV3Math.ToggleReaction(map, "🔥", "u1"));
        Assert.Equal(new[] { "u2" }, map["🔥"]);
        Assert.True(SuccesV3Math.ToggleReaction(map, "🔥", "u2"));
        Assert.False(map.ContainsKey("🔥"));
        Assert.False(SuccesV3Math.ToggleReaction(map, "💩", "u1"));
        Assert.Empty(map);
    }
}
