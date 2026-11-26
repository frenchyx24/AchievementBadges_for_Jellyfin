using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AchievementBadges.Helpers;
using Jellyfin.Plugin.AchievementBadges.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AchievementBadges.Services;

/// <summary>One thematic collection built from a Jellyfin BoxSet.</summary>
public sealed record NflixCollectionProgress(string Id, string Name, int Total, int Watched, int Percent, string PosterItemId, bool Completed);

/// <summary>One profile cosmetic (frame or title).</summary>
public sealed record NflixProfileCosmetic(string Id, string Label, bool Unlocked, string Source);

/// <summary>Body of POST users/{userId}/profile-cosmetics/equip. null keeps, "" unequips.</summary>
public sealed class NflixProfileCosmeticEquipRequest
{
    /// <summary>Frame id to equip.</summary>
    public string? Frame { get; set; }

    /// <summary>Title id to equip.</summary>
    public string? Title { get; set; }
}

/// <summary>
/// "Succès v2" lot 3: secret hints, collections, season pass, profile
/// frames/titles and the daily challenge. State lives in succes-v2.json.
/// </summary>
public class SuccesV2Service
{
    private static readonly TimeSpan CollectionsTtl = TimeSpan.FromMinutes(10);

    private readonly AchievementBadgeService _badges;
    private readonly QuestService _quests;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<SuccesV2Service> _logger;
    internal SuccesV2Store Store => _store;
    private readonly SuccesV2Store _store;
    private readonly ConcurrentDictionary<string, (DateTime At, List<NflixCollectionProgress> Items)> _collectionsCache = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="SuccesV2Service"/> class.</summary>
    public SuccesV2Service(
        IApplicationPaths applicationPaths,
        AchievementBadgeService badges,
        QuestService quests,
        ILibraryManager libraryManager,
        IUserManager userManager,
        ILogger<SuccesV2Service> logger)
    {
        _badges = badges;
        _quests = quests;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
        var dir = Path.Combine(applicationPaths.PluginConfigurationsPath, "achievementbadges");
        _store = new SuccesV2Store(Path.Combine(dir, "succes-v2.json"), logger);
    }

    // ---------- 1. Secret hints ----------

    /// <summary>Hints for every hidden badge still locked.</summary>
    public List<object> GetSecretHints(string userId)
    {
        _badges.GetBadgesForUser(userId); // evaluates and saves counters first
        var raw = _badges.PeekProfile(userId)?.Badges?.ToList() ?? new List<AchievementBadge>();
        var byId = raw.Where(b => b is not null).GroupBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var result = new List<object>();
        foreach (var def in _badges.GetActiveDefinitions().Where(d => d.IsSecret))
        {
            byId.TryGetValue(def.Id, out var badge);
            if (badge?.Unlocked == true) continue;
            var target = def.TargetValue > 0 ? def.TargetValue : badge?.TargetValue ?? 0;
            var percent = SuccesV2Math.Percent(badge?.CurrentValue ?? 0, target);
            var level = SuccesV2Math.HintLevel(percent);
            result.Add(new
            {
                def.Id,
                Hint = SuccesV2Math.HintText(def.Metric, target, level),
                HintLevel = level,
                Percent = percent
            });
        }

        return result;
    }

    // ---------- 2. Collections ----------

    /// <summary>BoxSets visible to the user with watched movie counts; cached 10 min.</summary>
    public object GetCollections(string userId)
    {
        var key = SuccesV2Store.NormalizeUserId(userId);
        List<NflixCollectionProgress> items;
        if (_collectionsCache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < CollectionsTtl)
        {
            items = hit.Items;
        }
        else
        {
            items = ComputeCollections(userId);
            _collectionsCache[key] = (DateTime.UtcNow, items);
        }

        var now = DateTimeOffset.UtcNow;
        var completedEver = _store.Update(userId, s =>
        {
            var dirty = false;
            foreach (var c in items.Where(c => c.Completed))
            {
                if (s.CompletedCollections.TryAdd(c.Id, now)) dirty = true;
            }

            return (s.CompletedCollections.Count, dirty);
        });

        var bonusXp = (long)completedEver * SuccesV2Math.CollectionXp;
        var level = LevelHelper.FromBadges(_badges.GetBadgesForUser(userId), bonusXp);
        return new
        {
            Collections = items,
            CompletedCount = completedEver,
            BonusXp = bonusXp,
            Level = new { level.Level, level.Xp, level.XpIntoLevel, level.XpForNextLevel, level.Title }
        };
    }

    private List<NflixCollectionProgress> ComputeCollections(string userId)
    {
        var result = new List<NflixCollectionProgress>();
        if (!Guid.TryParse(userId, out var guid)) return result;
        var user = _userManager.GetUserById(guid);
        if (user is null) return result;
        try
        {
            var sets = _libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                Recursive = true
            });
            foreach (var set in sets.OfType<Folder>())
            {
                var movieIds = set.GetChildren(user, true).OfType<Movie>().Select(m => m.Id).Distinct().ToArray();
                if (movieIds.Length == 0) continue;
                var watched = _libraryManager.GetItemList(new InternalItemsQuery(user)
                {
                    ItemIds = movieIds,
                    IsPlayed = true
                }).Count;
                watched = Math.Min(watched, movieIds.Length);
                result.Add(new NflixCollectionProgress(
                    set.Id.ToString("N"),
                    set.Name ?? string.Empty,
                    movieIds.Length,
                    watched,
                    SuccesV2Math.Percent(watched, movieIds.Length),
                    set.Id.ToString("N"),
                    watched >= movieIds.Length));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "[AchievementBadges] collections query failed for {UserId}", userId);
        }

        return result.OrderByDescending(c => c.Percent).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // ---------- 3. Season pass ----------

    /// <summary>Season pass of the current season; records earned rewards.</summary>
    public object GetSeasonPass(string userId) => GetSeasonPass(userId, DateTime.UtcNow);

    /// <summary>Season pass at a given instant.</summary>
    public object GetSeasonPass(string userId, DateTime nowUtc)
    {
        var (season, xp, tier) = ComputeSeason(userId, nowUtc);
        var tiers = Enumerable.Range(1, SuccesV2Math.SeasonTierCount).Select(t =>
        {
            var r = SuccesV2Math.TierReward(season, t);
            return new
            {
                Tier = t,
                XpRequired = SuccesV2Math.XpRequiredForTier(t),
                Reward = new { r.Type, r.Id, r.Label },
                Unlocked = t <= tier
            };
        }).ToList();
        return new
        {
            Season = new { season.Id, season.Name, StartsAt = season.Start.ToString("o"), EndsAt = season.End.ToString("o") },
            season.Id,
            season.Name,
            EndsAt = season.End.ToString("o"),
            CurrentTier = tier,
            SeasonXp = xp,
            XpPerTier = SuccesV2Math.SeasonXpPerTier,
            NextTierXp = tier >= SuccesV2Math.SeasonTierCount ? (long?)null : SuccesV2Math.XpRequiredForTier(tier + 1),
            Tiers = tiers
        };
    }

    private (NflixSeasonWindow Season, long Xp, int Tier) ComputeSeason(string userId, DateTime nowUtc)
    {
        var season = SuccesV2Math.SeasonAt(nowUtc);
        long badgeXp = 0;
        foreach (var b in _badges.GetBadgesForUser(userId))
        {
            if (b is not null && b.Unlocked && b.UnlockedAt is { } at && SuccesV2Math.InSeason(season, at))
                badgeXp += LevelHelper.XpForRarity(b.Rarity);
        }

        var observed = ObservedCompletedQuests(userId);
        return _store.Update(userId, s =>
        {
            var dirty = false;
            foreach (var (key, log) in observed)
            {
                if (s.CompletedQuests.TryAdd(key, log)) dirty = true;
            }

            long xp = badgeXp;
            xp += s.CompletedQuests.Values.Where(q => SuccesV2Math.InSeason(season, q.At)).Sum(q => (long)q.Xp);
            xp += s.CompletedCollections.Values.Count(at => SuccesV2Math.InSeason(season, at)) * (long)SuccesV2Math.CollectionXp;
            var tier = SuccesV2Math.TierForXp(xp);
            for (var t = 1; t <= tier; t++)
            {
                var r = SuccesV2Math.TierReward(season, t);
                if (s.SeasonRewards.TryAdd(r.Id, new SuccesV2EarnedReward { Type = r.Type, Label = r.Label, SeasonId = season.Id, Tier = t }))
                    dirty = true;
            }

            return ((season, xp, tier), dirty);
        });
    }

    private List<(string Key, SuccesV2QuestLog Log)> ObservedCompletedQuests(string userId)
    {
        var list = new List<(string, SuccesV2QuestLog)>();
        var profile = _badges.PeekProfile(userId);
        if (profile is null) return list;
        void Scan(IEnumerable<QuestState>? states, IReadOnlyList<QuestService.QuestTemplate> pool, string kind)
        {
            foreach (var q in states?.ToList() ?? new List<QuestState>())
            {
                if (q is null || !q.Completed) continue;
                var start = SuccesV2Math.QuestPeriodStart(q.Period);
                if (start is null) continue;
                var reward = pool.FirstOrDefault(t => t.Id == q.Id)?.Reward ?? 0;
                list.Add((kind + ":" + q.Period + ":" + q.Id, new SuccesV2QuestLog { Xp = Math.Max(0, reward), At = new DateTimeOffset(start.Value, TimeSpan.Zero) }));
            }
        }

        Scan(profile.DailyQuests, QuestService.GetEffectiveDailyPool(), "daily");
        Scan(profile.WeeklyQuests, QuestService.GetEffectiveWeeklyPool(), "weekly");
        return list;
    }

    // ---------- 4. Profile cosmetics ----------

    private (List<NflixProfileCosmetic> Frames, List<NflixProfileCosmetic> Titles, SuccesV2UserState State) BuildCosmetics(string userId)
    {
        var (season, _, tier) = ComputeSeason(userId, DateTime.UtcNow);
        var state = _store.Update(userId, s => (s, false));
        var bonus = (long)state.CompletedCollections.Count * SuccesV2Math.CollectionXp;
        var level = LevelHelper.FromBadges(_badges.GetBadgesForUser(userId), bonus).Level;

        var frames = new List<NflixProfileCosmetic>();
        var titles = new List<NflixProfileCosmetic>();
        foreach (var l in SuccesV2Math.LevelThresholds)
        {
            var src = "Niveau " + l;
            frames.Add(new NflixProfileCosmetic(SuccesV2Math.LevelFrameId(l), SuccesV2Math.LevelFrameLabel(l), level >= l, src));
            titles.Add(new NflixProfileCosmetic(SuccesV2Math.LevelTitleId(l), LevelHelper.TitleFor(l), level >= l, src));
        }

        var earned = state.SeasonRewards.ToList();
        foreach (var (id, r) in earned.Where(e => e.Value.SeasonId != season.Id).OrderBy(e => e.Value.SeasonId, StringComparer.Ordinal).ThenBy(e => e.Value.Tier))
        {
            var c = new NflixProfileCosmetic(id, r.Label, true, "Passe de saison " + r.SeasonId + " · palier " + r.Tier);
            (r.Type == "title" ? titles : frames).Add(c);
        }

        for (var t = 1; t <= SuccesV2Math.SeasonTierCount; t++)
        {
            var r = SuccesV2Math.TierReward(season, t);
            var c = new NflixProfileCosmetic(r.Id, r.Label, t <= tier || state.SeasonRewards.ContainsKey(r.Id), "Passe de saison " + season.Name + " · palier " + t);
            (r.Type == "title" ? titles : frames).Add(c);
        }

        return (frames, titles, state);
    }

    /// <summary>All frames and titles with unlock state and what is equipped.</summary>
    public object GetCosmetics(string userId)
    {
        var (frames, titles, state) = BuildCosmetics(userId);
        return new
        {
            Frames = frames,
            Titles = titles,
            EquippedFrame = state.EquippedFrame,
            EquippedTitle = state.EquippedTitle
        };
    }

    /// <summary>Equips a frame and/or title; only unlocked ids are accepted.</summary>
    public (bool Ok, string Message, object? Result) Equip(string userId, NflixProfileCosmeticEquipRequest? request)
    {
        if (request is null || (request.Frame is null && request.Title is null))
            return (false, "Indique un cadre ou un titre.", null);
        var (frames, titles, _) = BuildCosmetics(userId);
        if (!string.IsNullOrEmpty(request.Frame) && !frames.Any(f => f.Unlocked && f.Id == request.Frame))
            return (false, "Ce cadre n'est pas débloqué.", null);
        if (!string.IsNullOrEmpty(request.Title) && !titles.Any(f => f.Unlocked && f.Id == request.Title))
            return (false, "Ce titre n'est pas débloqué.", null);
        var state = _store.Update(userId, s =>
        {
            if (request.Frame is not null) s.EquippedFrame = request.Frame.Length == 0 ? null : request.Frame;
            if (request.Title is not null) s.EquippedTitle = request.Title.Length == 0 ? null : request.Title;
            return ((s.EquippedFrame, s.EquippedTitle), true);
        });
        return (true, "Cosmétiques mis à jour.", new { EquippedFrame = state.EquippedFrame, EquippedTitle = state.EquippedTitle });
    }

    /// <summary>Equipped frame and title of a user, with labels (public view).</summary>
    public object GetPublicEquipped(string targetUserId)
    {
        var state = _store.Update(targetUserId, s => (s, false));
        return new
        {
            EquippedFrame = state.EquippedFrame is null ? null : new { Id = state.EquippedFrame, Label = LabelFor(state, state.EquippedFrame, true) },
            EquippedTitle = state.EquippedTitle is null ? null : new { Id = state.EquippedTitle, Label = LabelFor(state, state.EquippedTitle, false) }
        };
    }

    private static string LabelFor(SuccesV2UserState state, string id, bool frame)
    {
        if (state.SeasonRewards.TryGetValue(id, out var r)) return r.Label;
        foreach (var l in SuccesV2Math.LevelThresholds)
        {
            if (frame && id == SuccesV2Math.LevelFrameId(l)) return SuccesV2Math.LevelFrameLabel(l);
            if (!frame && id == SuccesV2Math.LevelTitleId(l)) return LevelHelper.TitleFor(l);
        }

        return id;
    }

    // ---------- 5. Daily challenge ----------

    /// <summary>Today's first unfinished daily quest (or the first one when all are done).</summary>
    public object? GetDailyChallenge(string userId)
    {
        var list = _quests.GetOrCreateDailyList(userId);
        if (list.Count == 0) return null;
        var pick = list.FirstOrDefault(q => !Prop<bool>(q, "Completed")) ?? list[0];
        var expires = new DateTimeOffset(DateTime.Today.AddDays(1));
        return new
        {
            Id = Prop<string>(pick, "Id"),
            Title = Prop<string>(pick, "Title"),
            Description = Prop<string>(pick, "Description"),
            Icon = Prop<string>(pick, "Icon"),
            Progress = Prop<int>(pick, "Current"),
            Target = Prop<int>(pick, "Target"),
            RewardXp = Prop<int>(pick, "Reward"),
            Completed = Prop<bool>(pick, "Completed"),
            ExpiresAt = expires.ToString("o")
        };
    }

    private static T? Prop<T>(object o, string name)
        => o.GetType().GetProperty(name)?.GetValue(o) is T v ? v : default;
}
