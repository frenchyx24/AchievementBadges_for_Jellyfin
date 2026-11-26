using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.AchievementBadges.Services;

/// <summary>A duel between two users, persisted in succes-v2.json.</summary>
public sealed class SuccesV3Duel
{
    /// <summary>Gets or sets the duel id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the challenger (normalized user id).</summary>
    public string ChallengerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the opponent (normalized user id).</summary>
    public string OpponentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind: movies, episodes or hours.</summary>
    public string Kind { get; set; } = "movies";

    /// <summary>Gets or sets the target.</summary>
    public int Target { get; set; }

    /// <summary>Gets or sets the duration in days.</summary>
    public int Days { get; set; }

    /// <summary>Gets or sets when the challenge was sent.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the opponent accepted.</summary>
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>Gets or sets the end of the duel.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the opponent declined.</summary>
    public bool Declined { get; set; }

    /// <summary>Gets or sets the challenger counter snapshot at accept.</summary>
    public long ChallengerStart { get; set; }

    /// <summary>Gets or sets the opponent counter snapshot at accept.</summary>
    public long OpponentStart { get; set; }

    /// <summary>Gets or sets a value indicating whether the duel is decided.</summary>
    public bool Resolved { get; set; }

    /// <summary>Gets or sets the winner id; empty for a draw.</summary>
    public string? WinnerId { get; set; }

    /// <summary>Gets or sets the challenger's final progress.</summary>
    public long ChallengerFinal { get; set; }

    /// <summary>Gets or sets the opponent's final progress.</summary>
    public long OpponentFinal { get; set; }
}

/// <summary>Body of POST duels.</summary>
public sealed class NflixDuelCreateRequest
{
    /// <summary>Gets or sets the opponent id.</summary>
    public string? OpponentId { get; set; }

    /// <summary>Gets or sets the kind.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the target.</summary>
    public int Target { get; set; }

    /// <summary>Gets or sets the duration in days.</summary>
    public int Days { get; set; }
}

/// <summary>Body of POST unlock-feed/react.</summary>
public sealed class NflixReactRequest
{
    /// <summary>Gets or sets the unlock key (userId:badgeId).</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the emoji.</summary>
    public string? Emoji { get; set; }
}

/// <summary>Pure rules behind the 3.2.0 social features.</summary>
public static class SuccesV3Math
{
    /// <summary>Hours a pending duel waits for an answer.</summary>
    public const int PendingHours = 72;

    /// <summary>Max open (pending or active) duels per user.</summary>
    public const int MaxOpenDuels = 5;

    /// <summary>The four allowed reactions.</summary>
    public static readonly IReadOnlyList<string> Emojis = new[] { "👏", "🔥", "😮", "❤️" };

    /// <summary>Monday 00:00 of the week holding the given local time.</summary>
    public static DateTime WeekStart(DateTime local)
    {
        var d = local.Date;
        var back = ((int)d.DayOfWeek + 6) % 7;
        return d.AddDays(-back);
    }

    /// <summary>French label, e.g. « 5 films en 7 jours ».</summary>
    public static string DuelLabel(string kind, int target, int days)
    {
        var unit = kind switch
        {
            "episodes" => "épisode",
            "hours" => "heure",
            _ => "film"
        };
        return $"{target} {unit}{(target > 1 ? "s" : string.Empty)} en {days} jour{(days > 1 ? "s" : string.Empty)}";
    }

    /// <summary>Outcome from one side: active, won, lost or draw.</summary>
    public static string DuelOutcome(long mine, long theirs, int target, bool ended)
    {
        var m = mine >= target;
        var t = theirs >= target;
        if (m && t) return "draw";
        if (m) return "won";
        if (t) return "lost";
        if (!ended) return "active";
        if (mine > theirs) return "won";
        if (theirs > mine) return "lost";
        return "draw";
    }

    /// <summary>Status of a stored duel for one viewer.</summary>
    public static string DuelStatus(SuccesV3Duel duel, string viewerId, DateTimeOffset now)
    {
        if (duel.Declined) return "declined";
        if (duel.StartsAt is null) return duel.CreatedAt.AddHours(PendingHours) <= now ? "expired" : "pending";
        if (!duel.Resolved) return "active";
        if (string.IsNullOrEmpty(duel.WinnerId)) return "draw";
        return string.Equals(duel.WinnerId, viewerId, StringComparison.Ordinal) ? "won" : "lost";
    }

    /// <summary>True while a duel still counts toward the open limit.</summary>
    public static bool IsOpen(SuccesV3Duel duel, DateTimeOffset now)
    {
        if (duel.Declined || duel.Resolved) return false;
        if (duel.StartsAt is null) return duel.CreatedAt.AddHours(PendingHours) > now;
        return duel.EndsAt is { } end && end > now;
    }

    /// <summary>Progress units from a raw counter delta (minutes become hours).</summary>
    public static long Progress(string kind, long delta)
        => Math.Max(0, kind == "hours" ? delta / 60 : delta);

    /// <summary>Toggles a reaction; false when the emoji is not allowed.</summary>
    public static bool ToggleReaction(Dictionary<string, List<string>> map, string emoji, string userId)
    {
        if (!Emojis.Contains(emoji)) return false;
        if (!map.TryGetValue(emoji, out var users))
        {
            users = new List<string>();
            map[emoji] = users;
        }

        if (!users.Remove(userId)) users.Add(userId);
        if (users.Count == 0) map.Remove(emoji);
        return true;
    }
}
