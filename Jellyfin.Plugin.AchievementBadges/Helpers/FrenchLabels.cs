using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.AchievementBadges.Models;

namespace Jellyfin.Plugin.AchievementBadges.Helpers;

/// <summary>
/// French display labels for values whose English form stays the API
/// contract (rarity and rank tier names). Clients keep mapping the English
/// value; these labels are an extra, read-only convenience.
/// </summary>
public static class FrenchLabels
{
    public static string Rarity(string? rarity) => (rarity ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "common" => "Commun",
        "uncommon" => "Peu commun",
        "rare" => "Rare",
        "epic" => "Épique",
        "legendary" => "Légendaire",
        "mythic" => "Mythique",
        _ => rarity ?? string.Empty
    };

    public static string Tier(string? name) => name switch
    {
        "Rookie" => "Débutant",
        "Novice" => "Novice",
        "Viewer" => "Spectateur",
        "Regular" => "Habitué",
        "Enthusiast" => "Passionné",
        "Binger" => "Boulimique",
        "Connoisseur" => "Connaisseur",
        "Maestro" => "Maestro",
        "Legend" => "Légende",
        "Immortal" => "Immortel",
        _ => name ?? string.Empty
    };

    /// <summary>
    /// French label for every badge category in <see cref="AchievementDefinitions"/>.
    /// Profiles saved by older builds can carry a French category name for
    /// the same group ("Défi" next to "Challenge"), so the French labels map
    /// to themselves too: both spellings land on one label and one row.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CategoryMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Afternoon Watching"] = "Après-midi",
            ["Anime"] = "Anime",
            ["Best Streaks"] = "Meilleures séries",
            ["Binge"] = "Boulimie",
            ["Books"] = "Livres",
            ["Challenge"] = "Défi",
            ["Combos"] = "Combos",
            ["Custom"] = "Personnalisé",
            ["Decades"] = "Décennies",
            ["Discography"] = "Discographie",
            ["Endurance"] = "Endurance",
            ["Episode Marathons"] = "Marathons d'épisodes",
            ["Eras"] = "Époques",
            ["Exploration"] = "Exploration",
            ["Film Marathons"] = "Marathons de films",
            ["Films"] = "Films",
            ["Games"] = "Jeux",
            ["General"] = "Général",
            ["Genre Specialist"] = "Spécialiste de genre",
            ["Genres"] = "Genres",
            ["Getting Started"] = "Premiers pas",
            ["Hidden"] = "Cachés",
            ["Holidays"] = "Fêtes",
            ["Languages"] = "Langues",
            ["Library Completion"] = "Complétion de bibliothèque",
            ["Loyalty"] = "Fidélité",
            ["Meta"] = "Méta",
            ["Morning Watching"] = "Matinée",
            ["Music"] = "Musique",
            ["Night Watching"] = "Nuit",
            ["People"] = "Personnalités",
            ["Pilot vs Completer"] = "Pilote ou finisseur",
            ["Prestige"] = "Prestige",
            ["Prime Time"] = "Heure de grande écoute",
            ["Rewatch"] = "Revisionnage",
            ["Runtime"] = "Durée",
            ["Score Economy"] = "Économie de score",
            ["Series"] = "Séries",
            ["Streaks"] = "Séries de jours",
            ["Studio Specialist"] = "Spécialiste de studio",
            ["Total Time"] = "Temps total",
            ["Weekdays"] = "Jours de semaine",
            ["Weekend Watching"] = "Week-end",
            ["World"] = "Monde",
            // French spellings seen in stored profiles and older translations.
            ["Week-end"] = "Week-end",
            ["Visionnage de weekend"] = "Week-end",
            ["Visionnage matinal"] = "Matinée",
            ["Visionnage nocturne"] = "Nuit",
            ["Visionnage l'après-midi"] = "Après-midi",
            ["Marathon"] = "Boulimie",
            ["Caché"] = "Cachés",
            ["Personnes"] = "Personnalités",
            ["Séries consécutives"] = "Séries de jours",
            ["Complétion bibliothèque"] = "Complétion de bibliothèque",
            ["Pilote vs Finisseur"] = "Pilote ou finisseur",
            ["Heures de grande écoute"] = "Heure de grande écoute",
            ["Spécialiste des studios"] = "Spécialiste de studio",
        };

    private static readonly HashSet<string> _categoryLabels =
        new(CategoryMap.Values, StringComparer.OrdinalIgnoreCase);

    // Label -> English name from the definitions (first entry wins, and the
    // English names come first in CategoryMap).
    private static readonly Dictionary<string, string> _categoryKeys = BuildCategoryKeys();

    private static Dictionary<string, string> BuildCategoryKeys()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in CategoryMap) d.TryAdd(kv.Value, kv.Key);
        return d;
    }

    /// <summary>English category name for a French label (the stable key), or the label itself.</summary>
    public static string CategoryKey(string label) =>
        _categoryKeys.TryGetValue(label, out var key) ? key : label;

    /// <summary>French label of a category; known French labels pass through, unknown names stay as they are.</summary>
    public static string Category(string? category)
    {
        var c = (category ?? string.Empty).Trim();
        if (c.Length == 0) return "Général";
        if (CategoryMap.TryGetValue(c, out var fr)) return fr;
        var label = _categoryLabels.FirstOrDefault(l => string.Equals(l, c, StringComparison.OrdinalIgnoreCase));
        return label ?? c;
    }

    /// <summary>Badge title in the default language (French), or the given fallback.</summary>
    public static string BadgeTitle(string? id, string? fallback)
    {
        if (string.IsNullOrEmpty(id)) return fallback ?? string.Empty;
        return BadgeLocalizer.Lookup(id, "fr").title ?? fallback ?? string.Empty;
    }

    private static string S(int n, string singular, string plural) => n == 1 ? singular : plural;

    /// <summary>French "what to do next" line for a smart goal.</summary>
    public static string GoalAction(AchievementMetric metric, int remaining)
    {
        var n = remaining;
        return metric switch
        {
            AchievementMetric.MoviesWatched => $"Regardez encore {n} {S(n, "film", "films")}",
            AchievementMetric.TotalItemsWatched => $"Regardez encore {n} {S(n, "titre", "titres")}",
            AchievementMetric.SeriesCompleted => $"Terminez encore {n} {S(n, "série", "séries")}",
            AchievementMetric.LateNightSessions => $"Encore {n} {S(n, "séance", "séances")} de nuit",
            AchievementMetric.EarlyMorningSessions => $"Encore {n} {S(n, "séance", "séances")} au petit matin",
            AchievementMetric.WeekendSessions => $"Encore {n} {S(n, "séance", "séances")} le week-end",
            AchievementMetric.UniqueDecadesWatched => $"Regardez des titres de {n} {S(n, "nouvelle décennie", "nouvelles décennies")}",
            AchievementMetric.UniqueCountriesWatched => $"Regardez des titres de {n} {S(n, "nouveau pays", "nouveaux pays")}",
            AchievementMetric.UniqueLanguagesWatched => $"Regardez en {n} {S(n, "nouvelle langue", "nouvelles langues")}",
            AchievementMetric.UniqueGenresWatched => $"Explorez {n} {S(n, "nouveau genre", "nouveaux genres")}",
            AchievementMetric.UniqueLibrariesVisited => $"Regardez dans {n} {S(n, "nouvelle bibliothèque", "nouvelles bibliothèques")}",
            AchievementMetric.TotalMinutesWatched => $"Regardez encore {Math.Max(1, (int)Math.Round(n / 60.0))} h",
            AchievementMetric.DaysWatched => $"Regardez encore {n} {S(n, "jour", "jours")}",
            AchievementMetric.RewatchCount => $"Revoyez encore {n} {S(n, "titre", "titres")}",
            AchievementMetric.GamePlays => $"Jouez encore {n} {S(n, "session", "sessions")}",
            AchievementMetric.GamePlayHours => $"Jouez encore {n} h",
            AchievementMetric.GameHours => $"Jouez encore {n} h à ce jeu",
            AchievementMetric.UniqueGamesPlayed => $"Jouez à {n} {S(n, "nouveau jeu", "nouveaux jeux")}",
            AchievementMetric.UniqueGamePlatforms => $"Jouez sur {n} {S(n, "nouvelle plateforme", "nouvelles plateformes")}",
            AchievementMetric.GamePlatformGames => $"Jouez à encore {n} {S(n, "jeu", "jeux")} sur cette plateforme",
            AchievementMetric.GameStudioGames => $"Jouez à encore {n} {S(n, "jeu", "jeux")} de ce studio",
            _ => $"Encore {n}"
        };
    }
}
