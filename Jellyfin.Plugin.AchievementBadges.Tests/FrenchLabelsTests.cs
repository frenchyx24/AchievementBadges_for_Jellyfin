using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.AchievementBadges.Helpers;
using Jellyfin.Plugin.AchievementBadges.Models;
using Jellyfin.Plugin.AchievementBadges.Services;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

/// <summary>French category labels, merged duplicates and goal actions for the stats routes.</summary>
public class FrenchLabelsTests
{
    [Fact]
    public void Every_defined_category_has_a_french_label()
    {
        var categories = AchievementDefinitions.All
            .Select(d => d.Category)
            .Distinct()
            .ToList();
        Assert.NotEmpty(categories);
        foreach (var c in categories)
        {
            Assert.True(FrenchLabels.CategoryMap.ContainsKey(c), "missing French label for category " + c);
        }
    }

    [Theory]
    [InlineData("Getting Started", "Premiers pas")]
    [InlineData("Morning Watching", "Matinée")]
    [InlineData("Challenge", "Défi")]
    [InlineData("Défi", "Défi")]
    [InlineData("Weekend Watching", "Week-end")]
    [InlineData("Week-end", "Week-end")]
    [InlineData("Total Time", "Temps total")]
    [InlineData("Temps total", "Temps total")]
    [InlineData("Genre Specialist", "Spécialiste de genre")]
    [InlineData("Spécialiste de genre", "Spécialiste de genre")]
    [InlineData(null, "Général")]
    public void Duplicates_land_on_one_label(string? category, string label) =>
        Assert.Equal(label, FrenchLabels.Category(category));

    [Theory]
    [InlineData("Défi", "Challenge")]
    [InlineData("Week-end", "Weekend Watching")]
    [InlineData("Temps total", "Total Time")]
    public void Category_key_stays_english(string label, string key) =>
        Assert.Equal(key, FrenchLabels.CategoryKey(label));

    [Fact]
    public void Labels_are_not_the_english_names()
    {
        var english = AchievementDefinitions.All.Select(d => d.Category).Distinct()
            .Where(c => FrenchLabels.CategoryMap[c] == c)
            .ToList();
        // Same word in both languages only.
        Assert.All(english, c => Assert.Contains(c, new[] { "Anime", "Combos", "Endurance", "Exploration", "Films", "Genres", "Prestige" }));
    }

    [Theory]
    [InlineData(AchievementMetric.TotalMinutesWatched, 3033, "Regardez encore 51 h")]
    [InlineData(AchievementMetric.SeriesCompleted, 1, "Terminez encore 1 série")]
    [InlineData(AchievementMetric.SeriesCompleted, 2, "Terminez encore 2 séries")]
    [InlineData(AchievementMetric.MoviesWatched, 3, "Regardez encore 3 films")]
    [InlineData(AchievementMetric.DaysLoggedIn, 4, "Encore 4")]
    public void Goal_actions_are_french(AchievementMetric metric, int remaining, string expected) =>
        Assert.Equal(expected, FrenchLabels.GoalAction(metric, remaining));

    [Fact]
    public void Goal_actions_have_no_english_words()
    {
        foreach (var m in System.Enum.GetValues<AchievementMetric>())
        {
            var a = FrenchLabels.GoalAction(m, 2);
            Assert.DoesNotMatch(new Regex(@"\b(Watch|more|Finish|Play|needed)\b"), a);
        }
    }
}
