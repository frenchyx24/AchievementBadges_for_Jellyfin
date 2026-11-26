using System;

namespace Jellyfin.Plugin.AchievementBadges.Helpers;

/// <summary>Seasonal event state: Name and EndsAt are null when no event runs.</summary>
public sealed record NflixSeasonState(bool Active, string? Name, DateTime? EndsAt);

/// <summary>
/// Seasonal windows (UTC, inclusive days): Halloween Oct 15 – Nov 2,
/// Noël Dec 10 – Jan 2, Été Jun 21 – Aug 31. EndsAt is the first instant
/// after the last day (00:00 UTC the next day).
/// </summary>
public static class SeasonHelper
{
    /// <summary>Season active at the given instant.</summary>
    public static NflixSeasonState At(DateTime now)
    {
        var d = now.Kind == DateTimeKind.Local ? now.ToUniversalTime() : now;
        var y = d.Year;
        var day = d.Date;

        if (day >= new DateTime(y, 10, 15) && day <= new DateTime(y, 11, 2))
            return Active("Halloween", new DateTime(y, 11, 3));
        if (day >= new DateTime(y, 12, 10))
            return Active("Noël", new DateTime(y + 1, 1, 3));
        if (day <= new DateTime(y, 1, 2))
            return Active("Noël", new DateTime(y, 1, 3));
        if (day >= new DateTime(y, 6, 21) && day <= new DateTime(y, 8, 31))
            return Active("Été", new DateTime(y, 9, 1));

        return new NflixSeasonState(false, null, null);
    }

    private static NflixSeasonState Active(string name, DateTime endsAt)
        => new(true, name, DateTime.SpecifyKind(endsAt, DateTimeKind.Utc));
}
