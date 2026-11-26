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
}
