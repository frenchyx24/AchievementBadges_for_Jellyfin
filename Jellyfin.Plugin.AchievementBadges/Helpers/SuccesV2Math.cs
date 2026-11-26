using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.AchievementBadges.Models;

namespace Jellyfin.Plugin.AchievementBadges.Helpers;

/// <summary>Meteorological season window (UTC): Start inclusive, End exclusive.</summary>
public sealed record NflixSeasonWindow(string Id, string Name, DateTime Start, DateTime End);

/// <summary>Cosmetic reward of a season-pass tier.</summary>
public sealed record NflixSeasonReward(string Type, string Id, string Label);

/// <summary>
/// Pure math for the "Succès v2" features: secret-badge hint levels,
/// season windows, season-pass tiers and level-based cosmetics.
/// </summary>
public static class SuccesV2Math
{
    /// <summary>Number of tiers in a season pass.</summary>
    public const int SeasonTierCount = 30;

    /// <summary>XP needed per season-pass tier.</summary>
    public const int SeasonXpPerTier = 500;

    /// <summary>Bonus XP granted once per completed collection.</summary>
    public const int CollectionXp = 50;

    /// <summary>Levels unlocking a frame and a title.</summary>
    public static readonly IReadOnlyList<int> LevelThresholds = new[] { 5, 10, 20, 35, 50 };

    private static readonly IReadOnlyDictionary<int, string> LevelFrameNames = new Dictionary<int, string>
    {
        [5] = "Bronze",
        [10] = "Argent",
        [20] = "Or",
        [35] = "Platine",
        [50] = "Diamant",
    };

    /// <summary>Progress percent, floored and clamped to 0..100.</summary>
    public static int Percent(int current, int target)
    {
        if (target <= 0) return current > 0 ? 100 : 0;
        if (current <= 0) return 0;
        return Math.Clamp((int)Math.Floor(100.0 * current / target), 0, 100);
    }

    /// <summary>0 = no progress, 1 = 1-33 %, 2 = 34-66 %, 3 = 67 %+.</summary>
    public static int HintLevel(int percent) => percent switch
    {
        <= 0 => 0,
        <= 33 => 1,
        <= 66 => 2,
        _ => 3,
    };

    /// <summary>French hint for a hidden badge; never contains the badge title.</summary>
    public static string HintText(AchievementMetric metric, int target, int level)
    {
        if (level <= 0) return "Un secret bien gardé… Continue d'explorer Nflix pour lever le voile.";
        var (vague, precise, nearly) = Templates(metric);
        return level switch
        {
            1 => vague,
            2 => precise,
            _ => nearly.Replace("{n}", Math.Max(target, 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
        };
    }

    private static (string Vague, string Precise, string Nearly) Templates(AchievementMetric metric)
    {
        switch (metric)
        {
            case AchievementMetric.TotalItemsWatched:
                return ("Tout commence par appuyer sur lecture.", "Ce secret récompense le nombre total de visionnages.", "Atteins {n} visionnages au total.");
            case AchievementMetric.MoviesWatched:
                return ("Le grand écran t'appelle.", "Les films sont la clé de ce secret.", "Cumule {n} films.");
            case AchievementMetric.MaxMoviesInSingleDay:
                return ("Le grand écran t'appelle.", "Plusieurs films le même jour, ça compte.", "Enchaîne {n} films dans la même journée.");
            case AchievementMetric.MaxEpisodesInSingleDay:
                return ("Un épisode, puis un autre…", "Les épisodes enchaînés le même jour comptent.", "Regarde {n} épisodes en une seule journée.");
            case AchievementMetric.SeriesCompleted:
            case AchievementMetric.LongSeriesCompleted:
            case AchievementMetric.VeryLongSeriesCompleted:
            case AchievementMetric.SeriesBingedAfterPilot:
                return ("Va jusqu'au bout des choses.", "Terminer des séries fait avancer ce secret.", "Termine {n} séries.");
            case AchievementMetric.LateNightSessions:
                return ("La nuit porte conseil.", "Les séances tardives comptent ici.", "Fais {n} séances en pleine nuit.");
            case AchievementMetric.EarlyMorningSessions:
                return ("Le monde appartient à ceux qui se lèvent tôt.", "Les séances matinales comptent ici.", "Fais {n} séances au petit matin.");
            case AchievementMetric.AfternoonSessions:
            case AchievementMetric.PrimeTimeSessions:
                return ("Chaque heure a son charme.", "Le moment de la journée où tu regardes compte.", "Fais {n} séances à ce moment précis de la journée.");
            case AchievementMetric.WeekendSessions:
                return ("Certains jours sont faits pour se détendre.", "Le week-end joue un rôle ici.", "Fais {n} séances le week-end.");
            case AchievementMetric.CurrentWatchStreak:
            case AchievementMetric.BestWatchStreak:
                return ("La régularité paie.", "Revenir jour après jour fait avancer ce secret.", "Tiens une série de {n} jours d'affilée.");
            case AchievementMetric.DaysWatched:
                return ("La régularité paie.", "Le nombre de jours où tu regardes compte.", "Regarde quelque chose sur {n} jours différents.");
            case AchievementMetric.DaysLoggedIn:
            case AchievementMetric.CurrentLoginStreak:
            case AchievementMetric.BestLoginStreak:
                return ("Passe dire bonjour plus souvent.", "Tes connexions comptent pour ce secret.", "Connecte-toi {n} jours.");
            case AchievementMetric.TotalMinutesWatched:
            case AchievementMetric.MaxMinutesInSingleDay:
                return ("Le temps file quand on aime.", "Le temps passé devant l'écran compte ici.", "Cumule {n} minutes de visionnage.");
            case AchievementMetric.LongestItemMinutes:
                return ("Certaines œuvres prennent leur temps.", "La durée d'un seul titre compte ici.", "Regarde un titre d'au moins {n} minutes.");
            case AchievementMetric.ShortItemsWatched:
                return ("Petit format, grand plaisir.", "Les contenus courts comptent ici.", "Regarde {n} contenus courts.");
            case AchievementMetric.RewatchCount:
                return ("Certaines histoires méritent d'être revues.", "Revoir un titre fait avancer ce secret.", "Revois des titres {n} fois.");
            case AchievementMetric.PrestigeLevel:
            case AchievementMetric.LifetimeScore:
            case AchievementMetric.BadgesUnlockedPercent:
            case AchievementMetric.BestComboCount:
                return ("Ta progression globale compte.", "Ton parcours de succès est la clé.", "Atteins {n} dans ta progression globale.");
        }

        var name = metric.ToString();
        if (name.StartsWith("WatchedOn", StringComparison.Ordinal))
            return ("Certains jours ne sont pas comme les autres.", "Une date de fête est liée à ce secret.", "Regarde quelque chose le jour de cette fête ({n} fois).");
        if (name.Contains("Music", StringComparison.Ordinal))
            return ("Tends l'oreille.", "La musique est liée à ce secret.", "Atteins {n} côté musique.");
        if (name.Contains("Book", StringComparison.Ordinal))
            return ("Les mots ont leur pouvoir.", "Livres et livres audio comptent ici.", "Atteins {n} côté lecture.");
        if (name.Contains("Game", StringComparison.Ordinal))
            return ("Prends la manette.", "Les jeux comptent pour ce secret.", "Atteins {n} côté jeux.");
        if (name.Contains("Completion", StringComparison.Ordinal))
            return ("Les complétistes seront récompensés.", "Compléter un ensemble de la bibliothèque compte ici.", "Complète {n} % d'un ensemble.");
        if (name.StartsWith("Unique", StringComparison.Ordinal))
            return ("La variété est le sel de la vie.", "Diversifier tes visionnages fait avancer ce secret.", "Explore {n} horizons différents.");
        if (name.Contains("Genre", StringComparison.Ordinal) || name.Contains("Studio", StringComparison.Ordinal)
            || name.Contains("Person", StringComparison.Ordinal) || name.Contains("Director", StringComparison.Ordinal)
            || name.Contains("Actor", StringComparison.Ordinal) || name.Contains("Decade", StringComparison.Ordinal)
            || name.Contains("Anime", StringComparison.Ordinal) || name.Contains("DayOfWeek", StringComparison.Ordinal))
            return ("Tes goûts en disent long.", "Un thème précis est lié à ce secret.", "Regarde {n} titres de ce thème.");
        return ("Un mystère t'attend…", "Ta façon de regarder compte pour ce secret.", "Tu y es presque : objectif {n}.");
    }

    /// <summary>Season containing the instant: Hiver Dec-Feb, Printemps Mar-May, Été Jun-Aug, Automne Sep-Nov.</summary>
    public static NflixSeasonWindow SeasonAt(DateTime now)
    {
        var d = now.Kind == DateTimeKind.Local ? now.ToUniversalTime() : now;
        var y = d.Year;
        var (key, name, startYear, startMonth) = d.Month switch
        {
            12 => ("hiver", "Hiver", y, 12),
            1 or 2 => ("hiver", "Hiver", y - 1, 12),
            >= 3 and <= 5 => ("printemps", "Printemps", y, 3),
            >= 6 and <= 8 => ("ete", "Été", y, 6),
            _ => ("automne", "Automne", y, 9),
        };
        var start = new DateTime(startYear, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        return new NflixSeasonWindow(
            startYear.ToString(CultureInfo.InvariantCulture) + "-" + key,
            name,
            start,
            start.AddMonths(3));
    }

    /// <summary>True when the instant falls in [Start, End).</summary>
    public static bool InSeason(NflixSeasonWindow season, DateTimeOffset at)
    {
        var u = at.UtcDateTime;
        return u >= season.Start && u < season.End;
    }

    /// <summary>Cumulative season XP needed for a tier (tier * 200).</summary>
    public static long XpRequiredForTier(int tier) => (long)Math.Clamp(tier, 0, SeasonTierCount) * SeasonXpPerTier;

    /// <summary>Highest tier reached with the XP, 0..30.</summary>
    public static int TierForXp(long xp) => xp <= 0 ? 0 : (int)Math.Min(SeasonTierCount, xp / SeasonXpPerTier);

    /// <summary>Reward of a tier: a title every 5 tiers, a frame otherwise.</summary>
    public static NflixSeasonReward TierReward(NflixSeasonWindow season, int tier)
    {
        var t = tier.ToString(CultureInfo.InvariantCulture);
        var year = season.Start.Year.ToString(CultureInfo.InvariantCulture);
        if (tier % 5 == 0)
        {
            var rank = tier switch
            {
                5 => "Explorateur",
                10 => "Assidu",
                15 => "Passionné",
                20 => "Vétéran",
                25 => "Virtuose",
                _ => "Champion",
            };
            var of = season.Name == "Printemps" ? "du printemps" : "de l'" + season.Name.ToLowerInvariant();
            return new NflixSeasonReward("title", season.Id + "-titre-" + t, rank + " " + of + " " + year);
        }

        return new NflixSeasonReward("frame", season.Id + "-cadre-" + t, "Cadre " + season.Name + " " + year + " · palier " + t);
    }

    /// <summary>Frame id unlocked at a level threshold.</summary>
    public static string LevelFrameId(int level) => "cadre-niveau-" + level.ToString(CultureInfo.InvariantCulture);

    /// <summary>Frame label for a level threshold.</summary>
    public static string LevelFrameLabel(int level)
        => "Cadre " + (LevelFrameNames.TryGetValue(level, out var n) ? n : "Niveau") + " (niveau " + level.ToString(CultureInfo.InvariantCulture) + ")";

    /// <summary>Title id unlocked at a level threshold.</summary>
    public static string LevelTitleId(int level) => "titre-niveau-" + level.ToString(CultureInfo.InvariantCulture);

    /// <summary>Start (UTC) of a quest period: "yyyy-MM-dd" or ISO week "yyyy-Www".</summary>
    public static DateTime? QuestPeriodStart(string? period)
    {
        if (string.IsNullOrEmpty(period)) return null;
        if (DateTime.TryParseExact(period, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        var i = period.IndexOf("-W", StringComparison.Ordinal);
        if (i > 0
            && int.TryParse(period.AsSpan(0, i), NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            && int.TryParse(period.AsSpan(i + 2), NumberStyles.None, CultureInfo.InvariantCulture, out var w)
            && y >= 1 && y <= 9998 && w >= 1 && w <= ISOWeek.GetWeeksInYear(y))
        {
            return DateTime.SpecifyKind(ISOWeek.ToDateTime(y, w, DayOfWeek.Monday), DateTimeKind.Utc);
        }

        return null;
    }
}
