using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AchievementBadges.Services;

/// <summary>Per-user state of the "Succès v2" features.</summary>
public sealed class SuccesV2UserState
{
    /// <summary>Equipped profile frame id.</summary>
    public string? EquippedFrame { get; set; }

    /// <summary>Equipped profile title id.</summary>
    public string? EquippedTitle { get; set; }

    /// <summary>Collection (BoxSet) id → first completion time.</summary>
    public Dictionary<string, DateTimeOffset> CompletedCollections { get; set; } = new(StringComparer.Ordinal);

    /// <summary>"kind:period:questId" → XP and period start, for the season pass.</summary>
    public Dictionary<string, SuccesV2QuestLog> CompletedQuests { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Season-pass rewards earned, kept across seasons.</summary>
    public Dictionary<string, SuccesV2EarnedReward> SeasonRewards { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>One completed quest.</summary>
public sealed class SuccesV2QuestLog
{
    /// <summary>XP granted.</summary>
    public int Xp { get; set; }

    /// <summary>Start of the quest period.</summary>
    public DateTimeOffset At { get; set; }
}

/// <summary>One earned season-pass reward.</summary>
public sealed class SuccesV2EarnedReward
{
    /// <summary>"frame" or "title".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>French label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Season id, e.g. 2026-automne.</summary>
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>Tier number.</summary>
    public int Tier { get; set; }
}

/// <summary>File root of succes-v2.json.</summary>
public sealed class SuccesV2StoreData
{
    /// <summary>Normalized user id → state.</summary>
    public Dictionary<string, SuccesV2UserState> Users { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>JSON file store (succes-v2.json) guarded by one lock, atomic writes.</summary>
public sealed class SuccesV2Store
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly object _lock = new();
    private readonly string _path;
    private readonly ILogger? _logger;
    private SuccesV2StoreData? _data;

    /// <summary>Initializes a new instance of the <see cref="SuccesV2Store"/> class.</summary>
    public SuccesV2Store(string path, ILogger? logger = null)
    {
        _path = path;
        _logger = logger;
    }

    /// <summary>Normalizes a user id (dashless lowercase guid when parseable).</summary>
    public static string NormalizeUserId(string userId)
        => Guid.TryParse(userId, out var g) ? g.ToString("N") : (userId ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Runs fn on the user's state under the lock; saves when fn reports dirty.</summary>
    public T Update<T>(string userId, Func<SuccesV2UserState, (T Value, bool Dirty)> fn)
    {
        var key = NormalizeUserId(userId);
        lock (_lock)
        {
            var data = LoadLocked();
            if (!data.Users.TryGetValue(key, out var state))
            {
                state = new SuccesV2UserState();
                data.Users[key] = state;
            }

            var (value, dirty) = fn(state);
            if (dirty) SaveLocked(data);
            return value;
        }
    }

    private SuccesV2StoreData LoadLocked()
    {
        if (_data is not null) return _data;
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<SuccesV2StoreData>(File.ReadAllText(_path), JsonOptions);
                if (loaded is not null)
                {
                    _data = new SuccesV2StoreData
                    {
                        Users = new Dictionary<string, SuccesV2UserState>(loaded.Users ?? new(), StringComparer.OrdinalIgnoreCase)
                    };
                    return _data;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "[AchievementBadges] succes-v2.json unreadable; starting empty and keeping a .bad copy");
            try { File.Copy(_path, _path + ".bad", true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        _data = new SuccesV2StoreData();
        return _data;
    }

    private void SaveLocked(SuccesV2StoreData data)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions));
            File.Move(tmp, _path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "[AchievementBadges] cannot write succes-v2.json");
        }
    }
}
