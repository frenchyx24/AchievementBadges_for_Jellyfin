using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.AchievementBadges.Helpers;
using Jellyfin.Plugin.AchievementBadges.Models;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.AchievementBadges.Services;

/// <summary>3.2.0: weekly board, duels, unlock reactions, progress alerts.</summary>
public class SuccesV3Service
{
    private readonly SuccesV2Service _v2;
    private readonly AchievementBadgeService _badges;
    private readonly IUserManager _userManager;
    private readonly SuccesV2Store _store;

    /// <summary>Initializes a new instance of the <see cref="SuccesV3Service"/> class.</summary>
    public SuccesV3Service(SuccesV2Service v2, AchievementBadgeService badges, IUserManager userManager)
    {
        _v2 = v2;
        _badges = badges;
        _userManager = userManager;
        _store = v2.Store; // one store instance per file, never two
    }

    private static string N(string id) => SuccesV2Store.NormalizeUserId(id);

    private string? NameOf(string id)
        => Guid.TryParse(id, out var g) ? _userManager.GetUserById(g)?.Username : null;

    private static List<string> ParseIds(string userId, string? ids)
    {
        var list = new List<string> { N(userId) };
        foreach (var p in (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(30))
        {
            if (!Guid.TryParse(p, out var g)) continue;
            var n = g.ToString("N");
            if (!list.Contains(n)) list.Add(n);
        }

        return list;
    }

    /// <summary>Ids that exist and the caller may see, caller first.</summary>
    private List<(string Id, string Name)> VisibleUsers(string userId, string? ids)
    {
        var me = N(userId);
        var result = new List<(string, string)>();
        foreach (var id in ParseIds(userId, ids))
        {
            var name = NameOf(id);
            if (name is null) continue;
            if (id != me && !_badges.CanSee(userId, id)) continue;
            result.Add((id, name));
        }

        return result;
    }

    private Dictionary<string, string> FrTitles(string id)
        => _badges.GetNflixSnapshot(id, "fr").Badges
            .Where(b => b is not null)
            .GroupBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Title, StringComparer.OrdinalIgnoreCase);

    // ---------- 1. Weekly board ----------

    /// <summary>XP earned since Monday 00:00 (server time) by the caller and the given ids.</summary>
    public List<object> GetWeeklyBoard(string userId, string? ids, DateTime? nowLocal = null)
    {
        var weekStartLocal = SuccesV3Math.WeekStart(nowLocal ?? DateTime.Now);
        var weekStart = new DateTimeOffset(weekStartLocal);
        var rows = new List<(string Id, string Name, long WeekXp, int Level, string Title)>();
        foreach (var (id, name) in VisibleUsers(userId, ids))
        {
            var badges = _badges.GetBadgesForUser(id);
            long xp = badges
                .Where(b => b is not null && b.Unlocked && b.UnlockedAt is { } at && at >= weekStart)
                .Sum(b => (long)LevelHelper.XpForRarity(b.Rarity));
            _v2.GetSeasonPass(id); // syncs completed quests into the store
            xp += _store.Update(id, s => (s.CompletedQuests
                .Where(kv => kv.Key.StartsWith("daily:", StringComparison.Ordinal) && kv.Value.At.UtcDateTime.Date >= weekStartLocal.Date)
                .Sum(kv => (long)kv.Value.Xp), false));
            var level = LevelHelper.FromBadges(badges);
            rows.Add((id, name, xp, level.Level, level.Title));
        }

        return rows
            .OrderByDescending(r => r.WeekXp)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) => (object)new { UserId = r.Id, r.Name, r.WeekXp, r.Level, r.Title, Rank = i + 1 })
            .ToList();
    }

    // ---------- 2. Duels ----------

    private long CounterValue(string userId, string kind)
    {
        var c = _badges.PeekProfile(userId)?.Counters;
        if (c is null) return 0;
        return kind switch
        {
            "episodes" => c.EpisodesByDate?.Values.Sum(v => (long)v) ?? 0,
            "hours" => c.TotalMinutesWatched,
            _ => c.MoviesWatched
        };
    }

    /// <summary>Sends a duel to another user.</summary>
    public (bool Ok, string Message, object? Result) CreateDuel(string userId, NflixDuelCreateRequest? request, DateTimeOffset? nowArg = null)
    {
        if (request is null) return (false, "Requête invalide.", null);
        var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind is not ("movies" or "episodes" or "hours")) return (false, "Type de défi inconnu.", null);
        if (request.Target < 1 || request.Target > 100) return (false, "La cible doit être entre 1 et 100.", null);
        if (request.Days < 1 || request.Days > 14) return (false, "La durée doit être entre 1 et 14 jours.", null);
        var me = N(userId);
        if (!Guid.TryParse(request.OpponentId, out var g)) return (false, "Adversaire introuvable.", null);
        var opp = g.ToString("N");
        if (opp == me) return (false, "Impossible de se défier soi-même.", null);
        if (NameOf(opp) is null || !_badges.CanSee(userId, opp)) return (false, "Adversaire introuvable.", null);
        var now = nowArg ?? DateTimeOffset.UtcNow;
        return _store.UpdateAll(d =>
        {
            if (d.Duels.Count(x => (x.ChallengerId == me || x.OpponentId == me) && SuccesV3Math.IsOpen(x, now)) >= SuccesV3Math.MaxOpenDuels)
                return ((false, "Tu as déjà 5 défis en cours.", (object?)null), false);
            if (d.Duels.Count(x => (x.ChallengerId == opp || x.OpponentId == opp) && SuccesV3Math.IsOpen(x, now)) >= SuccesV3Math.MaxOpenDuels)
                return ((false, "Ton adversaire a déjà 5 défis en cours.", (object?)null), false);
            var duel = new SuccesV3Duel
            {
                Id = Guid.NewGuid().ToString("N"),
                ChallengerId = me,
                OpponentId = opp,
                Kind = kind,
                Target = request.Target,
                Days = request.Days,
                CreatedAt = now
            };
            d.Duels.Add(duel);
            return ((true, "Défi envoyé.", (object?)new { duel.Id }), true);
        });
    }

    /// <summary>Accept or decline; only the opponent may answer.</summary>
    public (bool Ok, string Message, object? Result) RespondDuel(string userId, string duelId, bool accept, DateTimeOffset? nowArg = null)
    {
        var me = N(userId);
        var now = nowArg ?? DateTimeOffset.UtcNow;
        return _store.UpdateAll(d =>
        {
            var duel = d.Duels.FirstOrDefault(x => string.Equals(x.Id, duelId, StringComparison.OrdinalIgnoreCase));
            if (duel is null) return ((false, "Défi introuvable.", (object?)null), false);
            if (duel.OpponentId != me) return ((false, "Seul l'adversaire peut répondre.", (object?)null), false);
            if (duel.StartsAt is not null || duel.Declined) return ((false, "Ce défi a déjà reçu une réponse.", (object?)null), false);
            if (!SuccesV3Math.IsOpen(duel, now)) return ((false, "Ce défi a expiré.", (object?)null), false);
            if (!accept)
            {
                duel.Declined = true;
                return ((true, "Défi refusé.", (object?)new { duel.Id }), true);
            }

            duel.StartsAt = now;
            duel.EndsAt = now.AddDays(duel.Days);
            duel.ChallengerStart = CounterValue(duel.ChallengerId, duel.Kind);
            duel.OpponentStart = CounterValue(duel.OpponentId, duel.Kind);
            return ((true, "Défi accepté.", (object?)new { duel.Id }), true);
        });
    }

    /// <summary>Every duel of the user, resolving the finished ones.</summary>
    public List<object> GetDuels(string userId, DateTimeOffset? nowArg = null)
    {
        var me = N(userId);
        var now = nowArg ?? DateTimeOffset.UtcNow;
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        string Name(string id)
        {
            if (!names.TryGetValue(id, out var n)) names[id] = n = NameOf(id) ?? "Utilisateur supprimé";
            return n;
        }

        return _store.UpdateAll(d =>
        {
            var dirty = d.Duels.RemoveAll(x => !SuccesV3Math.IsOpen(x, now)
                && (x.EndsAt ?? x.CreatedAt.AddHours(SuccesV3Math.PendingHours)) < now.AddDays(-30)) > 0;
            var list = new List<object>();
            foreach (var duel in d.Duels.Where(x => x.ChallengerId == me || x.OpponentId == me).OrderByDescending(x => x.CreatedAt))
            {
                long cp = 0, op = 0;
                if (duel.StartsAt is not null && !duel.Declined)
                {
                    if (duel.Resolved)
                    {
                        cp = duel.ChallengerFinal;
                        op = duel.OpponentFinal;
                    }
                    else
                    {
                        cp = SuccesV3Math.Progress(duel.Kind, CounterValue(duel.ChallengerId, duel.Kind) - duel.ChallengerStart);
                        op = SuccesV3Math.Progress(duel.Kind, CounterValue(duel.OpponentId, duel.Kind) - duel.OpponentStart);
                        var outcome = SuccesV3Math.DuelOutcome(cp, op, duel.Target, duel.EndsAt is { } end && now >= end);
                        if (outcome != "active")
                        {
                            duel.Resolved = true;
                            duel.WinnerId = outcome == "won" ? duel.ChallengerId : outcome == "lost" ? duel.OpponentId : string.Empty;
                            duel.ChallengerFinal = cp;
                            duel.OpponentFinal = op;
                            dirty = true;
                        }
                    }
                }

                var mineIsChallenger = duel.ChallengerId == me;
                list.Add(new
                {
                    duel.Id,
                    duel.ChallengerId,
                    ChallengerName = Name(duel.ChallengerId),
                    duel.OpponentId,
                    OpponentName = Name(duel.OpponentId),
                    duel.Kind,
                    Label = SuccesV3Math.DuelLabel(duel.Kind, duel.Target, duel.Days),
                    duel.Target,
                    StartsAt = duel.StartsAt?.ToString("o"),
                    EndsAt = duel.EndsAt?.ToString("o"),
                    Status = SuccesV3Math.DuelStatus(duel, me, now),
                    MyProgress = mineIsChallenger ? cp : op,
                    TheirProgress = mineIsChallenger ? op : cp
                });
            }

            return (list, dirty);
        });
    }

    // ---------- 3. Unlock feed ----------

    /// <summary>Unlocks of the last 14 days (max 50) with reactions.</summary>
    public List<object> GetUnlockFeed(string userId, string? ids, DateTimeOffset? nowArg = null)
    {
        var me = N(userId);
        var since = (nowArg ?? DateTimeOffset.UtcNow).AddDays(-14);
        var secret = _badges.GetActiveDefinitions().Where(d => d.IsSecret).Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = new List<(string Key, string Uid, string Name, AchievementBadge B, string Title)>();
        foreach (var (id, name) in VisibleUsers(userId, ids))
        {
            var recent = _badges.GetBadgesForUser(id)
                .Where(b => b is not null && b.Unlocked && b.UnlockedAt is { } at && at >= since && (id == me || !secret.Contains(b.Id)))
                .ToList();
            if (recent.Count == 0) continue;
            var fr = FrTitles(id);
            foreach (var b in recent)
                items.Add((id + ":" + b.Id, id, name, b, fr.TryGetValue(b.Id, out var t) && !string.IsNullOrEmpty(t) ? t : b.Title));
        }

        var top = items.OrderByDescending(i => i.B.UnlockedAt).Take(50).ToList();
        var reactions = _store.UpdateAll(d => (top.ToDictionary(
            i => i.Key,
            i => d.Reactions.TryGetValue(i.Key, out var m) ? m.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()) : new Dictionary<string, List<string>>()), false));
        return top.Select(i =>
        {
            var r = reactions[i.Key];
            return (object)new
            {
                i.Key,
                UserId = i.Uid,
                i.Name,
                BadgeId = i.B.Id,
                i.Title,
                i.B.Rarity,
                UnlockedAt = i.B.UnlockedAt?.ToString("o"),
                Reactions = SuccesV3Math.Emojis.ToDictionary(e => e, e => r.TryGetValue(e, out var u) ? u.Count : 0),
                Mine = SuccesV3Math.Emojis.Where(e => r.TryGetValue(e, out var u) && u.Contains(me)).ToList()
            };
        }).ToList();
    }

    /// <summary>Toggles the caller's reaction on an unlock.</summary>
    public (bool Ok, string Message, object? Result) React(string userId, NflixReactRequest? request)
    {
        var me = N(userId);
        var emoji = request?.Emoji ?? string.Empty;
        if (!SuccesV3Math.Emojis.Contains(emoji)) return (false, "Réaction non autorisée.", null);
        var parts = (request?.Key ?? string.Empty).Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var owner) || string.IsNullOrWhiteSpace(parts[1]))
            return (false, "Déblocage introuvable.", null);
        var ownerId = owner.ToString("N");
        if (NameOf(ownerId) is null || (ownerId != me && !_badges.CanSee(userId, ownerId)))
            return (false, "Déblocage introuvable.", null);
        var badge = _badges.GetBadgesForUser(ownerId).FirstOrDefault(b => b is not null && b.Unlocked && string.Equals(b.Id, parts[1], StringComparison.OrdinalIgnoreCase));
        if (badge is null) return (false, "Déblocage introuvable.", null);
        var key = ownerId + ":" + badge.Id;
        return _store.UpdateAll(d =>
        {
            if (!d.Reactions.TryGetValue(key, out var map))
            {
                map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                d.Reactions[key] = map;
            }

            SuccesV3Math.ToggleReaction(map, emoji, me);
            if (map.Count == 0) d.Reactions.Remove(key);
            var result = new
            {
                Key = key,
                Reactions = SuccesV3Math.Emojis.ToDictionary(e => e, e => map.TryGetValue(e, out var u) ? u.Count : 0),
                Mine = SuccesV3Math.Emojis.Where(e => map.TryGetValue(e, out var u) && u.Contains(me)).ToList()
            };
            return ((true, "OK", (object?)result), true);
        });
    }

    // ---------- 4. Progress alerts ----------

    private static string Unit(AchievementMetric metric, long n)
    {
        var s = metric.ToString();
        var u = s.Contains("Movie", StringComparison.Ordinal) ? "film"
            : s.Contains("Episode", StringComparison.Ordinal) ? "épisode"
            : s.Contains("Minute", StringComparison.Ordinal) ? "minute"
            : s.Contains("Series", StringComparison.Ordinal) ? "série"
            : "étape";
        return u + (n > 1 ? "s" : string.Empty);
    }

    /// <summary>Nudges: almost unlocked, streak at risk, duel ending.</summary>
    public List<object> GetProgressAlerts(string userId, DateTime? nowLocal = null)
    {
        var now = nowLocal ?? DateTime.Now;
        var list = new List<object>();
        var badges = _badges.GetBadgesForUser(userId);
        var defs = _badges.GetActiveDefinitions().GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var almost = badges
            .Where(b => b is not null && !b.Unlocked && defs.TryGetValue(b.Id, out var def) && !def.IsSecret)
            .Select(b => (B: b, Def: defs[b.Id], Target: defs[b.Id].TargetValue > 0 ? defs[b.Id].TargetValue : b.TargetValue))
            .Where(x => x.Target > 0)
            .Select(x => (x.B, x.Def, x.Target, Pct: SuccesV2Math.Percent(x.B.CurrentValue, x.Target)))
            .Where(x => x.Pct >= 90 && x.Pct < 100)
            .OrderByDescending(x => x.Pct)
            .Take(3)
            .ToList();
        if (almost.Count > 0)
        {
            var fr = FrTitles(userId);
            foreach (var x in almost)
            {
                var remaining = Math.Max(1L, (long)Math.Ceiling(x.Target - (double)x.B.CurrentValue));
                var title = fr.TryGetValue(x.B.Id, out var t) && !string.IsNullOrEmpty(t) ? t : x.B.Title;
                list.Add(new { Kind = "almost", Title = "Presque débloqué", Body = $"Plus que {remaining} {Unit(x.Def.Metric, remaining)} pour « {title} »", BadgeId = (string?)x.B.Id });
            }
        }

        if (now.Hour >= 18)
        {
            var cal = _badges.GetStreakCalendar(userId);
            var streak = cal.GetType().GetProperty("CurrentStreak")?.GetValue(cal) is int s ? s : 0;
            var last = _badges.PeekProfile(userId)?.Counters?.LastWatchDate;
            if (streak >= 3 && last != DateOnly.FromDateTime(now))
                list.Add(new { Kind = "streak-risk", Title = "Série en danger", Body = $"Ta série de {streak} jours s'arrête ce soir", BadgeId = (string?)null });
        }

        var nowUtc = new DateTimeOffset(now);
        foreach (var o in GetDuels(userId, nowUtc))
        {
            var t = o.GetType();
            if (t.GetProperty("Status")?.GetValue(o) as string != "active") continue;
            if (!DateTimeOffset.TryParse(t.GetProperty("EndsAt")?.GetValue(o) as string, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var end)) continue;
            if (end - nowUtc > TimeSpan.FromHours(24)) continue;
            var me = N(userId);
            var other = (string?)t.GetProperty("ChallengerId")?.GetValue(o) == me ? t.GetProperty("OpponentName")?.GetValue(o) : t.GetProperty("ChallengerName")?.GetValue(o);
            list.Add(new { Kind = "duel", Title = "Défi bientôt terminé", Body = $"Ton défi contre {other} ({t.GetProperty("Label")?.GetValue(o)}) se termine dans moins de 24 h", BadgeId = (string?)null });
        }

        return list;
    }
}
