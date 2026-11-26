using System;
using System.IO;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.AchievementBadges.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

/// <summary>
/// 3.0.2: the Nflix aggregate localized badges after the secret mask was
/// applied, so French titles replaced "???" and leaked secret badges.
/// </summary>
public class NflixSnapshotSecretTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AchievementBadgeService _badges;
    private static readonly Guid User = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public NflixSnapshotSecretTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "absnap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.PluginConfigurationsPath).Returns(_dataDir);
        var users = new Mock<IUserManager>();
        users.Setup(m => m.GetUserById(User)).Returns(new User("Alice", "prov", "reset"));
        _badges = new AchievementBadgeService(
            paths.Object,
            users.Object,
            new WebhookNotifier(NullLogger<WebhookNotifier>.Instance),
            new AuditLogService(paths.Object, NullLogger<AuditLogService>.Instance),
            NullLogger<AchievementBadgeService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void LockedSecretBadges_StayMasked_InFrenchSnapshot()
    {
        var (badges, _, summary) = _badges.GetNflixSnapshot(User.ToString(), "fr");

        var secret = badges.Single(b => b.Id == "hidden-night-shift");
        Assert.False(secret.Unlocked);
        Assert.Equal("???", secret.Title);
        Assert.Equal("Succès secret — continuez à regarder pour le découvrir.", secret.Description);
        Assert.Equal("help", secret.Icon);
        Assert.NotNull(summary);
    }
}
